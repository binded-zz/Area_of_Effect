using System.Collections.Generic;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Game.Rendering;
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
                // if (EntityManager.HasComponent<Game.Buildings.Efficiency>(selectedEntity)) 
                //    efficiency = (int)(EntityManager.GetComponentData<Game.Buildings.Efficiency>(selectedEntity).m_Efficiency * 100);
                
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

            // Pass 2: Global network layers - process chunks efficiently
            var prefabType = GetComponentTypeHandle<PrefabRef>(true);
            var transformType = GetComponentTypeHandle<Transform>(true);
            var chunks = m_BuildingQuery.ToArchetypeChunkArray(Unity.Collections.Allocator.TempJob);

            try {
                foreach (var chunk in chunks)
                {
                    var prefabRefs = chunk.GetNativeArray(ref prefabType);
                    var transforms = chunk.GetNativeArray(ref transformType);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        ProcessGlobalPrefab(prefabRefs[i].m_Prefab, transforms[i].m_Position, preset, ref overlayBuffer);
                    }
                }
            } finally {
                chunks.Dispose();
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
                        UnityEngine.Color defaultColor = new UnityEngine.Color(1f, 1f, 0f, 0.5f);
                        if (modName.Contains("Pollution")) defaultColor = new UnityEngine.Color(0.5f, 0.3f, 0f, 0.5f);
                        if (modName.Contains("Noise")) defaultColor = new UnityEngine.Color(1f, 0f, 0f, 0.5f);
                        if (modName.Contains("Wellbeing")) {
                            modName = "Well-being";
                            defaultColor = new UnityEngine.Color(0f, 1f, 0f, 0.5f);
                        }
                        DrawLocalEffect($"LocalModifier_{modName}", modName, defaultColor, mod.m_Radius.max, position, ref overlayBuffer);
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

        // ---- GLOBAL LAYERS (All buildings on map) ----

        private string GetEducationLayerId(Entity prefabEntity)
        {
            if (!EntityManager.HasComponent<SchoolData>(prefabEntity)) return null;
            SchoolData sd = EntityManager.GetComponentData<SchoolData>(prefabEntity);
            int lvl = (int)sd.m_EducationLevel;
            if (lvl <= 1) return "layer_edu_elementary";
            if (lvl == 2) return "layer_edu_highschool";
            if (lvl == 3) return "layer_edu_college";
            return "layer_edu_university";
        }

        private string GetGlobalCategory(Entity prefabEntity)
        {
            string eduId = GetEducationLayerId(prefabEntity);
            if (eduId != null) return eduId;
            if (EntityManager.HasComponent<PoliceStationData>(prefabEntity)) return "layer_police";
            if (EntityManager.HasComponent<FireStationData>(prefabEntity)) return "layer_fire";
            if (EntityManager.HasComponent<ParkData>(prefabEntity)) return "layer_parks";
            if (EntityManager.HasComponent<HospitalData>(prefabEntity)) return "layer_healthcare";
            if (EntityManager.HasComponent<TelecomFacilityData>(prefabEntity)) return "layer_telecom";
            if (EntityManager.HasComponent<PostFacilityData>(prefabEntity)) return "layer_post";
            if (EntityManager.HasComponent<LocalModifierData>(prefabEntity)) return "layer_wellbeing";
            return null;
        }

        private void ProcessGlobalPrefab(Entity prefabEntity, float3 position, int preset, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            string categoryId = GetGlobalCategory(prefabEntity);
            if (categoryId == null) return;
            if (!m_UISystem.TryGetGlobalLayerSetting(categoryId, out var setting) || !setting.Enabled) return;

            float opacityMult = Mod.Settings != null ? (Mod.Settings.Opacity / 100f) : 0.5f;
            UnityEngine.Color baseColor = setting.Color;
            baseColor.a = setting.Opacity;

            float highlightRadius = Mod.Settings != null ? Mod.Settings.GlobalCircleSize : 100f; 
            
            DrawGlobalRings(baseColor, position, highlightRadius, opacityMult, (int)preset, ref overlayBuffer);
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

        private void UpdateFloatingStats()
        {
            m_FloatingStats.Clear();
            if (m_CameraUpdateSystem.activeCamera == null) return;

            float3 cameraPos = m_CameraUpdateSystem.position;
            Camera cam = m_CameraUpdateSystem.activeCamera;
            
            float maxDist = Mod.Settings != null ? Mod.Settings.LabelDistance : 200f;
            float maxDistanceSq = maxDist * maxDist;

            m_FramesSinceLastStatUpdate++;
            if (m_FramesSinceLastStatUpdate >= 30) {
                m_FramesSinceLastStatUpdate = 0;
                m_CachedStats.Clear();

                var prefabType = GetComponentTypeHandle<PrefabRef>(true);
                var transformType = GetComponentTypeHandle<Transform>(true);
                var localModifierLookup = GetBufferLookup<LocalModifierData>(true);
                var attractionLookup = GetComponentLookup<AttractionData>(true);
                var telecomLookup = GetComponentLookup<TelecomFacilityData>(true);

                localModifierLookup.Update(this);
                attractionLookup.Update(this);
                telecomLookup.Update(this);

                var chunks = m_BuildingQuery.ToArchetypeChunkArray(Unity.Collections.Allocator.TempJob);

                try {
                    foreach (var chunk in chunks)
                    {
                        var prefabRefs = chunk.GetNativeArray(ref prefabType);
                        var transforms = chunk.GetNativeArray(ref transformType);
                        var entities = chunk.GetNativeArray(GetEntityTypeHandle());

                        for (int i = 0; i < chunk.Count; i++)
                        {
                            float3 worldPos = transforms[i].m_Position;
                            float distSq = math.distancesq(worldPos, cameraPos);
                            if (distSq > maxDistanceSq) continue;

                            Entity buildingEntity = entities[i];
                            Entity prefab = prefabRefs[i].m_Prefab;
                            
                            // 1. Efficiency
                            if (EntityManager.HasComponent<Game.Buildings.Efficiency>(buildingEntity)) {
                                // In some versions it might be a component, in others a buffer. 
                                // We will skip the complex buffer calc for now to fix build.
                            }

                            // 2. Local Modifiers (Wellbeing, Health, Meals)
                            if (localModifierLookup.HasBuffer(prefab))
                            {
                                var modifiers = localModifierLookup[prefab];
                                foreach (var mod in modifiers)
                                {
                                    if (mod.m_Delta.max > 0) {
                                        string label = mod.m_Type.ToString();
                                        string icon = "Healthcare";
                                        if (mod.m_Type == Game.Buildings.LocalModifierType.Wellbeing) {
                                            label = "Well-being";
                                            icon = "Wellbeing";
                                            // Heuristic: If it has Wellbeing but it's a restaurant/commercial, call it "Meals"
                                            if (EntityManager.HasComponent<Game.Prefabs.ObjectData>(prefab)) { // Generic check
                                                label = "Meals";
                                                icon = "Meals";
                                            }
                                        }
                                        m_CachedStats.Add(new CachedStat { worldPos = worldPos, label = label, value = mod.m_Delta.max, icon = icon });
                                    }
                                }
                            }

                            // 3. Attraction
                            if (attractionLookup.HasComponent(prefab))
                            {
                                var attr = attractionLookup[prefab];
                                if (attr.m_Attractiveness > 0)
                                    m_CachedStats.Add(new CachedStat { worldPos = worldPos, label = "Attractiveness", value = attr.m_Attractiveness, icon = "Parks" });
                            }
                            
                            // 4. Telecom
                            if (telecomLookup.HasComponent(prefab))
                            {
                                var tel = telecomLookup[prefab];
                                if (tel.m_Range > 0)
                                    m_CachedStats.Add(new CachedStat { worldPos = worldPos, label = "Telecom Range", value = tel.m_Range, icon = "Telecom" });
                            }
                        }
                    }
                } finally {
                    chunks.Dispose();
                }
            }

            float overlayHeight = Mod.Settings != null ? Mod.Settings.OverlayHeight : 10f;
            var groupedStats = new Dictionary<float3, List<AreaOfEffectUISystem.StatEntry>>();
            
            for (int i = 0; i < m_CachedStats.Count; i++) {
                var stat = m_CachedStats[i];
                if (!groupedStats.ContainsKey(stat.worldPos))
                    groupedStats[stat.worldPos] = new List<AreaOfEffectUISystem.StatEntry>();
                
                string hex = "#ffffff";
                string layerId = "";
                if (stat.label == "Well-being") layerId = "layer_wellbeing";
                else if (stat.label == "Attractiveness") layerId = "layer_parks";
                else if (stat.label == "Telecom Range") layerId = "layer_telecom";
                else layerId = $"LocalModifier_{stat.label}";

                if (m_UISystem.TryGetGlobalLayerSetting(layerId, out var gs)) 
                    hex = ColorUtility.ToHtmlStringRGB(gs.Color);
                else if (m_UISystem.TryGetLocalEffectSetting(layerId, out var ls))
                    hex = ColorUtility.ToHtmlStringRGB(ls.Color);

                groupedStats[stat.worldPos].Add(new AreaOfEffectUISystem.StatEntry { 
                    label = stat.label, 
                    value = stat.value, 
                    icon = stat.icon,
                    color = $"#{hex}"
                });
            }

            foreach (var kvp in groupedStats)
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
