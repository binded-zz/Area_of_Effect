using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Transform = Game.Objects.Transform;

namespace Area_of_Effect
{
    // =========================================================================
    //  VisualizationDispatcherSystem
    //
    //  Reads the active VisualizationMode from the UI binding, resolves the
    //  selected / ghost entity, schedules the appropriate Burst jobs, and writes
    //  a unified VisualizationPayload to the "vizPayload" RawValueBinding.
    //
    //  Mode contract (must match C# VisualizationMode enum AND TS VizMode union):
    //    -1  hidden    — nothing selected, overlay blank
    //     0  CircleOnly
    //     1  CircleWithRoads
    //     2  ExactPolygon
    //     3  CachedHeatmap
    //
    //  m_ForceUpdate flag:
    //    Set to true by the setVizMode trigger so OnUpdate bypasses all
    //    entity-change / position-threshold caches and forces an immediate
    //    full recalculation on the very next frame.  Cleared after dispatch.
    // =========================================================================
    public partial class VisualizationDispatcherSystem : UISystemBase
    {
        // == ECS =============================================================
        private ToolSystem           m_ToolSystem;
        private AreaOfEffectUISystem m_UISystem;
        private CameraUpdateSystem   m_CameraUpdateSystem;
        private OverlayRenderSystem  m_OverlayRenderSystem;
        private EntityQuery          m_RoadSegmentQuery;
        private EntityQuery          m_TempBuildingQuery;
        private EntityQuery          m_NodeGeomQuery;
        private EntityQuery          m_RoadNodeQuery;

        // == Bindings ========================================================
        private ValueBinding<int> m_ModeBinding;
        private RawValueBinding   m_PayloadBinding;
        private ValueBinding<bool> m_ShowOnHoverBinding;
        private ValueBinding<bool> m_EnablePreplacementBinding;

        // == Mode state ======================================================
        // m_ActiveMode mirrors what the UI last set.  -1 means "off" (user
        // clicked the already-active button to deselect it).
        private int                  m_ActiveMode         = 0;    // 0 = CircleOnly on first load
        private bool                 m_ShowOnHover        = true; // auto-show SVG ring on ghost hover
        public bool                  ShowOnHover          { get { return m_ShowOnHover; } }
        private bool                 m_EnablePreplacement = true; // show terrain ring during placement
        public bool                  EnablePreplacement   { get { return m_EnablePreplacement; } }
        private VisualizationPayload m_CurrentPayload     = new VisualizationPayload
        {
            mode = "hidden", originX = 50f, originY = 50f, radius = 0f, pathData = "", color = "#00c8ff", opacity = 0.55f
        };

        // == Persistent native containers ====================================
        private NativeList<float3>                     m_BFSWorldPoints;
        private NativeList<float3>                     m_DecimatedPoints;
        private NativeList<RoadSegmentItem>            m_RoadSegments;
        private NativeList<RoadSegmentItem>            m_DecimatedSegments;
        private NativeList<HeatmapNodeItem>            m_HeatmapRaw;
        private NativeList<Colossal.Mathematics.Bezier4x3> m_ActiveRoadCurves;

        // == Per-frame job tracking ===========================================
        private Entity            m_LastBFSEntity     = Entity.Null;
        private Entity            m_LastLoggedEntity  = Entity.Null;
        private int               m_LastDispatchMode  = -99;  // sentinel — never matches
        private JobHandle         m_PendingJob;
        private bool              m_JobPending        = false;
        private float3            m_LastDispatchOrigin = new float3(float.MaxValue, 0f, float.MaxValue);

        private bool m_ForceUpdate = false;
        private int  m_PayloadCooldown = 0;

        // == Caching for Nearest Node Search =================================
        private float3 m_LastSearchOrigin = new float3(float.MaxValue, 0f, float.MaxValue);
        private Entity m_CachedStartNode  = Entity.Null;

        [Unity.Burst.BurstCompile]
        private struct FindNearestNodeJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> m_Entities;
            [ReadOnly] public ComponentLookup<NodeGeometry> m_NodeGeomLookup;
            [ReadOnly] public BufferLookup<ConnectedEdge> m_ConnectedEdgeLookup;
            [ReadOnly] public ComponentLookup<Road> m_RoadLookup;
            public float3 m_Origin;
            public float m_RangeSq;
            public NativeArray<Entity> m_Result;

            public void Execute()
            {
                Entity best = Entity.Null;
                float bestDist = float.MaxValue;
                for (int i = 0; i < m_Entities.Length; i++)
                {
                    Entity ent = m_Entities[i];
                    if (!m_NodeGeomLookup.HasComponent(ent)) continue;
                    if (!m_ConnectedEdgeLookup.HasBuffer(ent)) continue;

                    var bounds = m_NodeGeomLookup[ent].m_Bounds;
                    float3 nodePos = (bounds.min + bounds.max) * 0.5f;
                    float dSq = math.distancesq(new float3(m_Origin.x, nodePos.y, m_Origin.z), nodePos);
                    if (dSq < m_RangeSq && dSq < bestDist)
                    {
                        var edges = m_ConnectedEdgeLookup[ent];
                        bool hasRoad = false;
                        for (int j = 0; j < edges.Length; j++)
                        {
                            if (m_RoadLookup.HasComponent(edges[j].m_Edge))
                            {
                                hasRoad = true;
                                break;
                            }
                        }
                        if (hasRoad)
                        {
                            bestDist = dSq;
                            best = ent;
                        }
                    }
                }
                m_Result[0] = best;
            }
        }

        // =====================================================================
        protected override void OnCreate()
        {
            UnityEngine.Debug.Log("[AoE] System Created and Running");
            try
            {
                base.OnCreate();

                UnityEngine.Debug.Log("VisualizationDispatcherSystem OnCreate starting.");

                m_ToolSystem         = World.GetOrCreateSystemManaged<ToolSystem>();
                m_UISystem           = World.GetOrCreateSystemManaged<AreaOfEffectUISystem>();
                m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
                m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

                // Road segments for CircleWithRoads.
                m_RoadSegmentQuery = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Edge>(),
                        ComponentType.ReadOnly<Curve>(),
                        ComponentType.ReadOnly<Road>(),
                    }
                });

                // Temp ghosts: ONLY Temp + PrefabRef required on the ghost entity.
                m_TempBuildingQuery = GetEntityQuery(new ComponentType[] {
                    ComponentType.ReadOnly<Game.Tools.Temp>(),
                    ComponentType.ReadOnly<Game.Prefabs.PrefabRef>()
                });

                // NodeGeometry chunks for BuildHeatmapJob.
                m_NodeGeomQuery = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { ComponentType.ReadOnly<NodeGeometry>() }
                });

                m_RoadNodeQuery = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Node>(),
                        ComponentType.ReadOnly<NodeGeometry>(),
                        ComponentType.ReadOnly<ConnectedEdge>()
                    }
                });

                // Native containers — allocated once, reused every frame.
                m_BFSWorldPoints    = new NativeList<float3>(PayloadLimits.RawBFSCapacity,               Allocator.Persistent);
                m_DecimatedPoints   = new NativeList<float3>(PayloadLimits.MaxPolygonVerts + 4,          Allocator.Persistent);
                m_RoadSegments      = new NativeList<RoadSegmentItem>(PayloadLimits.RawRoadListCapacity,  Allocator.Persistent);
                m_DecimatedSegments = new NativeList<RoadSegmentItem>(PayloadLimits.MaxRoadSegments + 4,  Allocator.Persistent);
                m_HeatmapRaw        = new NativeList<HeatmapNodeItem>(BuildHeatmapJob.HEATMAP_NODE_CAP,   Allocator.Persistent);
                m_ActiveRoadCurves  = new NativeList<Colossal.Mathematics.Bezier4x3>(PayloadLimits.RawRoadListCapacity, Allocator.Persistent);

                m_ActiveMode = Mod.Settings != null ? Mod.Settings.ActiveMode : 0;
                m_ShowOnHover = Mod.Settings != null ? Mod.Settings.ShowOnHover : true;
                m_EnablePreplacement = Mod.Settings != null ? Mod.Settings.EnablePreplacement : true;

                // vizMode: UI reads this to show which button is active.
                AddBinding(m_ModeBinding = new ValueBinding<int>(
                    "area_of_effect", "vizMode", m_ActiveMode));

                // vizPayload: raw JSON written by WritePayload callback.
                AddBinding(m_PayloadBinding = new RawValueBinding(
                    "area_of_effect", "vizPayload", WritePayload));

                // setVizMode trigger: UI sends int index (0-3) or -1 to turn off.
                AddBinding(new TriggerBinding<int>("area_of_effect", "setVizMode", (modeInt) =>
                {
                    int clamped = (modeInt < 0) ? -1 : Mathf.Clamp(modeInt, 0, 3);
                    UnityEngine.Debug.Log($"setVizMode triggered. UI sent: {modeInt}, clamped: {clamped}, current active: {m_ActiveMode}");

                    if (clamped == m_ActiveMode)
                    {
                        // If the user clicks the currently active mode, do nothing (do not toggle off).
                        UnityEngine.Debug.Log($"setVizMode: Clicked active mode {m_ActiveMode}, doing nothing.");
                        return;
                    }

                    m_ActiveMode = clamped;
                    UnityEngine.Debug.Log($"setVizMode: Set active mode to {m_ActiveMode}.");

                    if (Mod.Settings != null)
                    {
                        Mod.Settings.ActiveMode = m_ActiveMode;
                        Mod.Settings.ApplyAndSave();
                    }

                    m_ModeBinding.Update(m_ActiveMode);

                    // Force a full recalculation next frame regardless of cache state.
                    m_ForceUpdate      = true;
                    m_LastBFSEntity    = Entity.Null;
                    m_LastDispatchMode = -99;

                    // If turned off, push empty payload immediately — no need to wait for OnUpdate.
                    if (m_ActiveMode == -1)
                    {
                        UnityEngine.Debug.Log("setVizMode: Pushing empty payload immediately.");
                        PushEmptyPayload();
                        m_ForceUpdate = false;
                    }
                }));

                AddBinding(m_ShowOnHoverBinding = new ValueBinding<bool>(
                    "area_of_effect", "showOnHover", m_ShowOnHover));

                AddBinding(new TriggerBinding<bool>("area_of_effect", "setShowOnHover", (enabled) =>
                {
                    m_ShowOnHover = enabled;
                    m_ShowOnHoverBinding.Update(m_ShowOnHover);
                    if (Mod.Settings != null)
                    {
                        Mod.Settings.ShowOnHover = enabled;
                        Mod.Settings.ApplyAndSave();
                    }
                    if (!m_ShowOnHover)
                    {
                        m_ForceUpdate = true; // force clear if we were showing it
                    }
                }));

                // Separate toggle: enables/disables the terrain ring drawn by AreaOfEffectSystem
                // during building placement. Completely independent from showOnHover.
                AddBinding(m_EnablePreplacementBinding = new ValueBinding<bool>(
                    "area_of_effect", "enablePreplacement", m_EnablePreplacement));

                AddBinding(new TriggerBinding<bool>("area_of_effect", "setEnablePreplacement", (enabled) =>
                {
                    m_EnablePreplacement = enabled;
                    m_EnablePreplacementBinding.Update(m_EnablePreplacement);
                    if (Mod.Settings != null)
                    {
                        Mod.Settings.EnablePreplacement = enabled;
                        Mod.Settings.ApplyAndSave();
                    }
                }));

                UnityEngine.Debug.Log("VisualizationDispatcherSystem OnCreate completed successfully.");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Failed to initialize VisualizationDispatcherSystem in OnCreate: " + ex);
            }
        }

        protected override void OnDestroy()
        {
            UnityEngine.Debug.Log("VisualizationDispatcherSystem OnDestroy starting.");
            if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }
            if (m_BFSWorldPoints.IsCreated)    m_BFSWorldPoints.Dispose();
            if (m_DecimatedPoints.IsCreated)   m_DecimatedPoints.Dispose();
            if (m_RoadSegments.IsCreated)      m_RoadSegments.Dispose();
            if (m_DecimatedSegments.IsCreated) m_DecimatedSegments.Dispose();
            if (m_HeatmapRaw.IsCreated)        m_HeatmapRaw.Dispose();
            if (m_ActiveRoadCurves.IsCreated)  m_ActiveRoadCurves.Dispose();
            base.OnDestroy();
            UnityEngine.Debug.Log("VisualizationDispatcherSystem OnDestroy completed.");
        }

        // =====================================================================
        protected override void OnUpdate()
        {
            if (GameManager.instance.isGameLoading) return;

            if (Mod.Settings != null)
            {
                if (m_ShowOnHover != Mod.Settings.ShowOnHover)
                {
                    m_ShowOnHover = Mod.Settings.ShowOnHover;
                    m_ShowOnHoverBinding.Update(m_ShowOnHover);
                    if (!m_ShowOnHover) m_ForceUpdate = true;
                }
                if (m_EnablePreplacement != Mod.Settings.EnablePreplacement)
                {
                    m_EnablePreplacement = Mod.Settings.EnablePreplacement;
                    m_EnablePreplacementBinding.Update(m_EnablePreplacement);
                }
            }

            if (m_ActiveMode == -1) {
                PushEmptyPayload();
                return;
            }

            Entity selected = Entity.Null;

            // Pre-placement path: TempFlags.Create ghosts (build mode) — EnablePreplacement only.
            if (m_EnablePreplacement)
                selected = ResolveGhostEntity();

            // Hover path: TempFlags.Select highlights (mouse over existing building) — ShowOnHover only.
            if (selected == Entity.Null && m_ShowOnHover)
                selected = ResolveHoverEntity();

            // Explicit selection path: already-placed building the player clicked.
            // Always active — no toggle needed, this is the core mod function.
            if (selected == Entity.Null)
                selected = m_ToolSystem.selected;

            selected = GetRootBuilding(selected);

            if (selected != m_LastLoggedEntity)
            {
                m_LastLoggedEntity = selected;
                if (selected != Entity.Null && EntityManager.Exists(selected))
                {
                    string nameStr = "Unknown Building";
                    try
                    {
                        var nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
                        if (nameSystem != null) nameStr = nameSystem.GetRenderedLabelName(selected);
                    }
                    catch {}
                    UnityEngine.Debug.Log($"[AoE] Selected/Hovered Entity changed to: {selected.Index}:{selected.Version} (Name: {nameStr})");
                }
                else
                {
                    UnityEngine.Debug.Log("[AoE] Selected/Hovered Entity changed to: Null");
                }
            }

            if (selected == Entity.Null || !EntityManager.Exists(selected)) {
                PushEmptyPayload();
                return;
            }

            // ?? 2. Service radius ?????????????????????????????????????????????
            float radius = ResolveServiceRadius(selected);
            if (radius <= 0f)
            {
                PushEmptyPayload();
                return;
            }

            if (!EntityManager.HasComponent<Transform>(selected))
            {
                PushEmptyPayload();
                return;
            }
            Transform xform       = EntityManager.GetComponentData<Transform>(selected);
            float3    worldOrigin = xform.m_Position;

            // Cap visual radius
            float visualRadius = math.min(radius, 1500f);

            // 3. Project origin to screen % (clamped for Cohtml safety)
            const float kPctMin = -500f;
            const float kPctMax =  600f;
            Camera cam = m_CameraUpdateSystem.activeCamera;
            float originSX = 50f, originSY = 50f;

            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(worldOrigin);
                if (vp.z > 0f && !float.IsNaN(vp.x) && !float.IsInfinity(vp.x) && !float.IsNaN(vp.y) && !float.IsInfinity(vp.y))
                {
                    originSX = math.clamp(vp.x * 100f, kPctMin, kPctMax);
                    originSY = math.clamp((1f - vp.y) * 100f, kPctMin, kPctMax);
                }
            }
            // Do not project radius to UI pixels — rely exclusively on native C# 3D radii.
            float payloadRadius = visualRadius;

            ResolveColorAndOpacity(selected, out string resolvedColor, out float resolvedOpacity);
            if (radius > 1500f)
            {
                // Dampen the opacity for extra-large ranges so they don't block out the map view.
                float dampening = 1500f / radius;
                resolvedOpacity *= dampening;
            }

            float threshold = GetModePositionThreshold(m_ActiveMode);
            bool entityChanged = (selected != m_LastBFSEntity) || m_ForceUpdate;
            bool modeChanged   = (m_ActiveMode != m_LastDispatchMode) || m_ForceUpdate;

            if (HasMovedBeyondThreshold(worldOrigin, threshold))
            {
                entityChanged = true;
            }

            // ?? 5. Mode dispatch ??????????????????????????????????????????????
            switch (m_ActiveMode)
            {
                // ?? Mode 0: CircleOnly ?????????????????????????????????????????
                case 0:
                {
                    PushPayload(new VisualizationPayload
                    {
                        mode     = "CircleOnly",
                        originX  = originSX,
                        originY  = originSY,
                        radius   = 0f, // do NOT send radius to the TSX payload
                        pathData = "",
                        color    = resolvedColor,
                        opacity  = resolvedOpacity
                    });

                    // Draw native 3D circle directly in the game's engine
                    UnityEngine.Color parsedColor = UnityEngine.Color.cyan;
                    if (!string.IsNullOrEmpty(resolvedColor))
                    {
                        UnityEngine.ColorUtility.TryParseHtmlString(resolvedColor, out parsedColor);
                    }

                    // Get buffer and draw native circle conforming to terrain/water 3D space
                    OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out Unity.Jobs.JobHandle dependencies);
                    UnityEngine.Color nativeColor = new UnityEngine.Color(parsedColor.r, parsedColor.g, parsedColor.b, resolvedOpacity);
                    buffer.DrawCircle(nativeColor, worldOrigin, radius);
                    m_OverlayRenderSystem.AddBufferWriter(dependencies);

                    m_LastBFSEntity      = selected;
                    m_LastDispatchMode   = 0;
                    m_LastDispatchOrigin = worldOrigin;
                    break;
                }

                // ?? Mode 1: RoadsOnly ?????????????????????????????????????????
                case 1:
                {
                    float jobRadius  = math.min(radius, 1500f);

                    if (entityChanged || modeChanged)
                    {
                        if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }

                        m_ActiveRoadCurves.Clear();
                        m_ActiveRoadCurves.Capacity = PayloadLimits.RawRoadListCapacity;

                        var gatherJob = new GatherRoadCurvesJob
                        {
                            m_CurveType      = GetComponentTypeHandle<Curve>(true),
                            m_Origin         = worldOrigin,
                            m_RadiusSq       = jobRadius * jobRadius,
                            m_ResultWriter   = m_ActiveRoadCurves.AsParallelWriter()
                        };
                        m_PendingJob         = gatherJob.ScheduleParallel(m_RoadSegmentQuery, default);
                        m_JobPending         = true;
                        m_LastBFSEntity      = selected;
                        m_LastDispatchMode   = 1;
                        m_LastDispatchOrigin = worldOrigin;
                    }

                    if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }

                    // Render native 3D curves directly in the game's engine
                    UnityEngine.Color parsedColor = UnityEngine.Color.cyan;
                    if (!string.IsNullOrEmpty(resolvedColor))
                    {
                        UnityEngine.ColorUtility.TryParseHtmlString(resolvedColor, out parsedColor);
                    }
                    UnityEngine.Color nativeColor = new UnityEngine.Color(parsedColor.r, parsedColor.g, parsedColor.b, resolvedOpacity);

                    OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out Unity.Jobs.JobHandle dependencies);
                    for (int i = 0; i < m_ActiveRoadCurves.Length; i++)
                    {
                        var c = m_ActiveRoadCurves[i];
                        c.a.y += 0.5f;
                        c.b.y += 0.5f;
                        c.c.y += 0.5f;
                        c.d.y += 0.5f;
                        buffer.DrawCurve(nativeColor, c, 6f);
                    }
                    m_OverlayRenderSystem.AddBufferWriter(dependencies);

                    PushEmptyPayload();
                    break;
                }

                // ?? Mode 2: ExactPolygon (BFS Isochrone) ???????????????????????
                case 2:
                {
                    if (entityChanged || modeChanged)
                    {
                        if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }

                        Entity startNode = FindNearestRoadNode(worldOrigin);
                        if (startNode == Entity.Null)
                        {
                            UnityEngine.Debug.LogWarning("OnUpdate: No starting road node found near origin. Discarding ExactPolygon.");
                            break;
                        }

                        m_BFSWorldPoints.Clear();

                        var connectedEdgeLookup = GetBufferLookup<ConnectedEdge>(true);
                        var nodeGeomLookup      = GetComponentLookup<NodeGeometry>(true);
                        var edgeLookup          = GetComponentLookup<Edge>(true);
                        var curveLookup         = GetComponentLookup<Curve>(true);
                        var roadLookup          = GetComponentLookup<Road>(true);
                        
                        connectedEdgeLookup.Update(this);
                        nodeGeomLookup.Update(this);
                        edgeLookup.Update(this);
                        curveLookup.Update(this);
                        roadLookup.Update(this);

                        var bfsJob = new IsochroneBFSJob
                        {
                            m_ConnectedEdgeLookup = connectedEdgeLookup,
                            m_NodeGeomLookup      = nodeGeomLookup,
                            m_EdgeLookup          = edgeLookup,
                            m_CurveLookup         = curveLookup,
                            m_RoadLookup          = roadLookup,
                            m_StartNode           = startNode,
                            m_TravelBudget        = visualRadius,
                            m_BoundaryWorldPoints = m_BFSWorldPoints
                        };
                        JobHandle bfsHandle = bfsJob.Schedule(default);

                        var hullJob = new ConvexHullJob { m_Points = m_BFSWorldPoints };
                        JobHandle hullHandle = hullJob.Schedule(bfsHandle);

                        m_DecimatedPoints.Clear();
                        var dpJob = new DouglasPeuckerJob
                        {
                            m_Input        = m_BFSWorldPoints,
                            m_MaxOutput    = PayloadLimits.MaxPolygonVerts,
                            m_EpsilonScale = 0.002f,
                            m_Output       = m_DecimatedPoints
                        };
                        m_PendingJob         = dpJob.Schedule(hullHandle);
                        m_JobPending         = true;
                        m_LastBFSEntity      = selected;
                        m_LastDispatchMode   = 2;
                        m_LastDispatchOrigin = worldOrigin;
                    }

                    if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }

                    if (m_DecimatedPoints.Length >= 3)
                    {
                        UnityEngine.Color parsedColor = UnityEngine.Color.cyan;
                        if (!string.IsNullOrEmpty(resolvedColor))
                        {
                            UnityEngine.ColorUtility.TryParseHtmlString(resolvedColor, out parsedColor);
                        }

                        // Outline: full opacity. Fill: 25% alpha, same hue.
                        UnityEngine.Color outlineColor = new UnityEngine.Color(parsedColor.r, parsedColor.g, parsedColor.b, resolvedOpacity);
                        UnityEngine.Color fillColor    = new UnityEngine.Color(parsedColor.r, parsedColor.g, parsedColor.b, resolvedOpacity * 0.25f);

                        int   hullCount = m_DecimatedPoints.Length;
                        OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out Unity.Jobs.JobHandle dependencies);

                        // ── Scan-line fill ────────────────────────────────────────────────
                        // Walk horizontal rows across the hull's Z extent.  For every row,
                        // find the two edge intersections (convex hull guarantees exactly 2)
                        // and draw one DrawLine between them.  scanStep=8m, lineWidth=9m
                        // gives 1m overlap so there are never gaps between rows.
                        const float scanStep  = 8f;
                        const float lineWidth = 9f;

                        // Find Z extents of the hull
                        float zMin = m_DecimatedPoints[0].z;
                        float zMax = m_DecimatedPoints[0].z;
                        for (int i = 1; i < hullCount; i++)
                        {
                            float z = m_DecimatedPoints[i].z;
                            if (z < zMin) zMin = z;
                            if (z > zMax) zMax = z;
                        }

                        // Scan from bottom to top
                        for (float scanZ = zMin + scanStep * 0.5f; scanZ < zMax; scanZ += scanStep)
                        {
                            float xLeft  = float.MaxValue;
                            float xRight = float.MinValue;
                            float yLeft  = worldOrigin.y; // terrain height at intersection

                            for (int i = 0; i < hullCount; i++)
                            {
                                float3 pa = m_DecimatedPoints[i];
                                float3 pb = m_DecimatedPoints[(i + 1) % hullCount];

                                float za = pa.z, zb = pb.z;
                                // Does this edge cross the scan line?
                                if ((za <= scanZ && scanZ < zb) || (zb <= scanZ && scanZ < za))
                                {
                                    float t = (scanZ - za) / (zb - za);
                                    float xHit = pa.x + t * (pb.x - pa.x);
                                    float yHit = pa.y + t * (pb.y - pa.y);
                                    if (xHit < xLeft)  { xLeft  = xHit; yLeft = yHit; }
                                    if (xHit > xRight) { xRight = xHit; }
                                }
                            }

                            if (xLeft < xRight)
                            {
                                var scanSeg = new Colossal.Mathematics.Line3.Segment(
                                    new float3(xLeft,  yLeft + 0.5f, scanZ),
                                    new float3(xRight, yLeft + 0.5f, scanZ));
                                buffer.DrawLine(fillColor, scanSeg, lineWidth);
                            }
                        }

                        // ── Outline: bright border on every hull edge ─────────────────────
                        for (int i = 0; i < hullCount; i++)
                        {
                            float3 p1 = m_DecimatedPoints[i];
                            float3 p2 = m_DecimatedPoints[(i + 1) % hullCount];
                            p1.y += 0.5f;
                            p2.y += 0.5f;
                            buffer.DrawLine(outlineColor, new Colossal.Mathematics.Line3.Segment(p1, p2), 10f);
                        }

                        m_OverlayRenderSystem.AddBufferWriter(dependencies);
                    }

                    PushEmptyPayload();
                    break;
                }

                // -- Mode 3: CachedHeatmap -------------------------------------
                case 3:
                {
                    float jobRadius  = math.min(radius, BuildHeatmapJob.HEATMAP_RADIUS_CAP);
                    if (entityChanged || modeChanged)
                    {
                        if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }

                        Entity startNode = FindNearestRoadNode(worldOrigin);
                        if (startNode == Entity.Null)
                        {
                            UnityEngine.Debug.LogWarning("OnUpdate: No starting road node found near origin. Discarding CachedHeatmap.");
                            break;
                        }

                        m_HeatmapRaw.Clear();
                        float radiusSq = jobRadius * jobRadius;

                        var connectedEdgeLookup = GetBufferLookup<ConnectedEdge>(true);
                        var nodeGeomLookup      = GetComponentLookup<NodeGeometry>(true);
                        var edgeLookup          = GetComponentLookup<Edge>(true);
                        var curveLookup         = GetComponentLookup<Curve>(true);
                        var roadLookup          = GetComponentLookup<Road>(true);
                        
                        connectedEdgeLookup.Update(this);
                        nodeGeomLookup.Update(this);
                        edgeLookup.Update(this);
                        curveLookup.Update(this);
                        roadLookup.Update(this);

                        var heatmapJob = new BuildHeatmapJob
                        {
                            m_ConnectedEdgeLookup = connectedEdgeLookup,
                            m_NodeGeomLookup      = nodeGeomLookup,
                            m_EdgeLookup          = edgeLookup,
                            m_CurveLookup         = curveLookup,
                            m_RoadLookup          = roadLookup,
                            m_StartNode           = startNode,
                            m_Origin              = worldOrigin,
                            m_RadiusSq            = radiusSq,
                            m_Result              = m_HeatmapRaw
                        };
                        m_PendingJob         = heatmapJob.Schedule();
                        m_JobPending         = true;
                        m_LastBFSEntity      = selected;
                        m_LastDispatchMode   = 3;
                        m_LastDispatchOrigin = worldOrigin;
                    }

                    if (m_JobPending) { m_PendingJob.Complete(); m_JobPending = false; }

                    UnityEngine.Color parsedColor = UnityEngine.Color.cyan;
                    if (!string.IsNullOrEmpty(resolvedColor))
                    {
                        UnityEngine.ColorUtility.TryParseHtmlString(resolvedColor, out parsedColor);
                    }

                    OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out Unity.Jobs.JobHandle dependencies);
                    for (int i = 0; i < m_HeatmapRaw.Length; i++)
                    {
                        var item = m_HeatmapRaw[i];
                        float3 nodePos = new float3(item.worldXZ.x, worldOrigin.y, item.worldXZ.y);
                        UnityEngine.Color nodeColor = new UnityEngine.Color(parsedColor.r, parsedColor.g, parsedColor.b, resolvedOpacity * item.weight);
                        buffer.DrawCircle(nodeColor, nodePos, 10f); // 10m diameter heatmap dots
                    }
                    m_OverlayRenderSystem.AddBufferWriter(dependencies);

                    PushEmptyPayload();
                    break;
                }
            }

            m_ForceUpdate = false;

        }

        // =====================================================================
        //  ResolveGhostEntity
        //  Returns a Temp entity with TempFlags.Create — i.e. a NEW building
        //  ghost being placed in build mode. Controlled by EnablePreplacement.
        // =====================================================================
        private Entity ResolveGhostEntity()
        {
            if (m_TempBuildingQuery.IsEmpty) return Entity.Null;
            using var entities = m_TempBuildingQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                var temp = EntityManager.GetComponentData<Game.Tools.Temp>(entity);
                bool isPlacementGhost = (temp.m_Flags & Game.Tools.TempFlags.Create) != 0;
                if (!isPlacementGhost) continue;
                var prefab = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(entity).m_Prefab;
                if (prefab != Entity.Null && EntityManager.Exists(prefab) && EntityManager.HasComponent<Game.Prefabs.BuildingData>(prefab)) return entity;
            }
            return Entity.Null;
        }

        // =====================================================================
        //  ResolveHoverEntity
        //  Returns a Temp entity with TempFlags.Select — i.e. an EXISTING
        //  building the player is hovering over in select mode.
        //  Controlled by ShowOnHover.
        // =====================================================================
        private Entity ResolveHoverEntity()
        {
            if (m_TempBuildingQuery.IsEmpty) return Entity.Null;
            using var entities = m_TempBuildingQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                var temp = EntityManager.GetComponentData<Game.Tools.Temp>(entity);
                bool isHover = (temp.m_Flags & Game.Tools.TempFlags.Select) != 0;
                if (!isHover) continue;
                var prefab = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(entity).m_Prefab;
                if (prefab != Entity.Null && EntityManager.Exists(prefab) && EntityManager.HasComponent<Game.Prefabs.BuildingData>(prefab)) return entity;
            }
            return Entity.Null;
        }


        // =====================================================================
        //  Helpers
        // =====================================================================

        private void PushEmptyPayload()
        {
            PushPayload(new VisualizationPayload
            {
                mode = "hidden", originX = 50f, originY = 50f, radius = 0f, pathData = "", color = "#00c8ff", opacity = 0.55f
            });
        }

        private void PushPayload(VisualizationPayload newPayload)
        {
            bool changed = newPayload.mode != m_CurrentPayload.mode
                || !UnityEngine.Mathf.Approximately(newPayload.originX, m_CurrentPayload.originX)
                || !UnityEngine.Mathf.Approximately(newPayload.originY, m_CurrentPayload.originY)
                || !UnityEngine.Mathf.Approximately(newPayload.radius, m_CurrentPayload.radius)
                || newPayload.pathData != m_CurrentPayload.pathData
                || newPayload.color != m_CurrentPayload.color
                || !UnityEngine.Mathf.Approximately(newPayload.opacity, m_CurrentPayload.opacity);

            if (changed)
            {
                // Force instant update if mode changed, selection/radius changed, or pathData changed.
                // Otherwise (just coordinate drift from camera panning), throttle update rate to avoid Cohtml overload.
                bool forceInstant = newPayload.mode != m_CurrentPayload.mode
                    || !UnityEngine.Mathf.Approximately(newPayload.radius, m_CurrentPayload.radius)
                    || newPayload.color != m_CurrentPayload.color;

                m_PayloadCooldown++;
                if (forceInstant || m_PayloadCooldown >= 4)
                {
                    m_PayloadCooldown = 0;
                    m_CurrentPayload = newPayload;
                    m_PayloadBinding.Update();
                }
            }
            else
            {
                m_PayloadCooldown = 0;
            }
        }

        private void ResolveColorAndOpacity(Entity entity, out string color, out float opacity)
        {
            opacity = Mod.Settings != null ? (Mod.Settings.Opacity / 100f) : 0.55f;
            string defaultColor = "#00c8ff";

            if (entity == Entity.Null || !EntityManager.Exists(entity) || !EntityManager.HasComponent<PrefabRef>(entity))
            {
                color = defaultColor;
                return;
            }

            Entity prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            if (prefab == Entity.Null || !EntityManager.Exists(prefab))
            {
                color = defaultColor;
                return;
            }

            int category = -1;
            if (EntityManager.HasComponent<SchoolData>(prefab))
            {
                SchoolData sd = EntityManager.GetComponentData<SchoolData>(prefab);
                int lvl = (int)sd.m_EducationLevel;
                if (lvl <= 1) category = 7;
                else if (lvl == 2) category = 8;
                else if (lvl == 3) category = 9;
                else category = 10;
            }
            
            if (category == -1)
            {
                if (EntityManager.HasComponent<PoliceStationData>(prefab)) category = 1;
                else if (EntityManager.HasComponent<FireStationData>(prefab)) category = 2;
                else if (EntityManager.HasComponent<ParkData>(prefab)) category = 3;
                else if (EntityManager.HasComponent<HospitalData>(prefab)) category = 4;
                else if (EntityManager.HasComponent<TelecomFacilityData>(prefab)) category = 5;
                else if (EntityManager.HasComponent<PostFacilityData>(prefab)) category = 6;
                else if (EntityManager.HasBuffer<LocalModifierData>(prefab)) category = 0;
            }

            if (category != -1)
            {
                string layerId = GetLayerIdFromCategoryIndex(category);
                if (layerId != null && m_UISystem != null && m_UISystem.TryGetGlobalLayerSetting(layerId, out var setting))
                {
                    opacity = setting.Opacity * (Mod.Settings != null ? (Mod.Settings.Opacity / 100f) : 1f);
                    color = "#" + ColorUtility.ToHtmlStringRGB(setting.Color);
                    return;
                }
            }

            color = defaultColor;
        }

        private string GetLayerIdFromCategoryIndex(int index)
        {
            switch (index)
            {
                case 0: return "layer_wellbeing";
                case 1: return "layer_police";
                case 2: return "layer_fire";
                case 3: return "layer_parks";
                case 4: return "layer_healthcare";
                case 5: return "layer_telecom";
                case 6: return "layer_post";
                case 7: return "layer_edu_elementary";
                case 8: return "layer_edu_highschool";
                case 9: return "layer_edu_college";
                case 10: return "layer_edu_university";
                default: return null;
            }
        }

        private void WritePayload(IJsonWriter writer) => m_CurrentPayload.Write(writer);

        private float ResolveServiceRadius(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) return 0f;
            if (!EntityManager.HasComponent<PrefabRef>(entity)) return 0f;
            Entity prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            if (prefab == Entity.Null || !EntityManager.Exists(prefab)) return 0f;

            bool isService = EntityManager.HasComponent<SchoolData>(prefab)
                || EntityManager.HasComponent<PoliceStationData>(prefab)
                || EntityManager.HasComponent<FireStationData>(prefab)
                || EntityManager.HasComponent<ParkData>(prefab)
                || EntityManager.HasComponent<HospitalData>(prefab)
                || EntityManager.HasComponent<TelecomFacilityData>(prefab)
                || EntityManager.HasComponent<PostFacilityData>(prefab)
                || EntityManager.HasComponent<CoverageData>(prefab)
                || EntityManager.HasBuffer<LocalModifierData>(prefab);
            if (!isService) return 0f;

            if (EntityManager.HasComponent<CoverageData>(prefab))
                return EntityManager.GetComponentData<CoverageData>(prefab).m_Range;

            if (EntityManager.HasBuffer<LocalModifierData>(prefab))
            {
                var mods = EntityManager.GetBuffer<LocalModifierData>(prefab);
                for (int i = 0; i < mods.Length; i++)
                    if (mods[i].m_Radius.max > 0f) return mods[i].m_Radius.max;
            }

            if (EntityManager.HasComponent<TelecomFacilityData>(prefab))
                return EntityManager.GetComponentData<TelecomFacilityData>(prefab).m_Range;

            // No ghost fallback — if no explicit radius found, do not draw.
            return 0f;
        }

        /// <summary>
        /// Nearest road node within a hard-capped 300 m radius.
        /// Hard cap prevents the O(N) full-map scan that froze the game when
        /// called with the raw service radius (up to 8 000 m for a hospital).
        /// </summary>
        private Entity FindNearestRoadNode(float3 origin)
        {
            if (m_CachedStartNode != Entity.Null && math.distancesq(origin, m_LastSearchOrigin) < 1.0f)
            {
                return m_CachedStartNode;
            }

            const float kSearchRadius = 300f;
            float  radSq    = kSearchRadius * kSearchRadius;

            if (m_RoadNodeQuery.IsEmpty) return Entity.Null;

            var entities   = m_RoadNodeQuery.ToEntityArray(Allocator.TempJob);
            var result = new NativeArray<Entity>(1, Allocator.TempJob);

            var connectedEdgeLookup = GetBufferLookup<ConnectedEdge>(true);
            var roadLookup = GetComponentLookup<Road>(true);
            
            connectedEdgeLookup.Update(this);
            roadLookup.Update(this);

            var job = new FindNearestNodeJob
            {
                m_Entities = entities,
                m_NodeGeomLookup = GetComponentLookup<NodeGeometry>(true),
                m_ConnectedEdgeLookup = connectedEdgeLookup,
                m_RoadLookup = roadLookup,
                m_Origin = origin,
                m_RangeSq = radSq,
                m_Result = result
            };

            job.Run();

            Entity best = result[0];

            entities.Dispose();
            result.Dispose();

            m_LastSearchOrigin = origin;
            m_CachedStartNode  = best;

            return best;
        }

        private bool HasMovedBeyondThreshold(float3 current, float threshold)
        {
            float2 cur  = new float2(current.x, current.z);
            float2 last = new float2(m_LastDispatchOrigin.x, m_LastDispatchOrigin.z);
            return math.distancesq(cur, last) > threshold * threshold;
        }

        private static float GetModePositionThreshold(int mode)
        {
            switch (mode)
            {
                case 0:  return 0f;   // CircleOnly  — always re-render, zero cost
                case 1:  return 4f;   // RoadsOnly
                case 2:  return 10f;  // ExactPolygon — BFS is heavy
                case 3:  return 5f;   // CachedHeatmap
                default: return 4f;
            }
        }

        private Entity GetRootBuilding(Entity entity)
        {
            Entity current = entity;
            while (current != Entity.Null && EntityManager.Exists(current))
            {
                if (EntityManager.HasComponent<Game.Common.Owner>(current))
                {
                    Entity owner = EntityManager.GetComponentData<Game.Common.Owner>(current).m_Owner;
                    if (owner != Entity.Null && owner != current)
                    {
                        current = owner;
                        continue;
                    }
                }
                break;
            }
            return current;
        }
    }
}
