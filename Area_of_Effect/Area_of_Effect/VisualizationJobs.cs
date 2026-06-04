using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Game.Net;
using Game.Prefabs;

namespace Area_of_Effect
{
    // =========================================================================
    //  Job 1 - CircleWithRoads
    //  Parallel IJobChunk. Keeps road segments whose bezier midpoint is within
    //  the query radius. Output is capped via AddNoResize.
    // =========================================================================
    [BurstCompile]
    public struct FindRoadsInRadiusJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<Edge>     m_EdgeType;
        [ReadOnly] public ComponentTypeHandle<Curve>    m_CurveType;
        [ReadOnly] public ComponentLookup<NodeGeometry> m_NodeGeomLookup;

        public float3 m_Origin;
        public float  m_RadiusSq;

        [WriteOnly] public NativeList<RoadSegmentItem>.ParallelWriter m_ResultWriter;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                            bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var edges  = chunk.GetNativeArray(ref m_EdgeType);
            var curves = chunk.GetNativeArray(ref m_CurveType);

            for (int i = 0; i < chunk.Count; i++)
            {
                float3 mid = curves[i].m_Bezier.d * 0.125f
                           + curves[i].m_Bezier.c * 0.375f
                           + curves[i].m_Bezier.b * 0.375f
                           + curves[i].m_Bezier.a * 0.125f;

                float distSq = math.distancesq(new float3(m_Origin.x, mid.y, m_Origin.z), mid);
                if (distSq > m_RadiusSq) continue;

                m_ResultWriter.AddNoResize(new RoadSegmentItem
                {
                    a = new float2(curves[i].m_Bezier.a.x, curves[i].m_Bezier.a.z),
                    b = new float2(curves[i].m_Bezier.d.x, curves[i].m_Bezier.d.z)
                });
            }
        }
    }

    // =========================================================================
    //  GatherRoadCurvesJob
    //  Parallel IJobChunk that gathers full 3D curves within the service radius.
    // =========================================================================
    [BurstCompile]
    public struct GatherRoadCurvesJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<Curve> m_CurveType;

        public float3 m_Origin;
        public float  m_RadiusSq;

        [WriteOnly] public NativeList<Colossal.Mathematics.Bezier4x3>.ParallelWriter m_ResultWriter;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                            bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var curves = chunk.GetNativeArray(ref m_CurveType);

            for (int i = 0; i < chunk.Count; i++)
            {
                var curve = curves[i];
                float3 mid = curve.m_Bezier.d * 0.125f
                           + curve.m_Bezier.c * 0.375f
                           + curve.m_Bezier.b * 0.375f
                           + curve.m_Bezier.a * 0.125f;

                float distSq = math.distancesq(new float3(m_Origin.x, mid.y, m_Origin.z), mid);
                if (distSq > m_RadiusSq) continue;

                m_ResultWriter.AddNoResize(curve.m_Bezier);
            }
        }
    }

    // =========================================================================
    //  Job 2 - ExactPolygon (Isochrone BFS)
    //
    //  Single-threaded IJob. Hard caps prevent OS-level hang on large radii.
    //  BFS_BUDGET_CAP:     max metres of road BFS may explore.
    //  BFS_NODE_CAP:       max nodes visited during BFS traversal.
    //  BOUNDARY_SAFETY_CAP: max total edge-checks during boundary extraction
    //    (stops the O(N*E) stall when visited set is dense).
    // =========================================================================
    [BurstCompile]
    public struct IsochroneBFSJob : IJob
    {
        [ReadOnly] public BufferLookup<ConnectedEdge>   m_ConnectedEdgeLookup;
        [ReadOnly] public ComponentLookup<NodeGeometry> m_NodeGeomLookup;
        [ReadOnly] public ComponentLookup<Edge>         m_EdgeLookup;
        [ReadOnly] public ComponentLookup<Curve>        m_CurveLookup;
        [ReadOnly] public ComponentLookup<Road>         m_RoadLookup;

        public Entity m_StartNode;
        public float  m_TravelBudget;

        [WriteOnly] public NativeList<float3> m_BoundaryWorldPoints;

        private const float BFS_BUDGET_CAP       = 1500f;
        private const int   BFS_NODE_CAP         = 4096;
        private const int   BOUNDARY_SAFETY_CAP  = 4096;

        public void Execute()
        {
            if (m_StartNode == Entity.Null ||
                !m_NodeGeomLookup.HasComponent(m_StartNode) ||
                !m_ConnectedEdgeLookup.HasBuffer(m_StartNode))
                return;

            float budget = math.min(m_TravelBudget, BFS_BUDGET_CAP);

            var queue   = new NativeQueue<BFSNode>(Allocator.Temp);
            var visited = new NativeHashMap<Entity, float>(512, Allocator.Temp);

            queue.Enqueue(new BFSNode { entity = m_StartNode, cost = 0f });
            visited.TryAdd(m_StartNode, 0f);

            // -- BFS traversal -----------------------------------------------
            int iterations = 0;
            while (queue.Count > 0 && ++iterations < 5000)
            {
                if (visited.Count >= BFS_NODE_CAP) break;

                BFSNode current = queue.Dequeue();
                if (!m_ConnectedEdgeLookup.HasBuffer(current.entity)) continue;

                var edges = m_ConnectedEdgeLookup[current.entity];
                for (int i = 0; i < edges.Length; i++)
                {
                    Entity edgeEntity = edges[i].m_Edge;
                    if (!m_EdgeLookup.HasComponent(edgeEntity) ||
                        !m_CurveLookup.HasComponent(edgeEntity)) continue;
                    if (!m_RoadLookup.HasComponent(edgeEntity)) continue;

                    Edge  edge    = m_EdgeLookup[edgeEntity];
                    Curve curve   = m_CurveLookup[edgeEntity];
                    float segLen  = math.length(curve.m_Bezier.d - curve.m_Bezier.a);
                    float newCost = current.cost + segLen;
                    if (newCost > budget) continue;

                    Entity nextNode = (edge.m_Start == current.entity) ? edge.m_End : edge.m_Start;
                    if (!m_NodeGeomLookup.HasComponent(nextNode)) continue;

                    if (visited.TryGetValue(nextNode, out float existingCost))
                    {
                        if (existingCost <= newCost) continue;
                        visited[nextNode] = newCost;
                    }
                    else
                    {
                        visited.TryAdd(nextNode, newCost);
                    }
                    queue.Enqueue(new BFSNode { entity = nextNode, cost = newCost });
                }
            }

            // -- Boundary extraction -----------------------------------------
            // safetyCounter accumulates across ALL edge-checks in this phase.
            // Exceeding BOUNDARY_SAFETY_CAP exits immediately, outputting
            // whatever boundary nodes have been collected so far.
            int safetyCounter = 0;

            var keys = visited.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                if (safetyCounter > BOUNDARY_SAFETY_CAP) break;

                Entity node       = keys[i];
                bool   isBoundary = false;

                if (m_ConnectedEdgeLookup.HasBuffer(node))
                {
                    var edges = m_ConnectedEdgeLookup[node];
                    for (int j = 0; j < edges.Length; j++)
                    {
                        if (++safetyCounter > BOUNDARY_SAFETY_CAP) break;

                        Entity edgeEntity = edges[j].m_Edge;
                        if (!m_EdgeLookup.HasComponent(edgeEntity) ||
                            !m_CurveLookup.HasComponent(edgeEntity)) continue;

                        Edge   edge    = m_EdgeLookup[edgeEntity];
                        float  segLen  = math.length(m_CurveLookup[edgeEntity].m_Bezier.d
                                                   - m_CurveLookup[edgeEntity].m_Bezier.a);
                        Entity other   = (edge.m_Start == node) ? edge.m_End : edge.m_Start;

                        if (!visited.ContainsKey(other) || visited[node] + segLen > budget)
                        {
                            isBoundary = true;
                            break;
                        }
                    }
                }

                if (isBoundary && m_NodeGeomLookup.HasComponent(node))
                {
                    var b = m_NodeGeomLookup[node].m_Bounds;
                    m_BoundaryWorldPoints.Add((b.min + b.max) * 0.5f);
                }
            }

            keys.Dispose();
            visited.Dispose();
            queue.Dispose();
        }

        private struct BFSNode { public Entity entity; public float cost; }
    }

    // =========================================================================
    //  Job 3 - CachedHeatmap (BuildHeatmapJob)
    //
    //  Single-threaded Burst IJob.
    //  Traverses the network using BFS starting from m_StartNode, so we only
    //  process local road nodes near the building and do not iterate the entire
    //  city's road network.
    // =========================================================================
    [BurstCompile]
    public struct BuildHeatmapJob : IJob
    {
        [ReadOnly] public BufferLookup<ConnectedEdge>   m_ConnectedEdgeLookup;
        [ReadOnly] public ComponentLookup<NodeGeometry> m_NodeGeomLookup;
        [ReadOnly] public ComponentLookup<Edge>         m_EdgeLookup;
        [ReadOnly] public ComponentLookup<Curve>        m_CurveLookup;
        [ReadOnly] public ComponentLookup<Road>         m_RoadLookup;

        public Entity m_StartNode;
        public float3 m_Origin;
        public float  m_RadiusSq;

        [WriteOnly] public NativeList<HeatmapNodeItem> m_Result;

        public const int   HEATMAP_NODE_CAP   = 512;
        public const float HEATMAP_RADIUS_CAP = 1500f;

        public void Execute()
        {
            if (m_StartNode == Entity.Null ||
                !m_NodeGeomLookup.HasComponent(m_StartNode) ||
                !m_ConnectedEdgeLookup.HasBuffer(m_StartNode))
                return;

            float budget = math.sqrt(m_RadiusSq);

            var queue   = new NativeQueue<BFSNode>(Allocator.Temp);
            var visited = new NativeHashMap<Entity, float>(512, Allocator.Temp);

            queue.Enqueue(new BFSNode { entity = m_StartNode, cost = 0f });
            visited.TryAdd(m_StartNode, 0f);

            // -- BFS traversal -----------------------------------------------
            int iterations = 0;
            while (queue.Count > 0 && ++iterations < 5000)
            {
                if (visited.Count >= HEATMAP_NODE_CAP) break;

                BFSNode current = queue.Dequeue();
                if (!m_ConnectedEdgeLookup.HasBuffer(current.entity)) continue;

                var edges = m_ConnectedEdgeLookup[current.entity];
                for (int i = 0; i < edges.Length; i++)
                {
                    Entity edgeEntity = edges[i].m_Edge;
                    if (!m_EdgeLookup.HasComponent(edgeEntity) ||
                        !m_CurveLookup.HasComponent(edgeEntity)) continue;
                    if (!m_RoadLookup.HasComponent(edgeEntity)) continue;

                    Edge  edge    = m_EdgeLookup[edgeEntity];
                    Curve curve   = m_CurveLookup[edgeEntity];
                    float segLen  = math.length(curve.m_Bezier.d - curve.m_Bezier.a);
                    float newCost = current.cost + segLen;
                    if (newCost > budget) continue;

                    Entity nextNode = (edge.m_Start == current.entity) ? edge.m_End : edge.m_Start;
                    if (!m_NodeGeomLookup.HasComponent(nextNode)) continue;

                    if (visited.TryGetValue(nextNode, out float existingCost))
                    {
                        if (existingCost <= newCost) continue;
                        visited[nextNode] = newCost;
                    }
                    else
                    {
                        visited.TryAdd(nextNode, newCost);
                    }
                    queue.Enqueue(new BFSNode { entity = nextNode, cost = newCost });
                }
            }

            int safetyCounter = 0;
            var keys = visited.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < keys.Length; i++)
            {
                Entity node = keys[i];
                if (!m_NodeGeomLookup.HasComponent(node)) continue;

                var bounds = m_NodeGeomLookup[node].m_Bounds;
                float3 nodePos = (bounds.min + bounds.max) * 0.5f;

                // Spatial filter: only process road nodes where distancesq < radiusSquared
                float dSq = math.distancesq(new float3(m_Origin.x, nodePos.y, m_Origin.z), nodePos);
                if (dSq > m_RadiusSq) continue;

                // Hard cap: break and output current results cleanly.
                if (++safetyCounter > HEATMAP_NODE_CAP) break;

                float dist   = math.sqrt(dSq);
                float weight = math.saturate(1f - dist / budget);

                m_Result.Add(new HeatmapNodeItem
                {
                    worldXZ = new float2(nodePos.x, nodePos.z),
                    weight  = weight
                });
            }

            keys.Dispose();
            visited.Dispose();
            queue.Dispose();
        }

        private struct BFSNode { public Entity entity; public float cost; }
    }

    /// <summary>Blittable output of BuildHeatmapJob - world XZ + weight before screen projection.</summary>
    public struct HeatmapNodeItem
    {
        public float2 worldXZ;
        public float  weight;
    }
}
