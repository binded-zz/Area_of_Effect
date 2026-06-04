using System.Collections.Generic;
using Colossal.UI.Binding;
using Unity.Collections;
using Unity.Mathematics;

namespace Area_of_Effect
{
    // ?????????????????????????????????????????????????????????????????????????
    //  Hard vertex / segment caps � the single source of truth.
    //  These values are deliberately conservative: each JSON point is ~30 bytes
    //  so 256 points ? 7.7 KB, well inside Coherent Gameface's safe range.
    //  The caps are used by both the decimation jobs and the serializers so
    //  there is no way to accidentally bypass them on one path.
    // ?????????????????????????????????????????????????????????????????????????
    public static class PayloadLimits
    {
        /// <summary>Maximum road segments sent for CircleWithRoads mode.</summary>
        public const int MaxRoadSegments = 256;

        /// <summary>Maximum polygon vertices sent for ExactPolygon mode.</summary>
        public const int MaxPolygonVerts = 256;

        /// <summary>
        /// Initial capacity for the raw job output NativeList for road segments.
        /// Set to 2� the cap so the parallel writer has meaningful headroom before
        /// the decimation job reduces it to MaxRoadSegments.
        /// The list is also used as the ParallelWriter target, so its capacity
        /// must be set before scheduling the job � see VisualizationDispatcherSystem.
        /// </summary>
        public const int RawRoadListCapacity = MaxRoadSegments * 8;

        /// <summary>Initial capacity for the raw BFS boundary world-point list.</summary>
        public const int RawBFSCapacity = 4096;
    }
    // ?????????????????????????????????????????????????????????????????????????
    //  Unified JSON payload sent to the TypeScript UI.
    //
    //  EXACT variable name contract (TypeScript must read these names verbatim):
    //    "mode"     � one of "CircleOnly" | "CircleWithRoads" | "ExactPolygon" | "CachedHeatmap"
    //    "origin"   � { "x": float, "y": float } world-space XZ projected to normalised screen
    //    "mode"      one of "CircleOnly" | "CircleWithRoads" | "ExactPolygon" | "CachedHeatmap"
    //    "origin"    { "x": float, "y": float } world-space XZ projected to normalised screen
    //    "radius"    service radius in world units (metres)
    //    "pathData"  mode-dependent payload:
    //                   CircleOnly      ? empty string ""
    //                   CircleWithRoads ? JSON array of { "ax","ay","bx","by" } road segment endpoints
    //                   ExactPolygon    ? JSON array of { "x","y" } polygon vertices (screen %)
    //                   CachedHeatmap   ? JSON array of { "x","y","w" } coverage node points + weight
    // ?????????????????????????????????????????????????????????????????????????
    public struct VisualizationPayload : IJsonWritable
    {
        public string mode;
        public float originX;   // screen-space 0..100 (%)
        public float originY;   // screen-space 0..100 (%)
        public float radius;    // world metres
        public string pathData; // pre-serialized JSON string or ""
        public string color;    // hex color
        public float opacity;   // 0.0f..1.0f

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin("VisualizationPayload");

            writer.PropertyName("mode");
            writer.Write(mode ?? "CircleOnly");

            writer.PropertyName("originX");
            writer.Write(originX);

            writer.PropertyName("originY");
            writer.Write(originY);

            writer.PropertyName("radius");
            writer.Write(radius);

            writer.PropertyName("pathData");
            writer.Write(pathData ?? "");

            writer.PropertyName("color");
            writer.Write(color ?? "#00c8ff");

            writer.PropertyName("opacity");
            writer.Write(opacity);

            writer.TypeEnd();
        }
    }

    // =========================================================================
    //  Blittable structs used by Burst jobs — kept here alongside the payload
    //  so types/bindings stay in one place.
    // =========================================================================

    /// <summary>One road-segment endpoint pair for CircleWithRoads mode.</summary>
    public struct RoadSegmentItem
    {
        public float2 a; // world XZ
        public float2 b; // world XZ
    }

    /// <summary>One coverage node + weight for CachedHeatmap mode.</summary>
    public struct HeatmapNode
    {
        public float2 screenPct; // 0..100 normalised screen coordinates
        public float weight;     // 0..1 normalised coverage intensity
    }

    // =========================================================================
    //  Static helpers for hand-rolling JSON without Newtonsoft.
    //  Every overload accepts an explicit 'maxCount' parameter and enforces it
    //  as a hard ceiling so malformed or unexpectedly large lists are safe.
    // =========================================================================
    internal static class VisualizationSerializer
    {
        internal static string SerializeRoadSegments(
            NativeList<RoadSegmentItem> segments,
            int maxCount = PayloadLimits.MaxRoadSegments)
        {
            int count = System.Math.Min(segments.Length, maxCount);
            if (count == 0) return "[]";
            var sb = new System.Text.StringBuilder(count * 60);
            sb.Append('[');
            for (int i = 0; i < count; i++)
            {
                var s = segments[i];
                sb.Append("{\"a\":{\"x\":");
                sb.Append(s.a.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"y\":");
                sb.Append(s.a.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append("},\"b\":{\"x\":");
                sb.Append(s.b.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"y\":");
                sb.Append(s.b.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(i < count - 1 ? "}}," : "}}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        internal static string SerializePolygon(
            NativeList<float3> verts,
            int maxCount = PayloadLimits.MaxPolygonVerts)
        {
            int count = System.Math.Min(verts.Length, maxCount);
            if (count == 0) return "[]";
            var sb = new System.Text.StringBuilder(count * 35);
            sb.Append('[');
            for (int i = 0; i < count; i++)
            {
                var v = verts[i];
                sb.Append("{\"x\":");
                sb.Append(v.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"z\":");
                sb.Append(v.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(i < count - 1 ? "}," : "}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        internal static string SerializeHeatmap(
            List<HeatmapNode> nodes,
            int maxCount = BuildHeatmapJob.HEATMAP_NODE_CAP)
        {
            if (nodes == null || nodes.Count == 0) return "[]";
            int count = System.Math.Min(nodes.Count, maxCount);
            var sb = new System.Text.StringBuilder(count * 50);
            sb.Append('[');
            for (int i = 0; i < count; i++)
            {
                var n = nodes[i];
                sb.Append("{\"screenPct\":{\"x\":");
                sb.Append(n.screenPct.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"y\":");
                sb.Append(n.screenPct.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append("},\"w\":");
                sb.Append(n.weight.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(i < count - 1 ? "}," : "}");
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
