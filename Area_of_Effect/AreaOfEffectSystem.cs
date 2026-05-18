using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Game.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Game.Tools;
using Game.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Game.Objects;
using Game.SceneFlow;
using Game.Buildings;
using Game.City;
using Transform = Game.Objects.Transform;

namespace Area_of_Effect
{
    public partial class AreaOfEffectSystem : GameSystemBase
    {
        public struct OverlayDrawItem
        {
            public float3 m_Position;
            public int m_Category;
        }

        public struct CachedStatItem
        {
            public float3 m_Position;
            public int m_IconType; // 0 = Healthcare, 1 = Wellbeing, 2 = Meals, 3 = Parks, 4 = Telecom
            public int m_LabelType; // 0 = Healthcare, 1 = Well-being, 2 = Meals, 3 = Attractiveness, 4 = Telecom Range
            public float m_Value;
        }

        [Unity.Burst.BurstCompile]
        public struct FindGlobalOverlaysJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
            [ReadOnly] public ComponentTypeHandle<Transform> m_TransformType;
            
            [ReadOnly] public ComponentLookup<PoliceStationData> m_PoliceLookups;
            [ReadOnly] public ComponentLookup<FireStationData> m_FireLookups;
            [ReadOnly] public ComponentLookup<ParkData> m_ParkLookups;
            [ReadOnly] public ComponentLookup<HospitalData> m_HospitalLookups;
            [ReadOnly] public ComponentLookup<TelecomFacilityData> m_TelecomLookups;
            [ReadOnly] public ComponentLookup<PostFacilityData> m_PostLookups;
            [ReadOnly] public BufferLookup<LocalModifierData> m_LocalModifierLookups;
            [ReadOnly] public ComponentLookup<SchoolData> m_SchoolLookups;

            public bool m_LayerWellbeingEnabled;
            public bool m_LayerPoliceEnabled;
            public bool m_LayerFireEnabled;
            public bool m_LayerParksEnabled;
            public bool m_LayerHealthcareEnabled;
            public bool m_LayerTelecomEnabled;
            public bool m_LayerPostEnabled;
            public bool m_LayerEduElementaryEnabled;
            public bool m_LayerEduHighschoolEnabled;
            public bool m_LayerEduCollegeEnabled;
            public bool m_LayerEduUniversityEnabled;

            public NativeList<OverlayDrawItem> m_DrawList;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var prefabs = chunk.GetNativeArray(ref m_PrefabType);
                var transforms = chunk.GetNativeArray(ref m_TransformType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity prefab = prefabs[i].m_Prefab;
                    float3 pos = transforms[i].m_Position;

                    int category = -1;

                    if (m_SchoolLookups.HasComponent(prefab))
                    {
                        SchoolData sd = m_SchoolLookups[prefab];
                        int lvl = (int)sd.m_EducationLevel;
                        if (lvl <= 1) { if (m_LayerEduElementaryEnabled) category = 7; }
                        else if (lvl == 2) { if (m_LayerEduHighschoolEnabled) category = 8; }
                        else if (lvl == 3) { if (m_LayerEduCollegeEnabled) category = 9; }
                        else { if (m_LayerEduUniversityEnabled) category = 10; }
                    }

                    if (category == -1)
                    {
                        if (m_LayerPoliceEnabled && m_PoliceLookups.HasComponent(prefab)) category = 1;
                        else if (m_LayerFireEnabled && m_FireLookups.HasComponent(prefab)) category = 2;
                        else if (m_LayerParksEnabled && m_ParkLookups.HasComponent(prefab)) category = 3;
                        else if (m_LayerHealthcareEnabled && m_HospitalLookups.HasComponent(prefab)) category = 4;
                        else if (m_LayerTelecomEnabled && m_TelecomLookups.HasComponent(prefab)) category = 5;
                        else if (m_LayerPostEnabled && m_PostLookups.HasComponent(prefab)) category = 6;
                        else if (m_LayerWellbeingEnabled && m_LocalModifierLookups.HasBuffer(prefab)) category = 0;
                    }

                    if (category != -1)
                    {
                        m_DrawList.Add(new OverlayDrawItem
                        {
                            m_Position = pos,
                            m_Category = category
                        });
                    }
                }
            }
        }

        [Unity.Burst.BurstCompile]
        public struct FindFloatingStatsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
            [ReadOnly] public ComponentTypeHandle<Transform> m_TransformType;
            [ReadOnly] public EntityTypeHandle m_EntityType;
            
            [ReadOnly] public BufferLookup<LocalModifierData> m_LocalModifierLookup;
            [ReadOnly] public ComponentLookup<AttractionData> m_AttractionLookup;
            [ReadOnly] public ComponentLookup<TelecomFacilityData> m_TelecomLookup;
            [ReadOnly] public ComponentLookup<ObjectData> m_ObjectLookup;

            public float3 m_CameraPos;
            public float m_MaxDistanceSq;

            public NativeList<CachedStatItem> m_StatList;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var prefabs = chunk.GetNativeArray(ref m_PrefabType);
                var transforms = chunk.GetNativeArray(ref m_TransformType);
                var entities = chunk.GetNativeArray(m_EntityType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    float3 worldPos = transforms[i].m_Position;
                    float distSq = math.distancesq(worldPos, m_CameraPos);
                    if (distSq > m_MaxDistanceSq) continue;

                    Entity prefab = prefabs[i].m_Prefab;

                    // 1. Local Modifiers
                    if (m_LocalModifierLookup.HasBuffer(prefab))
                    {
                        var modifiers = m_LocalModifierLookup[prefab];
                        for (int j = 0; j < modifiers.Length; j++)
                        {
                            var mod = modifiers[j];
                            if (mod.m_Delta.max > 0)
                            {
                                int iconType = 0; // Healthcare
                                int labelType = 0; // Wellbeing
                                if (mod.m_Type == Game.Buildings.LocalModifierType.Wellbeing)
                                {
                                    iconType = 1; // Wellbeing
                                    labelType = 1; // Well-being
                                    if (m_ObjectLookup.HasComponent(prefab))
                                    {
                                        iconType = 2; // Meals
                                        labelType = 2; // Meals
                                    }
                                }
                                m_StatList.Add(new CachedStatItem
                                {
                                    m_Position = worldPos,
                                    m_IconType = iconType,
                                    m_LabelType = labelType,
                                    m_Value = mod.m_Delta.max
                                });
                            }
                        }
                    }

                    // 2. Attraction
                    if (m_AttractionLookup.HasComponent(prefab))
                    {
                        var attr = m_AttractionLookup[prefab];
                        if (attr.m_Attractiveness > 0)
                        {
                            m_StatList.Add(new CachedStatItem
                            {
                                m_Position = worldPos,
                                m_IconType = 3, // Parks
                                m_LabelType = 3, // Attractiveness
                                m_Value = attr.m_Attractiveness
                            });
                        }
                    }

                    // 3. Telecom
                    if (m_TelecomLookup.HasComponent(prefab))
                    {
                        var tel = m_TelecomLookup[prefab];
                        if (tel.m_Range > 0)
                        {
                            m_StatList.Add(new CachedStatItem
                            {
                                m_Position = worldPos,
                                m_IconType = 4, // Telecom
                                m_LabelType = 4, // Telecom Range
                                m_Value = tel.m_Range
                            });
                        }
                    }
                }
            }
        }

        private ILog log;
        private ToolSystem m_ToolSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private AreaOfEffectUISystem m_UISystem;
        private CameraUpdateSystem m_CameraUpdateSystem;
        private NameSystem m_NameSystem;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_BuildingQuery;
        private List<AreaOfEffectUISystem.FloatingStat> m_FloatingStats = new List<AreaOfEffectUISystem.FloatingStat>();
        private int m_FramesSinceLastStatUpdate = 0;

        protected override void OnCreate()
        {
            base.OnCreate();
            log = Mod.log;
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_UISystem = World.GetOrCreateSystemManaged<AreaOfEffectUISystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_UISystem.EnsureWellbeingRegistered();

            m_BuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Transform>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (Mod.Settings == null || !Mod.Settings.IsEnabled || GameManager.instance.isGameLoading)
            {
                if (m_FloatingStats.Count > 0)
                {
                    m_FloatingStats.Clear();
                    m_UISystem.UpdateFloatingStats(m_FloatingStats);
                }
                return;
            }

            OverlayRenderSystem.Buffer overlayBuffer = m_OverlayRenderSystem.GetBuffer(out Unity.Jobs.JobHandle dependencies);
            int preset = (int)Mod.Settings.Preset;

            // Pass 1: Selected building - local effects and data
            Entity selectedEntity = m_ToolSystem.selected;
            if (selectedEntity != Entity.Null && EntityManager.HasComponent<PrefabRef>(selectedEntity))
            {
                Transform transform = EntityManager.GetComponentData<Transform>(selectedEntity);
                ProcessLocalEntity(selectedEntity, transform.m_Position, ref overlayBuffer);

                // Update UI with selected building info
                string name = m_NameSystem.GetRenderedLabelName(selectedEntity);
                int efficiency = 0;
                
                int wellbeing = 0;
                int reach = 0;
                PrefabRef pRef = EntityManager.GetComponentData<PrefabRef>(selectedEntity);
                if (EntityManager.HasComponent<CoverageData>(pRef.m_Prefab)) 
                    reach = (int)EntityManager.GetComponentData<CoverageData>(pRef.m_Prefab).m_Range;
                
                if (EntityManager.HasBuffer<LocalModifierData>(pRef.m_Prefab)) {
                    var mods = EntityManager.GetBuffer<LocalModifierData>(pRef.m_Prefab);
                    foreach (var m in mods) if (m.m_Type == Game.Buildings.LocalModifierType.Wellbeing) wellbeing = (int)m.m_Delta.max;
                }

                m_UISystem.UpdateBuildingData(name, efficiency, wellbeing, reach);
            }
            else
            {
                m_UISystem.UpdateBuildingData("", 0, 0, 0);
            }

            // Pass 2: Global network layers - process chunks efficiently using Burst job
            if (m_UISystem.IsAnyGlobalLayerActive())
            {
                var drawList = new NativeList<OverlayDrawItem>(Allocator.TempJob);
                
                FindGlobalOverlaysJob job = new FindGlobalOverlaysJob
                {
                    m_PrefabType = GetComponentTypeHandle<PrefabRef>(true),
                    m_TransformType = GetComponentTypeHandle<Transform>(true),
                    
                    m_PoliceLookups = GetComponentLookup<PoliceStationData>(true),
                    m_FireLookups = GetComponentLookup<FireStationData>(true),
                    m_ParkLookups = GetComponentLookup<ParkData>(true),
                    m_HospitalLookups = GetComponentLookup<HospitalData>(true),
                    m_TelecomLookups = GetComponentLookup<TelecomFacilityData>(true),
                    m_PostLookups = GetComponentLookup<PostFacilityData>(true),
                    m_LocalModifierLookups = GetBufferLookup<LocalModifierData>(true),
                    m_SchoolLookups = GetComponentLookup<SchoolData>(true),

                    m_LayerWellbeingEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_wellbeing", out var wb) && wb.Enabled,
                    m_LayerPoliceEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_police", out var pc) && pc.Enabled,
                    m_LayerFireEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_fire", out var fp) && fp.Enabled,
                    m_LayerParksEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_parks", out var pk) && pk.Enabled,
                    m_LayerHealthcareEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_healthcare", out var hc) && hc.Enabled,
                    m_LayerTelecomEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_telecom", out var tc) && tc.Enabled,
                    m_LayerPostEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_post", out var ps) && ps.Enabled,
                    m_LayerEduElementaryEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_edu_elementary", out var ee) && ee.Enabled,
                    m_LayerEduHighschoolEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_edu_highschool", out var eh) && eh.Enabled,
                    m_LayerEduCollegeEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_edu_college", out var ec) && ec.Enabled,
                    m_LayerEduUniversityEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_edu_university", out var eu) && eu.Enabled,

                    m_DrawList = drawList
                };

                JobHandle jobHandle = job.Schedule(m_BuildingQuery, dependencies);
                jobHandle.Complete(); // Instant compilation & execution on background threads

                for (int i = 0; i < drawList.Length; i++)
                {
                    var item = drawList[i];
                    string layerId = GetLayerIdFromCategoryIndex(item.m_Category);
                    if (layerId != null && m_UISystem.TryGetGlobalLayerSetting(layerId, out var setting) && setting.Enabled)
                    {
                        float opacityMult = Mod.Settings != null ? (Mod.Settings.Opacity / 100f) : 0.5f;
                        UnityEngine.Color baseColor = setting.Color;
                        baseColor.a = setting.Opacity;
                        float highlightRadius = Mod.Settings != null ? Mod.Settings.GlobalCircleSize : 100f;
                        DrawGlobalRings(baseColor, item.m_Position, highlightRadius, opacityMult, preset, ref overlayBuffer);
                    }
                }

                drawList.Dispose();
            }

            // Pass 3: Floating Stats (Text labels)
            if (Mod.Settings != null && Mod.Settings.ShowStats)
            {
                UpdateFloatingStats();
            }
            else if (m_FloatingStats.Count > 0)
            {
                m_FloatingStats.Clear();
                m_UISystem.UpdateFloatingStats(m_FloatingStats);
            }

            m_OverlayRenderSystem.AddBufferWriter(dependencies);
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

        private static string GetLabelString(int type)
        {
            switch (type)
            {
                case 1: return "Well-being";
                case 2: return "Meals";
                case 3: return "Attractiveness";
                case 4: return "Telecom Range";
                default: return "Healthcare";
            }
        }

        private static string GetIconString(int type)
        {
            switch (type)
            {
                case 1: return "Wellbeing";
                case 2: return "Meals";
                case 3: return "Parks";
                case 4: return "Telecom";
                default: return "Healthcare";
            }
        }

        public void ClearStatsCache()
        {
            m_FramesSinceLastStatUpdate = 999; // Force update next frame
        }

        // ---- LOCAL ENTITY (Selected Building) ----

        private void ProcessLocalEntity(Entity entity, float3 position, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            if (EntityManager.HasComponent<PrefabRef>(entity))
            {
                PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                ProcessLocalPrefab(prefabRef.m_Prefab, position, ref overlayBuffer);
            }
            if (EntityManager.HasBuffer<Game.Buildings.InstalledUpgrade>(entity))
            {
                var upgrades = EntityManager.GetBuffer<Game.Buildings.InstalledUpgrade>(entity);
                foreach (var upgrade in upgrades)
                    ProcessLocalEntity(upgrade.m_Upgrade, position, ref overlayBuffer);
            }
        }

        private void ProcessLocalPrefab(Entity prefabEntity, float3 position, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            if (EntityManager.HasComponent<CoverageData>(prefabEntity))
            {
                CoverageData coverageData = EntityManager.GetComponentData<CoverageData>(prefabEntity);
                DrawLocalEffect("CoverageData", "Coverage", new UnityEngine.Color(0f, 1f, 0f, 0.5f), coverageData.m_Range, position, ref overlayBuffer);
            }
            if (EntityManager.HasBuffer<Game.Prefabs.LocalModifierData>(prefabEntity))
            {
                var modifiers = EntityManager.GetBuffer<Game.Prefabs.LocalModifierData>(prefabEntity);
                foreach (var mod in modifiers)
                {
                    if (mod.m_Radius.max > 0)
                    {
                        string modName = mod.m_Type.ToString();
                        if (modName.Contains("Wellbeing")) {
                            UnityEngine.Color defaultColor = new UnityEngine.Color(0f, 1f, 0f, 0.5f);
                            DrawLocalEffect("LocalModifier_Wellbeing", "Well-being Modifier", defaultColor, mod.m_Radius.max, position, ref overlayBuffer);
                        }
                    }
                }
            }
        }

        private void DrawLocalEffect(string id, string defaultName, UnityEngine.Color defaultColor, float radius, float3 position, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            if (radius <= 0) return;
            m_UISystem.RegisterLocalEffectType(id, defaultName, defaultColor);
            if (m_UISystem.TryGetLocalEffectSetting(id, out var setting) && setting.Enabled)
            {
                float globalOpacity = Mod.Settings != null ? (Mod.Settings.Opacity / 100f) : 1.0f;
                float heightOffset = Mod.Settings != null ? Mod.Settings.OverlayHeight : 1f;
                float3 center = position;
                center.y += heightOffset;
                UnityEngine.Color c = setting.Color;
                c.a = Mathf.Clamp01(setting.Opacity * globalOpacity);
                overlayBuffer.DrawCircle(c, center, radius);
            }
        }

        private void DrawGlobalRings(UnityEngine.Color baseColor, float3 position, float radius, float opacityMult, int preset, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            float heightOffset = Mod.Settings != null ? Mod.Settings.OverlayHeight : 1f;
            float3 center = position;
            center.y += heightOffset;

            UnityEngine.Color c1, c2, c3;
            float safeOpacity = math.clamp(opacityMult, 0.05f, 1f);
            switch (preset)
            {
                case 1: // Soft Glow - multiple overlapping full circles
                    c1 = baseColor; c1.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.15f);
                    c2 = baseColor; c2.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.30f);
                    c3 = baseColor; c3.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.60f);
                    overlayBuffer.DrawCircle(c1, center, radius);
                    overlayBuffer.DrawCircle(c2, center, radius * 0.65f);
                    overlayBuffer.DrawCircle(c3, center, radius * 0.20f);
                    break;

                case 2: // Classic - single ring
                    c1 = baseColor; c1.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.45f);
                    overlayBuffer.DrawCircle(c1, center, radius);
                    break;

                default: // Rings - concentric, each slightly different
                    c1 = baseColor; c1.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.20f);
                    c2 = baseColor; c2.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.45f);
                    c3 = baseColor; c3.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.80f);
                    overlayBuffer.DrawCircle(c1, center, radius);
                    overlayBuffer.DrawCircle(c2, center, radius * 0.55f);
                    overlayBuffer.DrawCircle(c3, center, radius * 0.12f);
                    break;
            }
        }

        private Entity m_LastSelectedEntity = Entity.Null;

        private struct CachedStat {
            public float3 worldPos;
            public string label;
            public float value;
            public string icon;
        }
        private List<CachedStat> m_CachedStats = new List<CachedStat>();

        private Dictionary<float3, List<AreaOfEffectUISystem.StatEntry>> m_GroupedStats = new Dictionary<float3, List<AreaOfEffectUISystem.StatEntry>>();
        private List<List<AreaOfEffectUISystem.StatEntry>> m_ListPool = new List<List<AreaOfEffectUISystem.StatEntry>>();
        private int m_ListPoolUsed = 0;

        private Dictionary<string, string> m_LabelToLayerIdCache = new Dictionary<string, string>();
        private Dictionary<Color, string> m_ColorToHexCache = new Dictionary<Color, string>();

        private List<AreaOfEffectUISystem.StatEntry> GetListFromPool()
        {
            if (m_ListPoolUsed < m_ListPool.Count)
            {
                var list = m_ListPool[m_ListPoolUsed++];
                list.Clear();
                return list;
            }
            var newList = new List<AreaOfEffectUISystem.StatEntry>();
            m_ListPool.Add(newList);
            m_ListPoolUsed++;
            return newList;
        }

        private string GetLayerIdForLabel(string label)
        {
            if (m_LabelToLayerIdCache.TryGetValue(label, out var id)) return id;
            string newId;
            if (label == "Well-being") newId = "layer_wellbeing";
            else if (label == "Attractiveness") newId = "layer_parks";
            else if (label == "Telecom Range") newId = "layer_telecom";
            else newId = $"LocalModifier_{label}";
            m_LabelToLayerIdCache[label] = newId;
            return newId;
        }

        private string GetHexForColor(Color color)
        {
            if (m_ColorToHexCache.TryGetValue(color, out var hex)) return hex;
            string newHex = ColorUtility.ToHtmlStringRGB(color);
            m_ColorToHexCache[color] = newHex;
            return newHex;
        }

        private void UpdateFloatingStats()
        {
            m_FloatingStats.Clear();
            if (m_CameraUpdateSystem.activeCamera == null) return;

            float3 cameraPos = m_CameraUpdateSystem.position;
            Camera cam = m_CameraUpdateSystem.activeCamera;
            
            float maxDist = Mod.Settings != null ? Mod.Settings.LabelDistance : 1500f;
            float maxDistanceSq = maxDist * maxDist;

            m_FramesSinceLastStatUpdate++;
            if (m_FramesSinceLastStatUpdate >= 30) {
                m_FramesSinceLastStatUpdate = 0;
                m_CachedStats.Clear();

                var statList = new NativeList<CachedStatItem>(Allocator.TempJob);

                var localModifierLookup = GetBufferLookup<LocalModifierData>(true);
                var attractionLookup = GetComponentLookup<AttractionData>(true);
                var telecomLookup = GetComponentLookup<TelecomFacilityData>(true);
                var objectLookup = GetComponentLookup<ObjectData>(true);

                localModifierLookup.Update(this);
                attractionLookup.Update(this);
                telecomLookup.Update(this);
                objectLookup.Update(this);

                FindFloatingStatsJob job = new FindFloatingStatsJob
                {
                    m_PrefabType = GetComponentTypeHandle<PrefabRef>(true),
                    m_TransformType = GetComponentTypeHandle<Transform>(true),
                    m_EntityType = GetEntityTypeHandle(),
                    
                    m_LocalModifierLookup = localModifierLookup,
                    m_AttractionLookup = attractionLookup,
                    m_TelecomLookup = telecomLookup,
                    m_ObjectLookup = objectLookup,
                    
                    m_CameraPos = cameraPos,
                    m_MaxDistanceSq = maxDistanceSq,
                    m_StatList = statList
                };

                JobHandle jobHandle = job.Schedule(m_BuildingQuery, default);
                jobHandle.Complete(); // Instantly process in parallel using Burst

                for (int i = 0; i < statList.Length; i++)
                {
                    var item = statList[i];
                    string label = GetLabelString(item.m_LabelType);
                    string icon = GetIconString(item.m_IconType);
                    m_CachedStats.Add(new CachedStat { worldPos = item.m_Position, label = label, value = item.m_Value, icon = icon });
                }

                statList.Dispose();
            }

            float overlayHeight = Mod.Settings != null ? Mod.Settings.OverlayHeight : 10f;
            m_GroupedStats.Clear();
            m_ListPoolUsed = 0;
            
            for (int i = 0; i < m_CachedStats.Count; i++) {
                var stat = m_CachedStats[i];
                if (!m_GroupedStats.TryGetValue(stat.worldPos, out var list))
                {
                    list = GetListFromPool();
                    m_GroupedStats[stat.worldPos] = list;
                }
                
                string hex = "ffffff";
                string layerId = GetLayerIdForLabel(stat.label);

                if (m_UISystem.TryGetGlobalLayerSetting(layerId, out var gs)) 
                    hex = GetHexForColor(gs.Color);
                else if (m_UISystem.TryGetLocalEffectSetting(layerId, out var ls))
                    hex = GetHexForColor(ls.Color);

                list.Add(new AreaOfEffectUISystem.StatEntry { 
                    label = stat.label, 
                    value = stat.value, 
                    icon = stat.icon,
                    color = $"#{hex}"
                });
            }

            foreach (var kvp in m_GroupedStats)
            {
                float3 labelWorldPos = kvp.Key;
                labelWorldPos.y += overlayHeight + 15f; 
                float3 viewportPos = cam.WorldToViewportPoint(labelWorldPos);
                
                if (viewportPos.z <= 0f) continue;

                float x = viewportPos.x * 100f;
                float y = (1f - viewportPos.y) * 100f;

                if (x < -10 || x > 110 || y < -10 || y > 110) continue;

                m_FloatingStats.Add(new AreaOfEffectUISystem.FloatingStat { 
                    entries = kvp.Value, 
                    x = x, y = y, z = viewportPos.z 
                });
            }

            if (m_FloatingStats.Count > 50) {
                m_FloatingStats.Sort((a, b) => a.z.CompareTo(b.z));
                m_FloatingStats.RemoveRange(50, m_FloatingStats.Count - 50);
            }

            m_UISystem.UpdateFloatingStats(m_FloatingStats);
        }

    }
}
