using System.Collections.Generic;
using Colossal.UI.Binding;
using Game.UI;
using UnityEngine;
using Unity.Mathematics;

namespace Area_of_Effect
{
    public partial class AreaOfEffectUISystem : UISystemBase
    {
        public class EffectSetting
        {
            public string Id;
            public string Name;
            public bool Enabled;
            public Color Color;
            public float Opacity; // 0..1
        }

        public struct StatEntry : IJsonWritable
        {
            public string label;
            public float value;
            public string icon;
            public string color;

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin("StatEntry");
                writer.PropertyName("label");
                writer.Write(label);
                writer.PropertyName("value");
                writer.Write(value);
                writer.PropertyName("icon");
                writer.Write(icon ?? "");
                writer.PropertyName("color");
                writer.Write(color ?? "#ffffff");
                writer.TypeEnd();
            }
        }

        public struct BuildingEffectEntry : IJsonWritable
        {
            public string group;
            public string name;
            public string value;
            public string range;
            public string icon;
            public string color;

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin("BuildingEffectEntry");
                writer.PropertyName("group");
                writer.Write(group ?? "");
                writer.PropertyName("name");
                writer.Write(name ?? "");
                writer.PropertyName("value");
                writer.Write(value ?? "");
                writer.PropertyName("range");
                writer.Write(range ?? "");
                writer.PropertyName("icon");
                writer.Write(icon ?? "");
                writer.PropertyName("color");
                writer.Write(color ?? "#ffffff");
                writer.TypeEnd();
            }
        }

        public struct FloatingStat : IJsonWritable
        {
            public List<StatEntry> entries;
            public float x; // Screen % 0..100
            public float y; // Screen % 0..100
            public float z; // Depth from camera

            public void Write(IJsonWriter writer)
            {
                writer.TypeBegin("FloatingStat");
                writer.PropertyName("entries");
                writer.ArrayBegin(entries?.Count ?? 0);
                if (entries != null) foreach (var e in entries) e.Write(writer);
                writer.ArrayEnd();
                writer.PropertyName("x");
                writer.Write(x);
                writer.PropertyName("y");
                writer.Write(y);
                writer.PropertyName("z");
                writer.Write(z);
                writer.TypeEnd();
            }
        }

        private Dictionary<string, EffectSetting> m_LocalSettings = new Dictionary<string, EffectSetting>();
        private Dictionary<string, EffectSetting> m_GlobalSettings = new Dictionary<string, EffectSetting>();
        private bool m_LocalDirty = true;
        private bool m_GlobalDirty = true;

        private ValueBinding<string> m_LocalSettingsBinding;
        private ValueBinding<string> m_GlobalSettingsBinding;
        private ValueBinding<bool> m_IsPanelOpenBinding;
        private ValueBinding<float> m_OpacityBinding;
        private ValueBinding<int> m_PresetBinding;
        private ValueBinding<float> m_SizeBinding;
        private ValueBinding<float> m_HeightBinding;
        private ValueBinding<bool> m_ShowStatsBinding;
        private ValueBinding<float> m_MaxDistanceBinding;
        private ValueBinding<bool> m_HighVisBinding;
        private ValueBinding<float> m_BubbleSizeBinding;
        private ValueBinding<string> m_FloatingStatsBinding;
        private ValueBinding<string> m_SelectedBuildingNameBinding;
        private ValueBinding<int>    m_SelectedBuildingEfficiencyBinding;
        private ValueBinding<float>  m_CoverageRangeBinding;
        private ValueBinding<float>  m_CoverageMagnitudeBinding;
        private ValueBinding<float>  m_ModifierRangeBinding;
        private ValueBinding<float>  m_ModifierMagnitudeBinding;
        private ValueBinding<string> m_SelectedBuildingEffectsBinding;

        private ValueBinding<string> m_PresetsBinding;
        private ValueBinding<float> m_WindowXBinding;
        private ValueBinding<float> m_WindowYBinding;

        private ValueBinding<bool>   m_EnableMiniInspectorBinding;
        private ValueBinding<float>  m_InspectorXBinding;
        private ValueBinding<float>  m_InspectorYBinding;
        private ValueBinding<string> m_InspectorNameBinding;
        private ValueBinding<string> m_InspectorEffectsJsonBinding;

        public class Preset
        {
            public int Slot;
            public string Name;
            public string LocalData;
            public string GlobalData;
            public bool IsFilled;
        }
        private List<Preset> m_Presets = new List<Preset>();

        protected override void OnCreate()
        {
            base.OnCreate();

            int opacity = Mod.Settings?.Opacity ?? 50;
            int preset  = (int)(Mod.Settings?.Preset ?? ModSettings.ModSettings.VisualPreset.Classic);

            AddBinding(m_LocalSettingsBinding   = new ValueBinding<string>("area_of_effect", "localSettings", "[]"));
            AddBinding(m_GlobalSettingsBinding  = new ValueBinding<string>("area_of_effect", "globalSettings", "[]"));
            LoadSavedSettings();
            m_LocalSettingsBinding.Update(SerializeDict(m_LocalSettings));
            m_GlobalSettingsBinding.Update(SerializeDict(m_GlobalSettings));
            AddBinding(m_IsPanelOpenBinding     = new ValueBinding<bool>  ("area_of_effect", "isPanelOpen", false));
            AddBinding(m_OpacityBinding         = new ValueBinding<float> ("area_of_effect", "opacity", (float)opacity));
            AddBinding(m_PresetBinding          = new ValueBinding<int>   ("area_of_effect", "preset", preset));
            AddBinding(m_SizeBinding            = new ValueBinding<float> ("area_of_effect", "circleSize", (float)(Mod.Settings?.GlobalCircleSize ?? 100)));
            AddBinding(m_HeightBinding          = new ValueBinding<float> ("area_of_effect", "overlayHeight", (float)(Mod.Settings?.OverlayHeight ?? 1)));
            AddBinding(m_ShowStatsBinding       = new ValueBinding<bool>  ("area_of_effect", "showStats", Mod.Settings?.ShowStats ?? true));
            AddBinding(m_MaxDistanceBinding     = new ValueBinding<float> ("area_of_effect", "maxDistance", (float)(Mod.Settings?.LabelDistance ?? 1500)));
            AddBinding(m_HighVisBinding         = new ValueBinding<bool>  ("area_of_effect", "highVis", Mod.Settings?.HighVis ?? false));
            AddBinding(m_BubbleSizeBinding       = new ValueBinding<float> ("area_of_effect", "bubbleSize", (float)(Mod.Settings?.BubbleSize ?? 100)));
            AddBinding(m_FloatingStatsBinding   = new ValueBinding<string>("area_of_effect", "floatingStatsJson", "[]"));
            AddBinding(m_SelectedBuildingNameBinding       = new ValueBinding<string>("area_of_effect", "buildingName",          ""));
            AddBinding(m_SelectedBuildingEfficiencyBinding  = new ValueBinding<int>   ("area_of_effect", "buildingEfficiency",    0));
            AddBinding(m_CoverageRangeBinding               = new ValueBinding<float> ("area_of_effect", "coverageRange",         0f));
            AddBinding(m_CoverageMagnitudeBinding           = new ValueBinding<float> ("area_of_effect", "coverageMagnitude",     0f));
            AddBinding(m_ModifierRangeBinding               = new ValueBinding<float> ("area_of_effect", "modifierRange",         0f));
            AddBinding(m_ModifierMagnitudeBinding           = new ValueBinding<float> ("area_of_effect", "modifierMagnitude",     0f));
            AddBinding(m_SelectedBuildingEffectsBinding     = new ValueBinding<string>("area_of_effect", "buildingEffectsJson",   "[]"));

            InitializePresets();
            AddBinding(m_PresetsBinding = new ValueBinding<string>("area_of_effect", "presetsJson", SerializePresetsForUI()));
            AddBinding(m_WindowXBinding = new ValueBinding<float>("area_of_effect", "windowX", Mod.Settings != null ? Mod.Settings.WindowX : 100f));
            AddBinding(m_WindowYBinding = new ValueBinding<float>("area_of_effect", "windowY", Mod.Settings != null ? Mod.Settings.WindowY : 100f));

            AddBinding(m_EnableMiniInspectorBinding   = new ValueBinding<bool>  ("area_of_effect", "enableMiniInspector",   Mod.Settings != null ? Mod.Settings.EnableMiniInspector : true));
            AddBinding(m_InspectorXBinding            = new ValueBinding<float> ("area_of_effect", "inspectorX",            Mod.Settings != null ? Mod.Settings.InspectorX : 100f));
            AddBinding(m_InspectorYBinding            = new ValueBinding<float> ("area_of_effect", "inspectorY",            Mod.Settings != null ? Mod.Settings.InspectorY : 300f));
            AddBinding(m_InspectorNameBinding         = new ValueBinding<string>("area_of_effect", "inspectorName",         ""));
            AddBinding(m_InspectorEffectsJsonBinding  = new ValueBinding<string>("area_of_effect", "inspectorEffectsJson", "[]"));

            // Triggers
            AddBinding(new TriggerBinding<string, bool>  ("area_of_effect", "toggleLocalEffect",    ToggleLocal));
            AddBinding(new TriggerBinding<string, string>("area_of_effect", "setLocalEffectColor",  SetLocalColor));
            AddBinding(new TriggerBinding<string, float> ("area_of_effect", "setLocalEffectAlpha",  SetLocalAlpha));
            AddBinding(new TriggerBinding<bool>          ("area_of_effect", "bulkToggleLocal",      ToggleAllLocal));

            AddBinding(new TriggerBinding<string, bool>  ("area_of_effect", "toggleGlobalLayer",   ToggleGlobal));
            AddBinding(new TriggerBinding<string, string>("area_of_effect", "setGlobalLayerColor",  SetGlobalColor));
            AddBinding(new TriggerBinding<string, float> ("area_of_effect", "setGlobalLayerAlpha",  SetGlobalAlpha));
            AddBinding(new TriggerBinding<bool>          ("area_of_effect", "bulkToggleGlobal",     ToggleAllGlobal));

            AddBinding(new TriggerBinding("area_of_effect", "togglePanel", () => {
                m_IsPanelOpenBinding.Update(!m_IsPanelOpenBinding.value);
            }));
            AddBinding(new TriggerBinding<float, float>("area_of_effect", "saveWindowPosition", SaveWindowPosition));
            AddBinding(new TriggerBinding("area_of_effect", "resetColorsOnly", ResetColorsOnly));
            AddBinding(new TriggerBinding("area_of_effect", "resetConfigOnly", ResetConfigOnly));
            AddBinding(new TriggerBinding<int, string>("area_of_effect", "savePreset", SavePreset));
            AddBinding(new TriggerBinding<int>("area_of_effect", "loadPreset", LoadPreset));
            AddBinding(new TriggerBinding<int>("area_of_effect", "deletePreset", DeletePreset));
            
            AddBinding(new TriggerBinding<bool>("area_of_effect", "setEnableMiniInspector", (val) => {
                m_EnableMiniInspectorBinding.Update(val);
                if (Mod.Settings != null) { Mod.Settings.EnableMiniInspector = val; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float, float>("area_of_effect", "saveInspectorPosition", (x, y) => {
                if (Mod.Settings != null)
                {
                    Mod.Settings.InspectorX = x;
                    Mod.Settings.InspectorY = y;
                    Mod.Settings.ApplyAndSave();
                }
            }));
            


            AddBinding(new TriggerBinding<int>("area_of_effect", "setPreset", (p) => {
                int safeP = Mathf.Clamp(p, 0, 2);
                m_PresetBinding.Update(safeP);
                if (Mod.Settings != null) { Mod.Settings.Preset = (ModSettings.ModSettings.VisualPreset)safeP; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setSize", (s) => {
                float safeS = Mathf.Clamp(s, 10f, 200f);
                m_SizeBinding.Update(safeS);
                if (Mod.Settings != null) { Mod.Settings.GlobalCircleSize = (int)safeS; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setHeight", (h) => {
                float safeH = Mathf.Clamp(h, 0f, 100f);
                m_HeightBinding.Update(safeH);
                if (Mod.Settings != null) { Mod.Settings.OverlayHeight = (int)safeH; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setOpacity", (o) => {
                float safeO = Mathf.Clamp(o, 0f, 100f);
                m_OpacityBinding.Update(safeO);
                if (Mod.Settings != null) { Mod.Settings.Opacity = (int)safeO; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<bool>("area_of_effect", "setShowStats", (val) => {
                m_ShowStatsBinding.Update(val);
                if (Mod.Settings != null) { Mod.Settings.ShowStats = val; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setMaxDistance", (d) => {
                float safeD = Mathf.Clamp(d, 1000f, 3600f);
                m_MaxDistanceBinding.Update(safeD);
                if (Mod.Settings != null) { Mod.Settings.LabelDistance = (int)safeD; Mod.Settings.ApplyAndSave(); }
                World.GetOrCreateSystemManaged<AreaOfEffectSystem>().ClearStatsCache();
            }));
            AddBinding(new TriggerBinding<bool>("area_of_effect", "setHighVis", (v) => {
                m_HighVisBinding.Update(v);
                if (Mod.Settings != null) { Mod.Settings.HighVis = v; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setBubbleSize", (b) => {
                float safeB = Mathf.Clamp(b, 50f, 200f);
                m_BubbleSizeBinding.Update(safeB);
                if (Mod.Settings != null) { Mod.Settings.BubbleSize = (int)safeB; Mod.Settings.ApplyAndSave(); }
            }));
        }

        protected override void OnUpdate()
        {
            if (Mod.Settings == null) return;
            int p = (int)Mod.Settings.Preset;
            if (m_PresetBinding.value != p)
                m_PresetBinding.Update(p);
            if (m_OpacityBinding.value != (float)Mod.Settings.Opacity)
                m_OpacityBinding.Update((float)Mod.Settings.Opacity);
            if (m_SizeBinding.value != (float)Mod.Settings.GlobalCircleSize)
                m_SizeBinding.Update((float)Mod.Settings.GlobalCircleSize);
            if (m_HeightBinding.value != (float)Mod.Settings.OverlayHeight)
                m_HeightBinding.Update((float)Mod.Settings.OverlayHeight);
            if (m_ShowStatsBinding.value != Mod.Settings.ShowStats)
                m_ShowStatsBinding.Update(Mod.Settings.ShowStats);
            if (m_MaxDistanceBinding.value != (float)Mod.Settings.LabelDistance)
                m_MaxDistanceBinding.Update((float)Mod.Settings.LabelDistance);
            if (m_HighVisBinding.value != Mod.Settings.HighVis)
                m_HighVisBinding.Update(Mod.Settings.HighVis);
            if (m_BubbleSizeBinding.value != (float)Mod.Settings.BubbleSize)
                m_BubbleSizeBinding.Update((float)Mod.Settings.BubbleSize);
            // Flush layer settings bindings only when data actually changed
            if (m_LocalDirty)
            {
                m_LocalSettingsBinding?.Update(SerializeDict(m_LocalSettings));
                m_LocalDirty = false;
            }
            if (m_GlobalDirty)
            {
                m_GlobalSettingsBinding?.Update(SerializeDict(m_GlobalSettings));
                m_GlobalDirty = false;
            }
            if (m_WindowXBinding.value != Mod.Settings.WindowX)
                m_WindowXBinding.Update(Mod.Settings.WindowX);
            if (m_WindowYBinding.value != Mod.Settings.WindowY)
                m_WindowYBinding.Update(Mod.Settings.WindowY);

            if (m_EnableMiniInspectorBinding.value != Mod.Settings.EnableMiniInspector)
                m_EnableMiniInspectorBinding.Update(Mod.Settings.EnableMiniInspector);
            if (m_InspectorXBinding.value != Mod.Settings.InspectorX)
                m_InspectorXBinding.Update(Mod.Settings.InspectorX);
            if (m_InspectorYBinding.value != Mod.Settings.InspectorY)
                m_InspectorYBinding.Update(Mod.Settings.InspectorY);
        }

        public void UpdateBuildingData(string name, int efficiency, float covRange, float covMag, float modRange, float modMag, string effectsJson)
        {
            m_SelectedBuildingNameBinding.Update(name ?? "");
            m_SelectedBuildingEfficiencyBinding.Update(efficiency);
            m_CoverageRangeBinding.Update(covRange);
            m_CoverageMagnitudeBinding.Update(covMag);
            m_ModifierRangeBinding.Update(modRange);
            m_ModifierMagnitudeBinding.Update(modMag);
            m_SelectedBuildingEffectsBinding.Update(effectsJson ?? "[]");
        }

        public void UpdateFloatingStats(string statsJson)
        {
            m_FloatingStatsBinding?.Update(statsJson ?? "[]");
        }

        public void UpdateInspectorData(string name, string effectsJson)
        {
            m_InspectorNameBinding?.Update(name ?? "");
            m_InspectorEffectsJsonBinding?.Update(effectsJson ?? "[]");
        }

        public void RegisterLocalEffectType(string id, string name, UnityEngine.Color defaultColor)
        {
            if (m_LocalSettings.ContainsKey(id)) {
                m_LocalSettings[id].Name = name;
                // Name-only update: mark dirty so the binding flushes next frame
                m_LocalDirty = true;
                return;
            }
            m_LocalSettings[id] = new EffectSetting { Id = id, Name = name, Enabled = true, Color = defaultColor, Opacity = 1.0f };
            m_LocalDirty = true;
        }

        public void RegisterGlobalLayer(string id, string name, UnityEngine.Color defaultColor)
        {
            if (m_GlobalSettings.ContainsKey(id)) {
                m_GlobalSettings[id].Name = name;
                m_GlobalDirty = true;
                return;
            }
            m_GlobalSettings[id] = new EffectSetting { Id = id, Name = name, Enabled = true, Color = defaultColor, Opacity = 1.0f };
            m_GlobalDirty = true;
        }

        public void EnsureWellbeingRegistered()
        {
            RegisterGlobalLayer("layer_wellbeing",       "Well-being",          new Color(0.9f,  0.1f,  0.75f, 1f));  // Magenta
            RegisterGlobalLayer("layer_police",           "Police Coverage",     new Color(0.15f, 0.5f,  1.0f,  1f));  // Royal Blue
            RegisterGlobalLayer("layer_fire",             "Fire Protection",     new Color(1f,    0.25f, 0.1f,  1f));  // Red-Orange
            RegisterGlobalLayer("layer_parks",            "Parks & Recreation",  new Color(0.15f, 0.85f, 0.3f,  1f));  // Green
            RegisterGlobalLayer("layer_healthcare",       "Healthcare",          new Color(1f,    0.35f, 0.35f, 1f));  // Coral Red (distinct from magenta Well-being)
            RegisterGlobalLayer("layer_deathcare",        "Deathcare",           new Color(0.3f,  0.7f,  0.55f, 1f));  // Teal (distinct from all others)
            RegisterGlobalLayer("layer_telecom",          "Telecom",             new Color(0.0f,  0.85f, 1f,    1f));  // Cyan
            RegisterGlobalLayer("layer_post",             "Postal Service",      new Color(1f,    0.85f, 0.0f,  1f));  // Yellow
            RegisterGlobalLayer("layer_edu_elementary",   "Education: Elementary",   new Color(0.55f, 0.9f,  0.1f,  1f));  // Yellow-Green
            RegisterGlobalLayer("layer_edu_highschool",   "Education: High School",  new Color(1f,    0.5f,  0.05f, 1f));  // Orange
            RegisterGlobalLayer("layer_edu_college",      "Education: College",      new Color(0.35f, 0.35f, 1f,    1f));  // Indigo Blue
            RegisterGlobalLayer("layer_edu_university",   "Education: University",   new Color(0.65f, 0.15f, 0.95f, 1f));  // Purple

            RegisterLocalEffectType("CoverageData",                       "General Coverage",           new Color(0.15f, 0.8f, 0.3f, 0.5f));
            RegisterLocalEffectType("PreplacementRing",                   "Pre-placement Ring",         new Color(0.0f, 0.78f, 1.0f, 0.6f));
            RegisterLocalEffectType("LocalModifier_Wellbeing",            "Well-being Modifier",        new Color(0.9f, 0.1f, 0.75f, 0.5f));
            RegisterLocalEffectType("LocalModifier_ElementaryEducation",  "Elementary Education",       new Color(0.6f, 0.9f, 0.1f, 0.5f));
            RegisterLocalEffectType("LocalModifier_HighSchoolEducation",  "High School Education",      new Color(1f, 0.55f, 0.05f, 0.5f));
            RegisterLocalEffectType("LocalModifier_HigherEducation",      "Higher Education",           new Color(0.35f, 0.35f, 1f, 0.5f));
        }

        public bool TryGetLocalEffectSetting(string id, out EffectSetting s) => m_LocalSettings.TryGetValue(id, out s);
        public bool TryGetGlobalLayerSetting(string id, out EffectSetting s) => m_GlobalSettings.TryGetValue(id, out s);

        public bool IsAnyGlobalLayerActive()
        {
            foreach (var kv in m_GlobalSettings)
            {
                if (kv.Value.Enabled) return true;
            }
            return false;
        }

        private static string GetSafePath(string folder, string filename)
        {
            try
            {
                string baseDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(baseDir)) return null;
                string targetDir = System.IO.Path.Combine(baseDir + "Low", "Colossal Order", "Cities Skylines II", folder);
                if (!System.IO.Directory.Exists(targetDir))
                {
                    System.IO.Directory.CreateDirectory(targetDir);
                }
                return System.IO.Path.Combine(targetDir, filename);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Error creating safe path: " + ex.Message);
                return null;
            }
        }

        private static readonly string LocalSettingsFilePath = GetSafePath("ModsSettings\\Area_of_Effect", "Area_of_Effect_local.json");
        private static readonly string GlobalSettingsFilePath = GetSafePath("ModsSettings\\Area_of_Effect", "Area_of_Effect_global.json");
        private static readonly string PresetsFilePath = GetSafePath("ModsSettings\\Area_of_Effect", "Area_of_Effect_presets.json");

        private void SaveLocalSettings()
        {
            if (string.IsNullOrEmpty(LocalSettingsFilePath)) return;
            string serialized = SerializeDict(m_LocalSettings);
            m_LocalSettingsBinding?.Update(serialized);
            m_LocalDirty = false; // just flushed — suppress next-frame redundant serialize
            try
            {
                System.IO.File.WriteAllText(LocalSettingsFilePath, serialized);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Error saving local settings file: " + ex.Message);
            }
            if (Mod.Settings != null)
            {
                Mod.Settings.SavedLocalSettings = serialized;
                Mod.Settings.ApplyAndSave();
            }
        }

        private void SaveGlobalSettings()
        {
            if (string.IsNullOrEmpty(GlobalSettingsFilePath)) return;
            string serialized = SerializeDict(m_GlobalSettings);
            m_GlobalSettingsBinding?.Update(serialized);
            m_GlobalDirty = false; // just flushed — suppress next-frame redundant serialize
            try
            {
                System.IO.File.WriteAllText(GlobalSettingsFilePath, serialized);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Error saving global settings file: " + ex.Message);
            }
            if (Mod.Settings != null)
            {
                Mod.Settings.SavedGlobalSettings = serialized;
                Mod.Settings.ApplyAndSave();
            }
        }

        private void ToggleLocal(string id, bool on) { if (m_LocalSettings.TryGetValue(id, out var s)) { s.Enabled = on; m_LocalDirty = true; SaveLocalSettings(); } }
        private void ToggleAllLocal(bool on) { foreach (var kv in m_LocalSettings) kv.Value.Enabled = on; m_LocalDirty = true; SaveLocalSettings(); }
        private void SetLocalColor(string id, string hex) { if (m_LocalSettings.TryGetValue(id, out var s) && ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = 1f; s.Color = c; m_LocalDirty = true; SaveLocalSettings(); } }
        private void SetLocalAlpha(string id, float a) { if (m_LocalSettings.TryGetValue(id, out var s)) { s.Opacity = Mathf.Clamp01(a); m_LocalDirty = true; SaveLocalSettings(); } }

        private void ToggleGlobal(string id, bool on) { if (m_GlobalSettings.TryGetValue(id, out var s)) { s.Enabled = on; m_GlobalDirty = true; SaveGlobalSettings(); } }
        private void ToggleAllGlobal(bool on) { foreach (var kv in m_GlobalSettings) kv.Value.Enabled = on; m_GlobalDirty = true; SaveGlobalSettings(); }
        private void SetGlobalColor(string id, string hex) { if (m_GlobalSettings.TryGetValue(id, out var s) && ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = 1f; s.Color = c; m_GlobalDirty = true; SaveGlobalSettings(); } }
        private void SetGlobalAlpha(string id, float a) { if (m_GlobalSettings.TryGetValue(id, out var s)) { s.Opacity = Mathf.Clamp01(a); m_GlobalDirty = true; SaveGlobalSettings(); } }

        private void ResetColorsOnly()
        {
            m_LocalSettings.Clear();
            m_GlobalSettings.Clear();
            EnsureWellbeingRegistered();
            SaveLocalSettings();
            SaveGlobalSettings();
            m_LocalDirty = true;
            m_GlobalDirty = true;
        }

        private void ResetConfigOnly()
        {
            string localBackup = SerializeDict(m_LocalSettings);
            string globalBackup = SerializeDict(m_GlobalSettings);

            if (Mod.Settings != null)
            {
                Mod.Settings.SetDefaults();
                Mod.Settings.SavedLocalSettings = localBackup;
                Mod.Settings.SavedGlobalSettings = globalBackup;
                Mod.Settings.ApplyAndSave();
            }

            // Sync UI bindings for all config variables immediately
            OnUpdate();
        }

        private void SaveWindowPosition(float x, float y)
        {
            if (Mod.Settings != null)
            {
                Mod.Settings.WindowX = x;
                Mod.Settings.WindowY = y;
                Mod.Settings.ApplyAndSave();
            }
        }

        private void InitializePresets()
        {
            m_Presets.Clear();
            for (int i = 0; i < 5; i++)
            {
                m_Presets.Add(new Preset { Slot = i, Name = $"Preset {i + 1}", LocalData = "", GlobalData = "", IsFilled = false });
            }
            LoadPresets();
        }

        private string SerializePresets()
        {
            var parts = new List<string>();
            foreach (var p in m_Presets)
            {
                string escName = EscapeJsonString(p.Name);
                string escLocal = EscapeJsonString(p.LocalData);
                string escGlobal = EscapeJsonString(p.GlobalData);
                parts.Add($"{{\"slot\":{p.Slot},\"name\":\"{escName}\",\"isFilled\":{(p.IsFilled ? "true" : "false")},\"localData\":\"{escLocal}\",\"globalData\":\"{escGlobal}\"}}");
            }
            return "[" + string.Join(",", parts) + "]";
        }

        private string SerializePresetsForUI()
        {
            var parts = new List<string>();
            foreach (var p in m_Presets)
            {
                string escName = EscapeJsonString(p.Name);
                parts.Add($"{{\"slot\":{p.Slot},\"name\":\"{escName}\",\"isFilled\":{(p.IsFilled ? "true" : "false")}}}");
            }
            return "[" + string.Join(",", parts) + "]";
        }

        private void SavePreset(int slot, string name)
        {
            if (slot < 0 || slot >= 5) return;
            m_Presets[slot].Slot = slot;
            m_Presets[slot].Name = string.IsNullOrEmpty(name) ? $"Preset {slot + 1}" : name;
            m_Presets[slot].IsFilled = true;
            m_Presets[slot].LocalData = SerializeDict(m_LocalSettings);
            m_Presets[slot].GlobalData = SerializeDict(m_GlobalSettings);

            try
            {
                if (!string.IsNullOrEmpty(PresetsFilePath))
                {
                    System.IO.File.WriteAllText(PresetsFilePath, SerializePresets());
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[AoE] Error saving presets to file: " + ex.Message);
            }

            m_PresetsBinding.Update(SerializePresetsForUI());
        }

        private void LoadPreset(int slot)
        {
            if (slot < 0 || slot >= 5 || !m_Presets[slot].IsFilled) return;

            m_LocalSettings.Clear();
            m_GlobalSettings.Clear();

            LoadSettingsDict(m_Presets[slot].LocalData, m_LocalSettings);
            LoadSettingsDict(m_Presets[slot].GlobalData, m_GlobalSettings);

            EnsureWellbeingRegistered();

            SaveLocalSettings();
            SaveGlobalSettings();

            m_LocalDirty = true;
            m_GlobalDirty = true;
        }

        private void DeletePreset(int slot)
        {
            if (slot < 0 || slot >= 5) return;
            m_Presets[slot].Name = $"Preset {slot + 1}";
            m_Presets[slot].IsFilled = false;
            m_Presets[slot].LocalData = "";
            m_Presets[slot].GlobalData = "";

            try
            {
                if (!string.IsNullOrEmpty(PresetsFilePath))
                {
                    System.IO.File.WriteAllText(PresetsFilePath, SerializePresets());
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[AoE] Error deleting preset: " + ex.Message);
            }

            m_PresetsBinding.Update(SerializePresetsForUI());
        }

        private void LoadPresets()
        {
            if (string.IsNullOrEmpty(PresetsFilePath) || !System.IO.File.Exists(PresetsFilePath)) return;
            try
            {
                string content = System.IO.File.ReadAllText(PresetsFilePath).Trim();
                if (content.StartsWith("[")) content = content.Substring(1);
                if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1);

                string[] objects = content.Split(new string[] { "},{" }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string obj in objects)
                {
                    string cleanObj = obj.Trim('{', '}');
                    string slotStr = ExtractJsonValue(cleanObj, "slot");
                    if (string.IsNullOrEmpty(slotStr)) continue;

                    int slot = 0;
                    if (int.TryParse(slotStr, out slot) && slot >= 0 && slot < 5)
                    {
                        string name = UnescapeJsonString(ExtractJsonValue(cleanObj, "name"));
                        string isFilledStr = ExtractJsonValue(cleanObj, "isFilled");
                        string localData = UnescapeJsonString(ExtractJsonValue(cleanObj, "localData"));
                        string globalData = UnescapeJsonString(ExtractJsonValue(cleanObj, "globalData"));

                        m_Presets[slot].Name = name;
                        m_Presets[slot].IsFilled = isFilledStr == "true";
                        m_Presets[slot].LocalData = localData;
                        m_Presets[slot].GlobalData = globalData;
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[AoE] Error loading presets: " + ex.Message);
            }
        }

        public static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string UnescapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
        }

        private string SerializeDict(Dictionary<string, EffectSetting> d)
        {
            var parts = new List<string>();
            foreach (var kv in d)
            {
                var s = kv.Value;
                string r = s.Color.r.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string g = s.Color.g.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string b = s.Color.b.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string a = s.Color.a.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string o = s.Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
                parts.Add($"{{\"id\":\"{s.Id}\",\"name\":\"{s.Name}\",\"enabled\":{(s.Enabled ? "true" : "false")},\"r\":{r},\"g\":{g},\"b\":{b},\"a\":{a},\"opacity\":{o}}}");
            }
            return "[" + string.Join(",", parts) + "]";
        }

        private void LoadSavedSettings()
        {
            string localStr = "";
            string globalStr = "";
            try
            {
                UnityEngine.Debug.Log($"[AoE] Loading saved settings from LocalPath: {LocalSettingsFilePath}, GlobalPath: {GlobalSettingsFilePath}");
                if (!string.IsNullOrEmpty(LocalSettingsFilePath) && System.IO.File.Exists(LocalSettingsFilePath))
                {
                    localStr = System.IO.File.ReadAllText(LocalSettingsFilePath);
                    UnityEngine.Debug.Log($"[AoE] Read local settings content: {localStr}");
                }
                if (!string.IsNullOrEmpty(GlobalSettingsFilePath) && System.IO.File.Exists(GlobalSettingsFilePath))
                {
                    globalStr = System.IO.File.ReadAllText(GlobalSettingsFilePath);
                    UnityEngine.Debug.Log($"[AoE] Read global settings content: {globalStr}");
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[AoE] Error reading saved settings files: " + ex.Message);
            }

            if (string.IsNullOrEmpty(localStr) && Mod.Settings != null)
            {
                localStr = Mod.Settings.SavedLocalSettings;
                UnityEngine.Debug.Log($"[AoE] Fallback local settings from Mod.Settings: {localStr}");
            }
            if (string.IsNullOrEmpty(globalStr) && Mod.Settings != null)
            {
                globalStr = Mod.Settings.SavedGlobalSettings;
                UnityEngine.Debug.Log($"[AoE] Fallback global settings from Mod.Settings: {globalStr}");
            }

            LoadSettingsDict(localStr, m_LocalSettings);
            LoadSettingsDict(globalStr, m_GlobalSettings);
        }

        private void LoadSettingsDict(string savedStr, Dictionary<string, EffectSetting> dict)
        {
            if (string.IsNullOrEmpty(savedStr) || savedStr == "[]") return;
            try
            {
                string content = savedStr.Trim();
                if (content.StartsWith("[")) content = content.Substring(1);
                if (content.EndsWith("]")) content = content.Substring(0, content.Length - 1);

                string[] objects = content.Split(new string[] { "},{" }, System.StringSplitOptions.RemoveEmptyEntries);
                UnityEngine.Debug.Log($"[AoE] Parsing {objects.Length} settings objects...");
                foreach (string obj in objects)
                {
                    string cleanObj = obj.Trim('{', '}');
                    string id = ExtractJsonValue(cleanObj, "id");
                    if (string.IsNullOrEmpty(id)) continue;

                    string name = ExtractJsonValue(cleanObj, "name");
                    string enabledStr = ExtractJsonValue(cleanObj, "enabled");
                    string rStr = ExtractJsonValue(cleanObj, "r");
                    string gStr = ExtractJsonValue(cleanObj, "g");
                    string bStr = ExtractJsonValue(cleanObj, "b");
                    string aStr = ExtractJsonValue(cleanObj, "a");
                    string opacityStr = ExtractJsonValue(cleanObj, "opacity");

                    bool enabled = enabledStr == "true";
                    
                    float r = 1f, g = 1f, b = 1f, a = 1f;
                    float.TryParse(rStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r);
                    float.TryParse(gStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g);
                    float.TryParse(bStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b);
                    float.TryParse(aStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a);

                    float opacity = 1.0f;
                    float.TryParse(opacityStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out opacity);

                    dict[id] = new EffectSetting
                    {
                        Id = id,
                        Name = string.IsNullOrEmpty(name) ? id : name,
                        Enabled = enabled,
                        Color = new Color(r, g, b, a),
                        Opacity = opacity
                    };
                    UnityEngine.Debug.Log($"[AoE] Loaded Setting: id={id}, name={name}, enabled={enabled}, color=({r},{g},{b},{a}), opacity={opacity}");
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("[AoE] Error loading saved Area of Effect layer settings: " + ex.Message);
            }
        }

        private string ExtractJsonValue(string objStr, string key)
        {
            if (string.IsNullOrEmpty(objStr) || string.IsNullOrEmpty(key)) return "";
            string pattern = "\"" + key + "\":";
            int idx = objStr.IndexOf(pattern);
            if (idx == -1)
            {
                pattern = key + ":";
                idx = objStr.IndexOf(pattern);
                if (idx == -1) return "";
            }

            int startIdx = idx + pattern.Length;
            if (startIdx >= objStr.Length) return "";

            string result;
            if (objStr[startIdx] == '"')
            {
                startIdx++;
                int endIdx = objStr.IndexOf('"', startIdx);
                if (endIdx == -1) return "";
                result = objStr.Substring(startIdx, endIdx - startIdx);
            }
            else
            {
                int endIdx = objStr.IndexOf(',', startIdx);
                if (endIdx == -1)
                {
                    result = objStr.Substring(startIdx).Trim();
                }
                else
                {
                    result = objStr.Substring(startIdx, endIdx - startIdx).Trim();
                }
            }
            return result ?? "";
        }
    }
}
