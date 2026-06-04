using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Area_of_Effect
{
    // =========================================================================
    //  Job 1 of 3 — GridDecimateJob
    //
    //  Spatial grid decimation for the road segment list.
    //  Assigns each road segment to a 2-D grid cell and keeps only one
    //  segment per cell, giving a uniform spatial sample within m_MaxOutput.
    //
    //  This is applied AFTER FindRoadsInRadiusJob / GatherRoadCurvesJob to
    //  prevent the payload from exceeding the Cohtml / JSON size budget.
    // =========================================================================
    [BurstCompile]
    public struct GridDecimateJob : IJob
    {
        // — Inputs ————————————————————————————————————————————————————————————
        [ReadOnly] public NativeList<RoadSegmentItem> m_Input;
        public int m_MaxOutput; // hard cap — must equal PayloadLimits.MaxRoadSegments

        // — Output ————————————————————————————————————————————————————————————
        public NativeList<RoadSegmentItem> m_Output; // pre-cleared by caller

        public void Execute()
        {
            int count = m_Input.Length;
            if (count == 0) return;

            // Fast path: already within budget — copy and return.
            if (count <= m_MaxOutput)
            {
                for (int i = 0; i < count; i++)
                    m_Output.Add(m_Input[i]);
                return;
            }

            // — Step 1: compute the XZ bounding box of all segment midpoints —
            float2 bMin = new float2(float.MaxValue,  float.MaxValue);
            float2 bMax = new float2(float.MinValue, float.MinValue);

            for (int i = 0; i < count; i++)
            {
                float2 mid = (m_Input[i].a + m_Input[i].b) * 0.5f;
                bMin = math.min(bMin, mid);
                bMax = math.max(bMax, mid);
            }

            // Guard against degenerate bbox (all segments at same point).
            float2 extent = bMax - bMin;
            if (extent.x < 0.001f) extent.x = 1f;
            if (extent.y < 0.001f) extent.y = 1f;

            // — Step 2: assign each segment to a grid cell ——————————————————
            // We want at most m_MaxOutput cells occupied.  Use a square grid
            // whose total cell count equals m_MaxOutput so the occupancy map fits
            // in a fixed NativeArray without dynamic allocation.
            //
            // GridRes = floor(sqrt(m_MaxOutput))  e.g. 256 → 16×16 = 256 cells
            int gridRes = (int)math.floor(math.sqrt((float)m_MaxOutput));
            if (gridRes < 1) gridRes = 1;
            int totalCells = gridRes * gridRes;

            // Occupancy: -1 = empty, ≥0 = index of the winning segment.
            var occupied = new NativeArray<int>(totalCells, Allocator.Temp);
            for (int c = 0; c < totalCells; c++) occupied[c] = -1;

            for (int i = 0; i < count; i++)
            {
                float2 mid = (m_Input[i].a + m_Input[i].b) * 0.5f;
                float2 norm = (mid - bMin) / extent; // 0..1
                int cx = math.clamp((int)(norm.x * gridRes), 0, gridRes - 1);
                int cy = math.clamp((int)(norm.y * gridRes), 0, gridRes - 1);
                int cell = cy * gridRes + cx;
                if (occupied[cell] == -1)
                    occupied[cell] = i; // first segment in cell wins
            }

            // — Step 3: collect one segment per occupied cell ————————————————
            for (int c = 0; c < totalCells; c++)
            {
                if (occupied[c] == -1) continue;
                m_Output.Add(m_Input[occupied[c]]);
                if (m_Output.Length >= m_MaxOutput) break;
            }

            occupied.Dispose();
        }
    }

    // =========================================================================
    //  Job 2 of 3 — ConvexHullJob  (replaces SortBoundaryByAngleJob)
    //
    //  Computes the convex hull of the BFS boundary point cloud using a
    //  Burst-safe Graham Scan. Overwrites m_Points in-place with the hull
    //  vertices in counter-clockwise order.
    //
    //  WHY convex hull instead of angle-sort?
    //  Angle-sorting only works cleanly for near-convex point sets. Real road
    //  networks are often concave (cul-de-sacs, dead ends, L-shapes) which
    //  causes the angle-sorted polygon to self-intersect in a starburst
    //  pattern. The convex hull is ALWAYS a valid non-self-intersecting
    //  polygon, regardless of network topology. For an isochrone the convex
    //  hull naturally represents the "maximum reachable area" boundary.
    //
    //  Time: O(n log n) for the sort + O(n) for the scan.
    //  Burst-JIT keeps it fast for the typical n <= 4096 BFS output.
    // =========================================================================
    [BurstCompile]
    public struct ConvexHullJob : IJob
    {
        // In-place: m_Points is overwritten with the CCW convex hull vertices.
        public NativeList<float3> m_Points; // world-space XYZ; only XZ is used

        public void Execute()
        {
            int n = m_Points.Length;
            if (n < 3) return; // trivial — leave as-is

            // -- Step 1: mirror all points into a temp XZ float2 array --------
            var pts = new NativeArray<float2>(n, Allocator.Temp);
            for (int i = 0; i < n; i++)
                pts[i] = new float2(m_Points[i].x, m_Points[i].z);

            // -- Step 2: find the pivot — lowest Z (Y in XZ), ties break on X -
            int pivotIdx = 0;
            for (int i = 1; i < n; i++)
            {
                if (pts[i].y < pts[pivotIdx].y ||
                    (pts[i].y == pts[pivotIdx].y && pts[i].x < pts[pivotIdx].x))
                    pivotIdx = i;
            }
            // Swap pivot to index 0
            float2 swp2 = pts[0];       pts[0]       = pts[pivotIdx];       pts[pivotIdx]       = swp2;
            float3 swp3 = m_Points[0];  m_Points[0]  = m_Points[pivotIdx];  m_Points[pivotIdx]  = swp3;
            float2 pivot = pts[0];

            // -- Step 3: sort indices 1..n-1 by CCW polar angle from pivot ----
            //   Use insertion sort — Burst-safe, no function pointers needed.
            for (int i = 2; i < n; i++)
            {
                float2 keyPt  = pts[i];
                float3 keyPt3 = m_Points[i];
                int j = i - 1;
                while (j >= 1)
                {
                    float cross = CrossZ(pivot, pts[j], keyPt);
                    if (cross > 0f) break;          // pts[j] is CCW of keyPt -> stop
                    if (cross == 0f)                // collinear -> keep the farther one
                    {
                        if (math.distancesq(pivot, pts[j]) >= math.distancesq(pivot, keyPt))
                            break;
                    }
                    pts[j + 1]      = pts[j];
                    m_Points[j + 1] = m_Points[j];
                    j--;
                }
                pts[j + 1]      = keyPt;
                m_Points[j + 1] = keyPt3;
            }

            // -- Step 4: Graham scan to build the convex hull -----------------
            var hull = new NativeList<int>(n, Allocator.Temp);
            hull.Add(0);
            hull.Add(1);

            for (int i = 2; i < n; i++)
            {
                while (hull.Length >= 2)
                {
                    int a = hull[hull.Length - 2];
                    int b = hull[hull.Length - 1];
                    float cross = CrossZ(pts[a], pts[b], pts[i]);
                    if (cross > 0f) break;                      // left turn -> keep b
                    hull.RemoveAt(hull.Length - 1);             // right/straight -> pop b
                }
                hull.Add(i);
            }

            // -- Step 5: overwrite m_Points with only the hull vertices -------
            var hullPts = new NativeArray<float3>(hull.Length, Allocator.Temp);
            for (int i = 0; i < hull.Length; i++)
                hullPts[i] = m_Points[hull[i]];

            m_Points.Clear();
            for (int i = 0; i < hullPts.Length; i++)
                m_Points.Add(hullPts[i]);

            hullPts.Dispose();
            hull.Dispose();
            pts.Dispose();
        }

        // 2D cross product of vectors (o->a) and (o->b).
        //  > 0 => CCW (left) turn
        //  < 0 => CW  (right) turn
        //  = 0 => collinear
        private static float CrossZ(float2 o, float2 a, float2 b)
        {
            float2 oa = a - o;
            float2 ob = b - o;
            return oa.x * ob.y - oa.y * ob.x;
        }
    }

    // =========================================================================
    //  Job 3 of 3 — DouglasPeuckerJob
    //
    //  Classic Ramer-Douglas-Peucker polyline simplification, adapted for a
    //  closed ring of world-space XZ points.  Runs entirely on NativeArrays
    //  using an explicit stack (NativeList used as a stack) — no recursion,
    //  fully Burst-compatible.
    //
    //  The epsilon threshold is automatically scaled to the spatial extent of
    //  the point set so the algorithm is resolution-independent: for a 1000m
    //  radius isochrone it tolerates ~2m deviation; for a 200m radius it
    //  tolerates ~0.4m.
    //
    //  After reduction, if the surviving vertex count still exceeds m_MaxOutput,
    //  a uniform stride sub-sample is applied as a final hard cap.
    //
    //  NOTE: With ConvexHullJob upstream the input is already a small convex
    //  hull (typically 6-20 vertices), so this job is usually a fast no-op
    //  that just copies the hull into m_Output.
    // =========================================================================
    [BurstCompile]
    public struct DouglasPeuckerJob : IJob
    {
        // — Inputs ————————————————————————————————————————————————————————————
        /// <summary>Hull vertices from ConvexHullJob.</summary>
        [ReadOnly] public NativeList<float3> m_Input;
        /// <summary>Maximum number of vertices in the output. Never exceeded.</summary>
        public int m_MaxOutput;
        /// <summary>
        /// Epsilon scale factor: the DP threshold = extent * m_EpsilonScale.
        /// 0.002 means 0.2% of the bounding diagonal — a good default.
        /// </summary>
        public float m_EpsilonScale;

        // — Output ————————————————————————————————————————————————————————————
        public NativeList<float3> m_Output; // pre-cleared by caller

        public void Execute()
        {
            int n = m_Input.Length;
            if (n == 0) return;
            if (n <= 2)
            {
                for (int i = 0; i < n; i++) m_Output.Add(m_Input[i]);
                return;
            }

            // Fast path: already within budget
            if (n <= m_MaxOutput)
            {
                for (int i = 0; i < n; i++) m_Output.Add(m_Input[i]);
                return;
            }

            // — Compute epsilon from bounding diagonal ————————————————————————
            float2 bMin = new float2(float.MaxValue,  float.MaxValue);
            float2 bMax = new float2(float.MinValue, float.MinValue);
            for (int i = 0; i < n; i++)
            {
                float2 p = new float2(m_Input[i].x, m_Input[i].z);
                bMin = math.min(bMin, p);
                bMax = math.max(bMax, p);
            }
            float diagonal  = math.length(bMax - bMin);
            float epsilon   = math.max(diagonal * m_EpsilonScale, 0.1f);
            float epsilonSq = epsilon * epsilon;

            // — Iterative Douglas-Peucker using an explicit segment stack ——————
            var keep  = new NativeArray<bool>(n, Allocator.Temp);
            var stack = new NativeList<int2>(n, Allocator.Temp);

            keep[0]     = true;
            keep[n - 1] = true;
            stack.Add(new int2(0, n - 1));

            while (stack.Length > 0)
            {
                int2 seg  = stack[stack.Length - 1];
                stack.RemoveAt(stack.Length - 1);

                int start = seg.x;
                int end   = seg.y;
                if (end - start < 2) continue;

                float2 a  = new float2(m_Input[start].x, m_Input[start].z);
                float2 b  = new float2(m_Input[end].x,   m_Input[end].z);
                float2 ab = b - a;
                float  abLenSq = math.dot(ab, ab);

                float maxDistSq = 0f;
                int   maxIdx    = start;

                for (int i = start + 1; i < end; i++)
                {
                    float2 p  = new float2(m_Input[i].x, m_Input[i].z);
                    float2 ap = p - a;

                    float distSq;
                    if (abLenSq < 1e-10f)
                    {
                        distSq = math.lengthsq(ap);
                    }
                    else
                    {
                        float  t    = math.clamp(math.dot(ap, ab) / abLenSq, 0f, 1f);
                        float2 proj = a + t * ab;
                        distSq = math.distancesq(p, proj);
                    }

                    if (distSq > maxDistSq)
                    {
                        maxDistSq = distSq;
                        maxIdx    = i;
                    }
                }

                if (maxDistSq > epsilonSq)
                {
                    keep[maxIdx] = true;
                    stack.Add(new int2(start, maxIdx));
                    stack.Add(new int2(maxIdx, end));
                }
            }

            // — Collect surviving points ——————————————————————————————————————
            int survivorCount = 0;
            for (int i = 0; i < n; i++)
                if (keep[i]) survivorCount++;

            if (survivorCount <= m_MaxOutput)
            {
                for (int i = 0; i < n; i++)
                    if (keep[i]) m_Output.Add(m_Input[i]);
            }
            else
            {
                // Survivors still exceed hard cap — uniform stride sub-sample.
                int stride = (int)math.ceil((float)survivorCount / m_MaxOutput);
                int taken  = 0;
                for (int i = 0; i < n && m_Output.Length < m_MaxOutput; i++)
                {
                    if (!keep[i]) continue;
                    if (taken % stride == 0) m_Output.Add(m_Input[i]);
                    taken++;
                }
                // Always include the last kept point to close the ring.
                if (m_Output.Length > 0)
                {
                    for (int i = n - 1; i >= 0; i--)
                    {
                        if (!keep[i]) continue;
                        float3 last = m_Input[i];
                        float3 cur  = m_Output[m_Output.Length - 1];
                        if (!last.Equals(cur)) m_Output.Add(last);
                        break;
                    }
                }
            }

            stack.Dispose();
            keep.Dispose();
        }
    }
}
