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
            [ReadOnly] public ComponentLookup<DeathcareFacilityData> m_DeathcareLookups;

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
            public bool m_LayerDeathcareEnabled;

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
                        else if (m_LayerDeathcareEnabled && m_DeathcareLookups.HasComponent(prefab)) category = 14;
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
            [ReadOnly] public ComponentLookup<CommercialProperty> m_CommercialPropertyLookup;
            [ReadOnly] public ComponentLookup<SchoolData> m_SchoolLookup;
            [ReadOnly] public ComponentLookup<PoliceStationData> m_PoliceLookup;
            [ReadOnly] public ComponentLookup<FireStationData> m_FireLookup;
            [ReadOnly] public ComponentLookup<HospitalData> m_HospitalLookup;
            [ReadOnly] public ComponentLookup<DeathcareFacilityData> m_DeathcareLookup;
            [ReadOnly] public ComponentLookup<PostFacilityData> m_PostLookup;
            [ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgradeLookup;
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefLookup;

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

                    Entity entity = entities[i];
                    Entity prefab = prefabs[i].m_Prefab;

                    int upgradeCount = 0;
                    if (m_InstalledUpgradeLookup.HasBuffer(entity))
                    {
                        upgradeCount = m_InstalledUpgradeLookup[entity].Length;
                    }

                    for (int pIdx = -1; pIdx < upgradeCount; pIdx++)
                    {
                        Entity currentPrefab = Entity.Null;
                        if (pIdx == -1)
                        {
                            currentPrefab = prefab;
                        }
                        else
                        {
                            Entity upgradeEntity = m_InstalledUpgradeLookup[entity][pIdx].m_Upgrade;
                            if (m_PrefabRefLookup.HasComponent(upgradeEntity))
                            {
                                currentPrefab = m_PrefabRefLookup[upgradeEntity].m_Prefab;
                            }
                        }

                        if (currentPrefab == Entity.Null) continue;

                        // 1. Local Modifiers
                        if (m_LocalModifierLookup.HasBuffer(currentPrefab))
                        {
                            var modifiers = m_LocalModifierLookup[currentPrefab];
                            for (int j = 0; j < modifiers.Length; j++)
                            {
                                var mod = modifiers[j];
                                float val = (math.abs(mod.m_Delta.max) > math.abs(mod.m_Delta.min)) ? mod.m_Delta.max : mod.m_Delta.min;
                                if (math.abs(val) > 0.001f)
                                {
                                    int iconType = 0; // Healthcare
                                    int labelType = 0; // Wellbeing
                                    if (mod.m_Type == Game.Buildings.LocalModifierType.Wellbeing)
                                    {
                                        iconType = 1; // Wellbeing
                                        labelType = 1; // Well-being
                                        if (m_CommercialPropertyLookup.HasComponent(currentPrefab))
                                        {
                                            iconType = 2; // Meals
                                            labelType = 2; // Meals
                                        }
                                    }
                                    else if (mod.m_Type == Game.Buildings.LocalModifierType.Health)
                                    {
                                        iconType = 13; // Health
                                        labelType = 13; // Health
                                    }
                                    else if (mod.m_Type == Game.Buildings.LocalModifierType.CrimeAccumulation)
                                    {
                                        iconType = 16; // Crime
                                        labelType = 16; // Crime
                                    }
                                    else if (mod.m_Type == Game.Buildings.LocalModifierType.ForestFireResponseTime)
                                    {
                                        iconType = 18; // Fire Response
                                        labelType = 18; // Fire Response
                                    }
                                    else if (mod.m_Type == Game.Buildings.LocalModifierType.ForestFireHazard)
                                    {
                                        iconType = 17; // Fire Hazard
                                        labelType = 17; // Fire Hazard
                                    }
                                    m_StatList.Add(new CachedStatItem
                                    {
                                        m_Position = worldPos,
                                        m_IconType = iconType,
                                        m_LabelType = labelType,
                                        m_Value = val
                                    });
                                }
                            }
                        }

                        // 2. Attraction
                        if (m_AttractionLookup.HasComponent(currentPrefab))
                        {
                            var attr = m_AttractionLookup[currentPrefab];
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
                        if (m_TelecomLookup.HasComponent(currentPrefab))
                        {
                            var tel = m_TelecomLookup[currentPrefab];
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

                        // 4. Education
                        if (pIdx == -1 && m_SchoolLookup.HasComponent(currentPrefab))
                        {
                            SchoolData sd = m_SchoolLookup[currentPrefab];
                            int lvl = (int)sd.m_EducationLevel;
                            int iconType = 7;
                            int labelType = 7;
                            if (lvl == 2) { iconType = 8; labelType = 8; }
                            else if (lvl == 3) { iconType = 9; labelType = 9; }
                            else if (lvl >= 4) { iconType = 10; labelType = 10; }

                            m_StatList.Add(new CachedStatItem
                            {
                                m_Position = worldPos,
                                m_IconType = iconType,
                                m_LabelType = labelType,
                                m_Value = sd.m_StudentCapacity
                            });
                        }

                        // 5. Police
                        if (pIdx == -1 && m_PoliceLookup.HasComponent(currentPrefab))
                        {
                            var pd = m_PoliceLookup[currentPrefab];
                            if (pd.m_PatrolCarCapacity > 0)
                            {
                                m_StatList.Add(new CachedStatItem
                                {
                                    m_Position = worldPos,
                                    m_IconType = 11, // Police
                                    m_LabelType = 11,
                                    m_Value = pd.m_PatrolCarCapacity
                                });
                            }
                        }

                        // 6. Fire
                        if (pIdx == -1 && m_FireLookup.HasComponent(currentPrefab))
                        {
                            var fd = m_FireLookup[currentPrefab];
                            if (fd.m_FireEngineCapacity > 0)
                            {
                                m_StatList.Add(new CachedStatItem
                                {
                                    m_Position = worldPos,
                                    m_IconType = 12, // Fire
                                    m_LabelType = 12,
                                    m_Value = fd.m_FireEngineCapacity
                                });
                            }
                        }

                        // 7. Hospital (Healthcare)
                        if (pIdx == -1 && m_HospitalLookup.HasComponent(currentPrefab))
                        {
                            var hd = m_HospitalLookup[currentPrefab];
                            if (hd.m_AmbulanceCapacity > 0)
                            {
                                m_StatList.Add(new CachedStatItem
                                {
                                    m_Position = worldPos,
                                    m_IconType = 0, // Healthcare
                                    m_LabelType = 0,
                                    m_Value = hd.m_AmbulanceCapacity
                                });
                            }
                        }

                        // 8. Deathcare
                        if (pIdx == -1 && m_DeathcareLookup.HasComponent(currentPrefab))
                        {
                            var dd = m_DeathcareLookup[currentPrefab];
                            if (dd.m_HearseCapacity > 0)
                            {
                                m_StatList.Add(new CachedStatItem
                                {
                                    m_Position = worldPos,
                                    m_IconType = 14, // Deathcare
                                    m_LabelType = 14,
                                    m_Value = dd.m_HearseCapacity
                                });
                            }
                        }

                        // 9. Post
                        if (pIdx == -1 && m_PostLookup.HasComponent(currentPrefab))
                        {
                            var psd = m_PostLookup[currentPrefab];
                            if (psd.m_PostVanCapacity > 0)
                            {
                                m_StatList.Add(new CachedStatItem
                                {
                                    m_Position = worldPos,
                                    m_IconType = 15, // Post
                                    m_LabelType = 15,
                                    m_Value = psd.m_PostVanCapacity
                                });
                            }
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
        private EntityQuery m_TempBuildingQuery;
        private VisualizationDispatcherSystem m_DispatcherSystem;
        private List<AreaOfEffectUISystem.FloatingStat> m_FloatingStats = new List<AreaOfEffectUISystem.FloatingStat>();
        private int m_FramesSinceLastStatUpdate = 0;
        private int m_StatsPositionThrottleCounter = 0;

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
            m_DispatcherSystem = World.GetOrCreateSystemManaged<VisualizationDispatcherSystem>();
            m_UISystem.EnsureWellbeingRegistered();

            m_BuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Transform>()
                },
                None = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });

            m_TempBuildingQuery = GetEntityQuery(new ComponentType[] {
                ComponentType.ReadOnly<Game.Tools.Temp>(),
                ComponentType.ReadOnly<Game.Prefabs.PrefabRef>()
            });
        }

        protected override void OnUpdate()
        {
            if (Mod.Settings == null || !Mod.Settings.IsEnabled || GameManager.instance.isGameLoading)
            {
                if (m_FloatingStats.Count > 0)
                {
                    m_FloatingStats.Clear();
                    m_UISystem.UpdateFloatingStats("[]");
                }
                return;
            }

            OverlayRenderSystem.Buffer overlayBuffer = m_OverlayRenderSystem.GetBuffer(out Unity.Jobs.JobHandle dependencies);
            int preset = (int)Mod.Settings.Preset;

            // Pass 1: Selected building - local effects and data
            Entity selectedEntity = Entity.Null;
            if (m_DispatcherSystem != null && m_DispatcherSystem.EnablePreplacement)
            {
                selectedEntity = ResolveGhostEntity();
            }
            if (selectedEntity == Entity.Null && m_DispatcherSystem != null && m_DispatcherSystem.ShowOnHover)
            {
                selectedEntity = ResolveHoverEntity();
            }
            if (selectedEntity == Entity.Null)
            {
                selectedEntity = m_ToolSystem.selected;
            }

            selectedEntity = GetRootBuilding(selectedEntity);

            if (selectedEntity != Entity.Null && EntityManager.Exists(selectedEntity) && EntityManager.HasComponent<PrefabRef>(selectedEntity) && EntityManager.HasComponent<Transform>(selectedEntity))
            {
                bool isGhost = false;
                if (EntityManager.HasComponent<Game.Tools.Temp>(selectedEntity))
                {
                    var temp = EntityManager.GetComponentData<Game.Tools.Temp>(selectedEntity);
                    isGhost = (temp.m_Flags & Game.Tools.TempFlags.Create) != 0;
                }
                Transform transform = EntityManager.GetComponentData<Transform>(selectedEntity);
                ProcessLocalEntity(selectedEntity, transform.m_Position, isGhost, ref overlayBuffer);

                PrefabRef pRef = EntityManager.GetComponentData<PrefabRef>(selectedEntity);
                Entity prefab = pRef.m_Prefab;

                // Resolve building name — ghost entities may have no rendered label, fall back to prefab name
                string buildingLabel = m_NameSystem.GetRenderedLabelName(selectedEntity);
                if (string.IsNullOrEmpty(buildingLabel) && prefab != Entity.Null && EntityManager.Exists(prefab))
                    buildingLabel = m_NameSystem.GetRenderedLabelName(prefab);

                // Gather effects once, share between mini-inspector and main panel
                var sharedEffects = new List<AreaOfEffectUISystem.BuildingEffectEntry>();
                GatherSelectedBuildingEffects(selectedEntity, sharedEffects);
                string sharedEffectsJson = SerializeBuildingEffects(sharedEffects);

                // Update Mini-Inspector
                m_UISystem.UpdateInspectorData(buildingLabel, sharedEffectsJson);

                // Update main panel Selected Building tab
                int efficiency = 0;
                float covRange = 0f, covMag = 0f, modRange = 0f, modMag = 0f;

                // Coverage — prefer live ModifiedServiceCoverage on the entity, fall back to prefab
                if (EntityManager.HasComponent<Game.Buildings.ModifiedServiceCoverage>(selectedEntity))
                {
                    var msc = EntityManager.GetComponentData<Game.Buildings.ModifiedServiceCoverage>(selectedEntity);
                    covRange = msc.m_Range;
                    covMag   = msc.m_Magnitude;
                }
                else if (prefab != Entity.Null && EntityManager.Exists(prefab) && EntityManager.HasComponent<CoverageData>(prefab))
                {
                    var cd = EntityManager.GetComponentData<CoverageData>(prefab);
                    covRange = cd.m_Range;
                    covMag   = cd.m_Magnitude;
                }

                // Modifiers — scan all entries, keep the one with the largest radius
                if (prefab != Entity.Null && EntityManager.Exists(prefab) && EntityManager.HasBuffer<LocalModifierData>(prefab))
                {
                    var mods = EntityManager.GetBuffer<LocalModifierData>(prefab);
                    for (int mi = 0; mi < mods.Length; mi++)
                    {
                        var m = mods[mi];
                        if (m.m_Radius.max > modRange)
                        {
                            modRange = m.m_Radius.max;
                            modMag   = m.m_Delta.max;
                        }
                    }
                }

                m_UISystem.UpdateBuildingData(buildingLabel, efficiency, covRange, covMag, modRange, modMag, sharedEffectsJson);
            }
            else
            {
                m_UISystem.UpdateBuildingData("", 0, 0f, 0f, 0f, 0f, "[]");
                m_UISystem.UpdateInspectorData("", "[]");
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
                    m_DeathcareLookups = GetComponentLookup<DeathcareFacilityData>(true),
 
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
                    m_LayerDeathcareEnabled = m_UISystem.TryGetGlobalLayerSetting("layer_deathcare", out var dc) && dc.Enabled,
 
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
                m_StatsPositionThrottleCounter++;
                if (m_StatsPositionThrottleCounter >= 2)
                {
                    m_StatsPositionThrottleCounter = 0;
                    UpdateFloatingStats();
                }
            }
            else if (m_FloatingStats.Count > 0)
            {
                m_FloatingStats.Clear();
                m_UISystem.UpdateFloatingStats("[]");
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
                case 14: return "layer_deathcare";
                default: return null;
            }
        }

        private static string GetLabelString(int type)
        {
            switch (type)
            {
                case 0: return "Healthcare";
                case 1: return "Well-being";
                case 2: return "Meals";
                case 3: return "Attractiveness";
                case 4: return "Telecom Range";
                case 7: return "Elementary";
                case 8: return "High School";
                case 9: return "College";
                case 10: return "University";
                case 11: return "Police Patrol";
                case 12: return "Fire Engines";
                case 13: return "Health";
                case 14: return "Deathcare";
                case 15: return "Post Vans";
                case 16: return "Crime Modifier";
                case 17: return "Fire Hazard";
                case 18: return "Fire Response";
                default: return "Healthcare";
            }
        }

        private static string GetIconString(int type)
        {
            switch (type)
            {
                case 0: return "Healthcare";
                case 1: return "Wellbeing";
                case 2: return "Meals";
                case 3: return "Parks";
                case 4: return "Telecom";
                case 7: return "Elementary";
                case 8: return "HighSchool";
                case 9: return "College";
                case 10: return "University";
                case 11: return "Police";
                case 12: return "Fire";
                case 13: return "Healthcare";
                case 14: return "Deathcare";
                case 15: return "Post";
                case 16: return "Crime";
                case 17: return "FireHazard";
                case 18: return "FireResponse";
                default: return "Healthcare";
            }
        }

        public void ClearStatsCache()
        {
            m_FramesSinceLastStatUpdate = 999; // Force update next frame
        }

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

        // ---- LOCAL ENTITY (Selected Building) ----

        private void ProcessLocalEntity(Entity entity, float3 position, bool isGhost, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) return;

            if (EntityManager.HasComponent<PrefabRef>(entity))
            {
                PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                ProcessLocalPrefab(prefabRef.m_Prefab, position, isGhost, ref overlayBuffer);
            }
            if (EntityManager.HasBuffer<Game.Buildings.InstalledUpgrade>(entity))
            {
                var upgrades = EntityManager.GetBuffer<Game.Buildings.InstalledUpgrade>(entity);
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.m_Upgrade != Entity.Null && EntityManager.Exists(upgrade.m_Upgrade))
                    {
                        ProcessLocalEntity(upgrade.m_Upgrade, position, isGhost, ref overlayBuffer);
                    }
                }
            }
        }

        private void ProcessLocalPrefab(Entity prefabEntity, float3 position, bool isGhost, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            if (EntityManager.HasComponent<CoverageData>(prefabEntity))
            {
                CoverageData coverageData = EntityManager.GetComponentData<CoverageData>(prefabEntity);
                string effectId = isGhost ? "PreplacementRing" : "CoverageData";
                string effectName = isGhost ? "Pre-placement Ring" : "General Coverage";
                UnityEngine.Color effectColor = isGhost ? new UnityEngine.Color(0.0f, 0.78f, 1.0f, 0.6f) : new UnityEngine.Color(0.15f, 0.8f, 0.3f, 0.5f);
                DrawLocalEffect(effectId, effectName, effectColor, coverageData.m_Range, position, ref overlayBuffer);
            }
            if (EntityManager.HasComponent<TelecomFacilityData>(prefabEntity))
            {
                var tel = EntityManager.GetComponentData<TelecomFacilityData>(prefabEntity);
                if (tel.m_Range > 0f)
                {
                    string effectId = isGhost ? "PreplacementRing" : "CoverageData";
                    string effectName = isGhost ? "Pre-placement Ring" : "General Coverage";
                    UnityEngine.Color telecomColor = isGhost ? new UnityEngine.Color(0.0f, 0.78f, 1.0f, 0.6f) : new UnityEngine.Color(0.0f, 0.85f, 1f, 0.5f);
                    DrawLocalEffect(effectId, effectName, telecomColor, tel.m_Range, position, ref overlayBuffer);
                }
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
                            UnityEngine.Color defaultColor = new UnityEngine.Color(0.9f, 0.1f, 0.75f, 0.5f);
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
                float renderRadius = math.min(radius, 1500f);
                overlayBuffer.DrawCircle(c, center, renderRadius);
            }
        }

        private void DrawGlobalRings(UnityEngine.Color baseColor, float3 position, float radius, float opacityMult, int preset, ref OverlayRenderSystem.Buffer overlayBuffer)
        {
            float heightOffset = Mod.Settings != null ? Mod.Settings.OverlayHeight : 1f;
            float3 center = position;
            center.y += heightOffset;

            UnityEngine.Color c1, c2, c3;
            float safeOpacity = math.clamp(opacityMult, 0.05f, 1f);
            float renderRadius = math.min(radius, 1500f);
            switch (preset)
            {
                case 1: // Soft Glow - multiple overlapping full circles
                    c1 = baseColor; c1.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.15f);
                    c2 = baseColor; c2.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.30f);
                    c3 = baseColor; c3.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.60f);
                    overlayBuffer.DrawCircle(c1, center, renderRadius);
                    overlayBuffer.DrawCircle(c2, center, renderRadius * 0.65f);
                    overlayBuffer.DrawCircle(c3, center, renderRadius * 0.20f);
                    break;

                case 2: // Classic - single ring
                    c1 = baseColor; c1.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.45f);
                    overlayBuffer.DrawCircle(c1, center, renderRadius);
                    break;

                default: // Rings - concentric, each slightly different
                    c1 = baseColor; c1.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.20f);
                    c2 = baseColor; c2.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.45f);
                    c3 = baseColor; c3.a = Mathf.Clamp01(baseColor.a * safeOpacity * 0.80f);
                    overlayBuffer.DrawCircle(c1, center, renderRadius);
                    overlayBuffer.DrawCircle(c2, center, renderRadius * 0.55f);
                    overlayBuffer.DrawCircle(c3, center, renderRadius * 0.12f);
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

        private Dictionary<string, string> m_LabelToLayerIdCache = new Dictionary<string, string>();
        private Dictionary<UnityEngine.Color, string> m_ColorToHexCache = new Dictionary<UnityEngine.Color, string>();

        private string GetLayerIdForLabel(string label)
        {
            if (m_LabelToLayerIdCache.TryGetValue(label, out var id)) return id;
            string newId;
            if (label == "Well-being" || label == "Meals") newId = "layer_wellbeing";
            else if (label == "Attractiveness") newId = "layer_parks";
            else if (label == "Telecom Range") newId = "layer_telecom";
            else if (label == "Elementary") newId = "layer_edu_elementary";
            else if (label == "High School") newId = "layer_edu_highschool";
            else if (label == "College") newId = "layer_edu_college";
            else if (label == "University") newId = "layer_edu_university";
            else if (label == "Police Patrol") newId = "layer_police";
            else if (label == "Fire Engines") newId = "layer_fire";
            else if (label == "Healthcare" || label == "Health") newId = "layer_healthcare";
            else if (label == "Deathcare") newId = "layer_deathcare";
            else if (label == "Post Vans") newId = "layer_post";
            else if (label == "Crime Modifier") newId = "layer_police";
            else if (label == "Fire Hazard" || label == "Fire Response") newId = "layer_fire";
            else newId = $"LocalModifier_{label}";
            m_LabelToLayerIdCache[label] = newId;
            return newId;
        }

        private string GetHexForColor(UnityEngine.Color color)
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
                var commercialPropertyLookup = GetComponentLookup<CommercialProperty>(true);
                var schoolLookup = GetComponentLookup<SchoolData>(true);
                var policeLookup = GetComponentLookup<PoliceStationData>(true);
                var fireLookup = GetComponentLookup<FireStationData>(true);
                var hospitalLookup = GetComponentLookup<HospitalData>(true);
                var deathcareLookup = GetComponentLookup<DeathcareFacilityData>(true);
                var postLookup = GetComponentLookup<PostFacilityData>(true);
                var installedUpgradeLookup = GetBufferLookup<InstalledUpgrade>(true);
                var prefabRefLookup = GetComponentLookup<PrefabRef>(true);

                localModifierLookup.Update(this);
                attractionLookup.Update(this);
                telecomLookup.Update(this);
                commercialPropertyLookup.Update(this);
                schoolLookup.Update(this);
                policeLookup.Update(this);
                fireLookup.Update(this);
                hospitalLookup.Update(this);
                deathcareLookup.Update(this);
                postLookup.Update(this);
                installedUpgradeLookup.Update(this);
                prefabRefLookup.Update(this);

                FindFloatingStatsJob job = new FindFloatingStatsJob
                {
                    m_PrefabType = GetComponentTypeHandle<PrefabRef>(true),
                    m_TransformType = GetComponentTypeHandle<Transform>(true),
                    m_EntityType = GetEntityTypeHandle(),
                    
                    m_LocalModifierLookup = localModifierLookup,
                    m_AttractionLookup = attractionLookup,
                    m_TelecomLookup = telecomLookup,
                    m_CommercialPropertyLookup = commercialPropertyLookup,
                    m_SchoolLookup = schoolLookup,
                    m_PoliceLookup = policeLookup,
                    m_FireLookup = fireLookup,
                    m_HospitalLookup = hospitalLookup,
                    m_DeathcareLookup = deathcareLookup,
                    m_PostLookup = postLookup,
                    m_InstalledUpgradeLookup = installedUpgradeLookup,
                    m_PrefabRefLookup = prefabRefLookup,
                    
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
            
            for (int i = 0; i < m_CachedStats.Count; i++) {
                var stat = m_CachedStats[i];
                string layerId = GetLayerIdForLabel(stat.label);
                
                bool isLayerEnabled = true;
                if (m_UISystem.TryGetGlobalLayerSetting(layerId, out var gs))
                {
                    if (!gs.Enabled) isLayerEnabled = false;
                }
                else if (m_UISystem.TryGetLocalEffectSetting(layerId, out var ls))
                {
                    if (!ls.Enabled) isLayerEnabled = false;
                }
                
                if (!isLayerEnabled) continue;

                if (!m_GroupedStats.TryGetValue(stat.worldPos, out var list))
                {
                    list = new List<AreaOfEffectUISystem.StatEntry>();
                    m_GroupedStats[stat.worldPos] = list;
                }
                
                string hex = "ffffff";
                if (m_UISystem.TryGetGlobalLayerSetting(layerId, out var gs2)) 
                    hex = GetHexForColor(gs2.Color);
                else if (m_UISystem.TryGetLocalEffectSetting(layerId, out var ls2))
                    hex = GetHexForColor(ls2.Color);

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
                if (float.IsNaN(viewportPos.x) || float.IsInfinity(viewportPos.x) || float.IsNaN(viewportPos.y) || float.IsInfinity(viewportPos.y) || float.IsNaN(viewportPos.z) || float.IsInfinity(viewportPos.z)) continue;

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

            string statsJson = SerializeFloatingStats(m_FloatingStats);
            m_UISystem.UpdateFloatingStats(statsJson);
        }

        private string SerializeFloatingStats(List<AreaOfEffectUISystem.FloatingStat> stats)
        {
            if (stats == null || stats.Count == 0) return "[]";
            var sb = new System.Text.StringBuilder(stats.Count * 250);
            sb.Append('[');
            for (int i = 0; i < stats.Count; i++)
            {
                var stat = stats[i];
                sb.Append("{\"x\":");
                sb.Append(stat.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"y\":");
                sb.Append(stat.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"z\":");
                sb.Append(stat.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"entries\":[");
                if (stat.entries != null)
                {
                    for (int j = 0; j < stat.entries.Count; j++)
                    {
                        var e = stat.entries[j];
                        sb.Append("{\"label\":\"");
                        sb.Append(e.label ?? "");
                        sb.Append("\",\"value\":");
                        sb.Append(e.value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        sb.Append(",\"icon\":\"");
                        sb.Append(e.icon ?? "");
                        sb.Append("\",\"color\":\"");
                        sb.Append(e.color ?? "#ffffff");
                        sb.Append(j < stat.entries.Count - 1 ? "\"}," : "\"}");
                    }
                }
                sb.Append(i < stats.Count - 1 ? "]}," : "]}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        private void GatherSelectedBuildingEffects(Entity entity, List<AreaOfEffectUISystem.BuildingEffectEntry> effectsList)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) return;

            if (EntityManager.HasComponent<PrefabRef>(entity))
            {
                PrefabRef prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                GatherPrefabEffects(prefabRef.m_Prefab, entity, effectsList);
            }

            if (EntityManager.HasBuffer<Game.Buildings.InstalledUpgrade>(entity))
            {
                var upgrades = EntityManager.GetBuffer<Game.Buildings.InstalledUpgrade>(entity);
                for (int i = 0; i < upgrades.Length; i++)
                {
                    var upgrade = upgrades[i];
                    if (upgrade.m_Upgrade != Entity.Null && EntityManager.Exists(upgrade.m_Upgrade))
                    {
                        GatherSelectedBuildingEffects(upgrade.m_Upgrade, effectsList);
                    }
                }
            }
        }

        private void GatherPrefabEffects(Entity prefabEntity, Entity liveEntity, List<AreaOfEffectUISystem.BuildingEffectEntry> effectsList)
        {
            if (prefabEntity == Entity.Null || !EntityManager.Exists(prefabEntity)) return;

            // 1. Coverage (CoverageData or ModifiedServiceCoverage)
            bool hasCoverage = false;
            float covRange = 0f;
            float covMag = 0f;
            string covService = "";

            if (liveEntity != Entity.Null && EntityManager.HasComponent<Game.Buildings.ModifiedServiceCoverage>(liveEntity))
            {
                var msc = EntityManager.GetComponentData<Game.Buildings.ModifiedServiceCoverage>(liveEntity);
                covRange = msc.m_Range;
                covMag = msc.m_Magnitude;
                hasCoverage = true;
            }

            if (EntityManager.HasComponent<CoverageData>(prefabEntity))
            {
                var cd = EntityManager.GetComponentData<CoverageData>(prefabEntity);
                if (!hasCoverage)
                {
                    covRange = cd.m_Range;
                    covMag = cd.m_Magnitude;
                    hasCoverage = true;
                }
                covService = cd.m_Service.ToString();
            }

            if (hasCoverage && covRange > 0f)
            {
                string layerId = GetLayerIdFromServiceName(covService);
                string colorHex = GetHexForLayerId(layerId);
                string serviceName = string.IsNullOrEmpty(covService) ? "General" : covService;
                AddEffectEntry(effectsList,
                    "Coverage",
                    $"{serviceName} Coverage",
                    $"+{covMag:0.#}",
                    $"{covRange:0.#} m",
                    GetIconForServiceName(covService),
                    colorHex
                );
            }

            // 2. Local Modifiers (LocalModifierData buffer)
            if (EntityManager.HasBuffer<LocalModifierData>(prefabEntity))
            {
                var modifiers = EntityManager.GetBuffer<LocalModifierData>(prefabEntity);
                for (int i = 0; i < modifiers.Length; i++)
                {
                    var mod = modifiers[i];
                    float val = (math.abs(mod.m_Delta.max) > math.abs(mod.m_Delta.min)) ? mod.m_Delta.max : mod.m_Delta.min;
                    if (math.abs(val) > 0.001f || mod.m_Radius.max > 0.001f)
                    {
                        string layerId = GetLayerIdForLocalModifier(mod.m_Type);
                        string colorHex = GetHexForLayerId(layerId);
                        string icon = GetIconForLocalModifier(mod.m_Type);

                        string rangeStr = "";
                        if (mod.m_Radius.max > 0f)
                        {
                            if (mod.m_Radius.min > 0f)
                                rangeStr = $"{mod.m_Radius.min:0.#} - {mod.m_Radius.max:0.#} m";
                            else
                                rangeStr = $"{mod.m_Radius.max:0.#} m";
                        }

                        string valStr = "";
                        if (mod.m_Type == Game.Buildings.LocalModifierType.Wellbeing || mod.m_Type == Game.Buildings.LocalModifierType.Health)
                        {
                            valStr = val > 0 ? $"+{val:0.#}" : $"{val:0.#}";
                        }
                        else
                        {
                            // Multiply by 100 to match game's percentage display (e.g. -0.8 -> -80%)
                            float pct = val * 100f;
                            valStr = pct > 0 ? $"+{pct:0.#} %" : $"{pct:0.#} %";
                        }

                        AddEffectEntry(effectsList,
                            "Local Modifier",
                            FormatLocalModifierName(mod.m_Type),
                            valStr,
                            rangeStr,
                            icon,
                            colorHex
                        );
                    }
                }
            }

            // 3. Attraction
            if (EntityManager.HasComponent<AttractionData>(prefabEntity))
            {
                var attr = EntityManager.GetComponentData<AttractionData>(prefabEntity);
                if (attr.m_Attractiveness > 0)
                {
                    string colorHex = GetHexForLayerId("layer_parks");
                    AddEffectEntry(effectsList,
                        "Attraction",
                        "Attractiveness",
                        $"+{attr.m_Attractiveness}",
                        "",
                        "Parks",
                        colorHex
                    );
                }
            }

            // 4. Leisure Provider
            if (EntityManager.HasComponent<LeisureProviderData>(prefabEntity))
            {
                var lp = EntityManager.GetComponentData<LeisureProviderData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_parks");
                AddEffectEntry(effectsList,
                    "Recreation",
                    FormatLeisureType(lp.m_LeisureType),
                    lp.m_Efficiency > 0 ? $"+{lp.m_Efficiency}" : lp.m_Efficiency.ToString(),
                    GetLeisureRangeNotes(lp.m_LeisureType),
                    "Parks",
                    colorHex
                );
            }

            // 5. Hospital Data
            if (EntityManager.HasComponent<HospitalData>(prefabEntity))
            {
                var hd = EntityManager.GetComponentData<HospitalData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_healthcare");
                if (hd.m_AmbulanceCapacity > 0)
                    AddEffectEntry(effectsList, "Healthcare", "Ambulance Capacity", hd.m_AmbulanceCapacity.ToString(), "", "Healthcare", colorHex);
                if (hd.m_MedicalHelicopterCapacity > 0)
                    AddEffectEntry(effectsList, "Healthcare", "Helicopter Capacity", hd.m_MedicalHelicopterCapacity.ToString(), "", "Healthcare", colorHex);
                if (hd.m_PatientCapacity > 0)
                    AddEffectEntry(effectsList, "Healthcare", "Patient Capacity", hd.m_PatientCapacity.ToString(), "", "Healthcare", colorHex);
                if (hd.m_TreatmentBonus > 0)
                    AddEffectEntry(effectsList, "Healthcare", "Treatment Bonus", $"+{hd.m_TreatmentBonus}", "", "Healthcare", colorHex);

                // Health Range
                AddEffectEntry(effectsList, "Healthcare", "Health Range", $"{hd.m_HealthRange.x} - {hd.m_HealthRange.y}", "", "Healthcare", colorHex);
            }



            // 7. Police Station Data
            if (EntityManager.HasComponent<PoliceStationData>(prefabEntity))
            {
                var pd = EntityManager.GetComponentData<PoliceStationData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_police");
                if (pd.m_PatrolCarCapacity > 0)
                    AddEffectEntry(effectsList, "Police", "Patrol Cars", pd.m_PatrolCarCapacity.ToString(), "", "Police", colorHex);
                if (pd.m_PoliceHelicopterCapacity > 0)
                    AddEffectEntry(effectsList, "Police", "Police Helicopters", pd.m_PoliceHelicopterCapacity.ToString(), "", "Police", colorHex);
                if (pd.m_JailCapacity > 0)
                    AddEffectEntry(effectsList, "Police", "Jail Capacity", pd.m_JailCapacity.ToString(), "", "Police", colorHex);
            }

            // 8. Fire Station Data
            if (EntityManager.HasComponent<FireStationData>(prefabEntity))
            {
                var fd = EntityManager.GetComponentData<FireStationData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_fire");
                if (fd.m_FireEngineCapacity > 0)
                    AddEffectEntry(effectsList, "Fire Protection", "Fire Engines", fd.m_FireEngineCapacity.ToString(), "", "Fire", colorHex);
                if (fd.m_FireHelicopterCapacity > 0)
                    AddEffectEntry(effectsList, "Fire Protection", "Fire Helicopters", fd.m_FireHelicopterCapacity.ToString(), "", "Fire", colorHex);
            }

            // 9. Deathcare Facility Data
            if (EntityManager.HasComponent<DeathcareFacilityData>(prefabEntity))
            {
                var dd = EntityManager.GetComponentData<DeathcareFacilityData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_deathcare");
                if (dd.m_HearseCapacity > 0)
                    AddEffectEntry(effectsList, "Deathcare", "Hearses", dd.m_HearseCapacity.ToString(), "", "Deathcare", colorHex);
            }

            // 10. Post Facility Data
            if (EntityManager.HasComponent<PostFacilityData>(prefabEntity))
            {
                var psd = EntityManager.GetComponentData<PostFacilityData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_post");
                if (psd.m_PostVanCapacity > 0)
                    AddEffectEntry(effectsList, "Postal Service", "Post Vans", psd.m_PostVanCapacity.ToString(), "", "Post", colorHex);
                if (psd.m_PostTruckCapacity > 0)
                    AddEffectEntry(effectsList, "Postal Service", "Post Trucks", psd.m_PostTruckCapacity.ToString(), "", "Post", colorHex);
                if (psd.m_MailCapacity > 0)
                    AddEffectEntry(effectsList, "Postal Service", "Mail Capacity", psd.m_MailCapacity.ToString(), "", "Post", colorHex);
            }

            // 11. Telecom Facility Data
            if (EntityManager.HasComponent<TelecomFacilityData>(prefabEntity))
            {
                var tel = EntityManager.GetComponentData<TelecomFacilityData>(prefabEntity);
                string colorHex = GetHexForLayerId("layer_telecom");
                if (tel.m_Range > 0f)
                    AddEffectEntry(effectsList, "Telecom", "Telecom Range", $"{tel.m_Range:0.#} m", "", "Telecom", colorHex);
            }

            // 12. Sub-infoview Items (PlaceableInfoviewItem buffer)
            if (EntityManager.HasBuffer<PlaceableInfoviewItem>(prefabEntity))
            {
                var items = EntityManager.GetBuffer<PlaceableInfoviewItem>(prefabEntity);
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.m_Item != Entity.Null && EntityManager.Exists(item.m_Item))
                    {
                        if (EntityManager.HasComponent<InfoviewCoverageData>(item.m_Item))
                        {
                            var icd = EntityManager.GetComponentData<InfoviewCoverageData>(item.m_Item);
                            string serviceName = icd.m_Service.ToString();
                            string layerId = GetLayerIdFromServiceName(serviceName);
                            string colorHex = GetHexForLayerId(layerId);
                            string icon = GetIconForServiceName(serviceName);

                            // Bounds1 range
                            string rangeStr = "";
                            if (icd.m_Range.max > 0f)
                            {
                                if (icd.m_Range.min > 0f)
                                    rangeStr = $"{icd.m_Range.min:0.#} - {icd.m_Range.max:0.#} m";
                                else
                                    rangeStr = $"{icd.m_Range.max:0.#} m";
                            }

                            AddEffectEntry(effectsList,
                                "Sub-Coverage",
                                $"{serviceName} Coverage",
                                "",
                                rangeStr,
                                icon,
                                colorHex
                            );
                        }
                    }
                }
            }

            // 13. City Modifiers (CityModifierData buffer)
            if (EntityManager.HasBuffer<Game.Prefabs.CityModifierData>(prefabEntity))
            {
                var cityModifiers = EntityManager.GetBuffer<Game.Prefabs.CityModifierData>(prefabEntity);
                for (int i = 0; i < cityModifiers.Length; i++)
                {
                    var modifier = cityModifiers[i];
                    float val = (math.abs(modifier.m_Range.max) > math.abs(modifier.m_Range.min)) ? modifier.m_Range.max : modifier.m_Range.min;
                    if (math.abs(val) > 0.0001f)
                    {
                        string valStr = "";
                        if (modifier.m_Mode == Game.Prefabs.ModifierValueMode.Relative || modifier.m_Mode == Game.Prefabs.ModifierValueMode.InverseRelative)
                        {
                            float pct = val * 100f;
                            valStr = pct > 0 ? $"+{pct:0.#} %" : $"{pct:0.#} %";
                        }
                        else
                        {
                            valStr = val > 0 ? $"+{val:0.#}" : $"{val:0.#}";
                        }

                        string nameStr = FormatCityModifierName(modifier.m_Type);
                        string iconStr = GetIconForCityModifier(modifier.m_Type);
                        string colorHex = GetHexForCityModifier(modifier.m_Type);

                        AddEffectEntry(effectsList,
                            "City Modifier",
                            nameStr,
                            valStr,
                            "City-wide",
                            iconStr,
                            colorHex
                        );
                    }
                }
            }
        }

        private void AddEffectEntry(List<AreaOfEffectUISystem.BuildingEffectEntry> list, string group, string name, string val, string rangeStr, string icon, string colorHex)
        {
            string finalColor = colorHex;
            if (!string.IsNullOrEmpty(finalColor) && !finalColor.StartsWith("#"))
            {
                finalColor = "#" + finalColor;
            }
            list.Add(new AreaOfEffectUISystem.BuildingEffectEntry {
                group = group,
                name = name,
                value = val,
                range = rangeStr,
                icon = icon,
                color = finalColor
            });
        }

        private string GetHexForLayerId(string layerId)
        {
            if (m_UISystem.TryGetGlobalLayerSetting(layerId, out var gs))
                return GetHexForColor(gs.Color);
            if (m_UISystem.TryGetLocalEffectSetting(layerId, out var ls))
                return GetHexForColor(ls.Color);
            return "ffffff";
        }

        private string GetLayerIdFromServiceName(string service)
        {
            switch (service)
            {
                case "Healthcare": return "layer_healthcare";
                case "Deathcare": return "layer_deathcare";
                case "Police": return "layer_police";
                case "Fire": return "layer_fire";
                case "Park": return "layer_parks";
                case "Telecom": return "layer_telecom";
                case "Post": return "layer_post";
                case "Education": return "layer_edu_elementary";
                default: return "CoverageData";
            }
        }

        private string GetIconForServiceName(string service)
        {
            switch (service)
            {
                case "Healthcare": return "Healthcare";
                case "Deathcare": return "Deathcare";
                case "Police": return "Police";
                case "Fire": return "Fire";
                case "Park": return "Parks";
                case "Telecom": return "Telecom";
                case "Post": return "Post";
                default: return "Healthcare";
            }
        }

        private string GetLayerIdForLocalModifier(Game.Buildings.LocalModifierType type)
        {
            switch (type)
            {
                case Game.Buildings.LocalModifierType.Wellbeing: return "layer_wellbeing";
                case Game.Buildings.LocalModifierType.Health: return "layer_healthcare";
                case Game.Buildings.LocalModifierType.CrimeAccumulation: return "layer_police";
                case Game.Buildings.LocalModifierType.ForestFireResponseTime: return "layer_fire";
                case Game.Buildings.LocalModifierType.ForestFireHazard: return "layer_fire";
                default: return "layer_wellbeing";
            }
        }

        private string GetIconForLocalModifier(Game.Buildings.LocalModifierType type)
        {
            switch (type)
            {
                case Game.Buildings.LocalModifierType.Wellbeing: return "Wellbeing";
                case Game.Buildings.LocalModifierType.Health: return "Healthcare";
                case Game.Buildings.LocalModifierType.CrimeAccumulation: return "Crime";
                case Game.Buildings.LocalModifierType.ForestFireResponseTime: return "FireResponse";
                case Game.Buildings.LocalModifierType.ForestFireHazard: return "FireHazard";
                default: return "Wellbeing";
            }
        }

        private string FormatLocalModifierName(Game.Buildings.LocalModifierType type)
        {
            switch (type)
            {
                case Game.Buildings.LocalModifierType.Wellbeing: return "Well-being Modifier";
                case Game.Buildings.LocalModifierType.Health: return "Health Modifier";
                case Game.Buildings.LocalModifierType.CrimeAccumulation: return "Crime Accumulation";
                case Game.Buildings.LocalModifierType.ForestFireResponseTime: return "Forest Fire Response";
                case Game.Buildings.LocalModifierType.ForestFireHazard: return "Forest Fire Hazard";
                default: return type.ToString();
            }
        }

        private string FormatLeisureType(Game.Agents.LeisureType type)
        {
            switch (type)
            {
                case Game.Agents.LeisureType.Meals: return "Meals";
                case Game.Agents.LeisureType.Entertainment: return "Entertainment";
                case Game.Agents.LeisureType.Commercial: return "Commercial";
                case Game.Agents.LeisureType.CityIndoors: return "Indoor Recreation";
                case Game.Agents.LeisureType.Travel: return "Travel";
                case Game.Agents.LeisureType.CityPark: return "Outdoor Recreation";
                case Game.Agents.LeisureType.CityBeach: return "Beach Recreation";
                case Game.Agents.LeisureType.Attractions: return "Attractions";
                case Game.Agents.LeisureType.Relaxation: return "Relaxation";
                case Game.Agents.LeisureType.Sightseeing: return "Sightseeing";
                default: return type.ToString();
            }
        }

        private string GetLeisureRangeNotes(Game.Agents.LeisureType type)
        {
            switch (type)
            {
                case Game.Agents.LeisureType.CityPark:
                case Game.Agents.LeisureType.CityBeach:
                    return "Season dependent";
                case Game.Agents.LeisureType.CityIndoors:
                    return "Year-round";
                default:
                    return "";
            }
        }

        private string GetSchoolLayerId(int level)
        {
            switch (level)
            {
                case 1: return "layer_edu_elementary";
                case 2: return "layer_edu_highschool";
                case 3: return "layer_edu_college";
                default: return "layer_edu_university";
            }
        }

        private string GetSchoolIcon(int level)
        {
            switch (level)
            {
                case 1: return "Elementary";
                case 2: return "HighSchool";
                case 3: return "College";
                default: return "University";
            }
        }

        private string SerializeBuildingEffects(List<AreaOfEffectUISystem.BuildingEffectEntry> effects)
        {
            if (effects == null || effects.Count == 0) return "[]";
            var sb = new System.Text.StringBuilder(effects.Count * 200);
            sb.Append('[');
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                sb.Append("{\"group\":\"");
                sb.Append(AreaOfEffectUISystem.EscapeJsonString(e.group ?? ""));
                sb.Append("\",\"name\":\"");
                sb.Append(AreaOfEffectUISystem.EscapeJsonString(e.name ?? ""));
                sb.Append("\",\"value\":\"");
                sb.Append(AreaOfEffectUISystem.EscapeJsonString(e.value ?? ""));
                sb.Append("\",\"range\":\"");
                sb.Append(AreaOfEffectUISystem.EscapeJsonString(e.range ?? ""));
                sb.Append("\",\"icon\":\"");
                sb.Append(AreaOfEffectUISystem.EscapeJsonString(e.icon ?? ""));
                sb.Append("\",\"color\":\"");
                sb.Append(AreaOfEffectUISystem.EscapeJsonString(e.color ?? "#ffffff"));
                sb.Append(i < effects.Count - 1 ? "\"}," : "\"}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        private string FormatCityModifierName(Game.City.CityModifierType type)
        {
            switch (type)
            {
                case Game.City.CityModifierType.CityServiceBuildingBaseUpkeepCost: return "Service Base Upkeep";
                case Game.City.CityModifierType.CityServiceImportCost: return "Service Import Cost";
                case Game.City.CityModifierType.BuildingLevelingCost: return "Building Leveling Cost";
                case Game.City.CityModifierType.CrimeResponseTime: return "Crime Response Time";
                case Game.City.CityModifierType.CrimeProbability: return "Crime Probability";
                case Game.City.CityModifierType.CrimeAccumulation: return "Crime Accumulation";
                case Game.City.CityModifierType.DisasterWarningTime: return "Disaster Warning Time";
                case Game.City.CityModifierType.DisasterDamageRate: return "Disaster Damage Rate";
                case Game.City.CityModifierType.DiseaseProbability: return "Disease Probability";
                case Game.City.CityModifierType.HospitalEfficiency: return "Hospital Efficiency";
                case Game.City.CityModifierType.IndustrialEfficiency: return "Industrial Efficiency";
                case Game.City.CityModifierType.OfficeEfficiency: return "Office Efficiency";
                case Game.City.CityModifierType.UniversityInterest: return "University Interest";
                case Game.City.CityModifierType.UniversityGraduation: return "University Graduation";
                case Game.City.CityModifierType.CollegeGraduation: return "College Graduation";
                case Game.City.CityModifierType.TelecomCapacity: return "Telecom Capacity";
                case Game.City.CityModifierType.Entertainment: return "Entertainment Modifier";
                case Game.City.CityModifierType.ParkEntertainment: return "Park Entertainment";
                case Game.City.CityModifierType.ImportCost: return "Import Cost";
                case Game.City.CityModifierType.ExportCost: return "Export Cost";
                case Game.City.CityModifierType.LoanInterest: return "Loan Interest";
                case Game.City.CityModifierType.TaxHappiness: return "Tax Happiness";
                default:
                    return type.ToString().Replace("CityService", "").Replace("Industrial", "Industrial ").Replace("Office", "Office ");
            }
        }

        private string GetIconForCityModifier(Game.City.CityModifierType type)
        {
            switch (type)
            {
                case Game.City.CityModifierType.CityServiceBuildingBaseUpkeepCost:
                case Game.City.CityModifierType.CityServiceImportCost:
                case Game.City.CityModifierType.BuildingLevelingCost:
                case Game.City.CityModifierType.ImportCost:
                case Game.City.CityModifierType.ExportCost:
                case Game.City.CityModifierType.LoanInterest:
                    return "efficiency";
                
                case Game.City.CityModifierType.CrimeResponseTime:
                case Game.City.CityModifierType.CrimeProbability:
                case Game.City.CityModifierType.CrimeAccumulation:
                case Game.City.CityModifierType.PrisonTime:
                case Game.City.CityModifierType.CriminalMonitorProbability:
                    return "police";
                
                case Game.City.CityModifierType.DisasterWarningTime:
                case Game.City.CityModifierType.DisasterDamageRate:
                    return "firehazard";
                
                case Game.City.CityModifierType.DiseaseProbability:
                case Game.City.CityModifierType.PollutionHealthAffect:
                case Game.City.CityModifierType.HospitalEfficiency:
                    return "healthcare";
                
                case Game.City.CityModifierType.UniversityInterest:
                case Game.City.CityModifierType.UniversityGraduation:
                case Game.City.CityModifierType.CollegeGraduation:
                    return "education";
                
                case Game.City.CityModifierType.TelecomCapacity:
                    return "telecom";
                
                case Game.City.CityModifierType.Entertainment:
                case Game.City.CityModifierType.ParkEntertainment:
                    return "parks";
                
                case Game.City.CityModifierType.IndustrialEfficiency:
                case Game.City.CityModifierType.OfficeEfficiency:
                case Game.City.CityModifierType.IndustrialAirPollution:
                case Game.City.CityModifierType.IndustrialGroundPollution:
                case Game.City.CityModifierType.IndustrialGarbage:
                    return "pollution";

                default:
                    return "wellbeing";
            }
        }

        private string GetHexForCityModifier(Game.City.CityModifierType type)
        {
            switch (type)
            {
                case Game.City.CityModifierType.CityServiceBuildingBaseUpkeepCost:
                case Game.City.CityModifierType.CityServiceImportCost:
                case Game.City.CityModifierType.BuildingLevelingCost:
                case Game.City.CityModifierType.ImportCost:
                case Game.City.CityModifierType.ExportCost:
                case Game.City.CityModifierType.LoanInterest:
                    return GetHexForLayerId("layer_post"); // Yellow/Golden
                
                case Game.City.CityModifierType.CrimeResponseTime:
                case Game.City.CityModifierType.CrimeProbability:
                case Game.City.CityModifierType.CrimeAccumulation:
                case Game.City.CityModifierType.PrisonTime:
                case Game.City.CityModifierType.CriminalMonitorProbability:
                    return GetHexForLayerId("layer_police");
                
                case Game.City.CityModifierType.DisasterWarningTime:
                case Game.City.CityModifierType.DisasterDamageRate:
                    return GetHexForLayerId("layer_fire");
                
                case Game.City.CityModifierType.DiseaseProbability:
                case Game.City.CityModifierType.PollutionHealthAffect:
                case Game.City.CityModifierType.HospitalEfficiency:
                    return GetHexForLayerId("layer_healthcare");
                
                case Game.City.CityModifierType.UniversityInterest:
                case Game.City.CityModifierType.UniversityGraduation:
                case Game.City.CityModifierType.CollegeGraduation:
                    return GetHexForLayerId("layer_edu_university");
                
                case Game.City.CityModifierType.TelecomCapacity:
                    return GetHexForLayerId("layer_telecom");
                
                case Game.City.CityModifierType.Entertainment:
                case Game.City.CityModifierType.ParkEntertainment:
                    return GetHexForLayerId("layer_parks");
                
                default:
                    return "00A2E8"; // Theme blue
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
