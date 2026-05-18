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
        private RawValueBinding m_FloatingStatsBinding;
        private ValueBinding<string> m_SelectedBuildingNameBinding;
        private ValueBinding<int> m_SelectedBuildingEfficiencyBinding;
        private ValueBinding<int> m_SelectedBuildingWellbeingBinding;
        private ValueBinding<int> m_SelectedBuildingServiceReachBinding;

        private List<FloatingStat> m_CurrentStats = new List<FloatingStat>();

        protected override void OnCreate()
        {
            base.OnCreate();

            int opacity = Mod.Settings?.Opacity ?? 50;
            int preset  = (int)(Mod.Settings?.Preset ?? ModSettings.ModSettings.VisualPreset.Classic);

            AddBinding(m_LocalSettingsBinding   = new ValueBinding<string>("area_of_effect", "localSettings", "[]"));
            AddBinding(m_GlobalSettingsBinding  = new ValueBinding<string>("area_of_effect", "globalSettings", "[]"));
            AddBinding(m_IsPanelOpenBinding     = new ValueBinding<bool>  ("area_of_effect", "isPanelOpen", false));
            AddBinding(m_OpacityBinding         = new ValueBinding<float> ("area_of_effect", "opacity", (float)opacity));
            AddBinding(m_PresetBinding          = new ValueBinding<int>   ("area_of_effect", "preset", preset));
            AddBinding(m_SizeBinding            = new ValueBinding<float> ("area_of_effect", "circleSize", (float)(Mod.Settings?.GlobalCircleSize ?? 100)));
            AddBinding(m_HeightBinding          = new ValueBinding<float> ("area_of_effect", "overlayHeight", (float)(Mod.Settings?.OverlayHeight ?? 1)));
            AddBinding(m_ShowStatsBinding       = new ValueBinding<bool>  ("area_of_effect", "showStats", Mod.Settings?.ShowStats ?? true));
            AddBinding(m_MaxDistanceBinding     = new ValueBinding<float> ("area_of_effect", "maxDistance", (float)(Mod.Settings?.LabelDistance ?? 1500)));
            AddBinding(m_HighVisBinding         = new ValueBinding<bool>  ("area_of_effect", "highVis", Mod.Settings?.HighVis ?? false));
            AddBinding(m_FloatingStatsBinding   = new RawValueBinding     ("area_of_effect", "floatingStats", WriteFloatingStats));
            AddBinding(m_SelectedBuildingNameBinding = new ValueBinding<string>("area_of_effect", "buildingName", ""));
            AddBinding(m_SelectedBuildingEfficiencyBinding = new ValueBinding<int>("area_of_effect", "buildingEfficiency", 0));
            AddBinding(m_SelectedBuildingWellbeingBinding = new ValueBinding<int>("area_of_effect", "buildingWellbeing", 0));
            AddBinding(m_SelectedBuildingServiceReachBinding = new ValueBinding<int>("area_of_effect", "buildingServiceReach", 0));

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
            


            AddBinding(new TriggerBinding<int>("area_of_effect", "setPreset", (p) => {
                int safeP = Mathf.Clamp(p, 0, 2);
                m_PresetBinding.Update(safeP);
                if (Mod.Settings != null) { Mod.Settings.Preset = (ModSettings.ModSettings.VisualPreset)safeP; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setSize", (s) => {
                float safeS = Mathf.Clamp(s, 10f, 500f);
                m_SizeBinding.Update(safeS);
                if (Mod.Settings != null) { Mod.Settings.GlobalCircleSize = (int)safeS; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setHeight", (h) => {
                float safeH = Mathf.Clamp(h, 0f, 100f);
                m_HeightBinding.Update(safeH);
                if (Mod.Settings != null) { Mod.Settings.OverlayHeight = (int)safeH; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setOpacity", (o) => {
                float safeO = Mathf.Clamp(o, 10f, 100f);
                m_OpacityBinding.Update(safeO);
                if (Mod.Settings != null) { Mod.Settings.Opacity = (int)safeO; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<bool>("area_of_effect", "setShowStats", (val) => {
                m_ShowStatsBinding.Update(val);
                if (Mod.Settings != null) { Mod.Settings.ShowStats = val; Mod.Settings.ApplyAndSave(); }
            }));
            AddBinding(new TriggerBinding<float>("area_of_effect", "setMaxDistance", (d) => {
                float safeD = Mathf.Clamp(d, 1000f, 3500f);
                m_MaxDistanceBinding.Update(safeD);
                if (Mod.Settings != null) { Mod.Settings.LabelDistance = (int)safeD; Mod.Settings.ApplyAndSave(); }
                World.GetOrCreateSystemManaged<AreaOfEffectSystem>().ClearStatsCache();
            }));
            AddBinding(new TriggerBinding<bool>("area_of_effect", "setHighVis", (v) => {
                m_HighVisBinding.Update(v);
                if (Mod.Settings != null) { Mod.Settings.HighVis = v; Mod.Settings.ApplyAndSave(); }
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
        }

        public void UpdateBuildingData(string name, int efficiency, int wellbeing, int reach)
        {
            m_SelectedBuildingNameBinding.Update(name);
            m_SelectedBuildingEfficiencyBinding.Update(efficiency);
            m_SelectedBuildingWellbeingBinding.Update(wellbeing);
            m_SelectedBuildingServiceReachBinding.Update(reach);
        }

        public void UpdateFloatingStats(List<FloatingStat> stats)
        {
            m_CurrentStats = stats;
            m_FloatingStatsBinding?.Update();
        }

        private void WriteFloatingStats(IJsonWriter writer)
        {
            writer.ArrayBegin(m_CurrentStats.Count);
            foreach (var stat in m_CurrentStats)
            {
                stat.Write(writer);
            }
            writer.ArrayEnd();
        }

        public void RegisterLocalEffectType(string id, string name, UnityEngine.Color defaultColor)
        {
            if (m_LocalSettings.ContainsKey(id)) return;
            m_LocalSettings[id] = new EffectSetting { Id = id, Name = name, Enabled = true, Color = defaultColor, Opacity = 1.0f };
            m_LocalSettingsBinding?.Update(SerializeDict(m_LocalSettings));
        }

        public void RegisterGlobalLayer(string id, string name, UnityEngine.Color defaultColor)
        {
            if (m_GlobalSettings.ContainsKey(id)) return;
            m_GlobalSettings[id] = new EffectSetting { Id = id, Name = name, Enabled = true, Color = defaultColor, Opacity = 1.0f };
            m_GlobalSettingsBinding?.Update(SerializeDict(m_GlobalSettings));
        }

        public void EnsureWellbeingRegistered()
        {
            RegisterGlobalLayer("layer_wellbeing", "Well-being", new Color(0.3f, 1f, 0.3f, 1f));
            RegisterGlobalLayer("layer_police", "Police Coverage", new Color(0.2f, 0.4f, 1f, 1f));
            RegisterGlobalLayer("layer_fire", "Fire Protection", new Color(1f, 0.3f, 0.2f, 1f));
            RegisterGlobalLayer("layer_parks", "Parks & Recreation", new Color(0.2f, 1f, 0.4f, 1f));
            RegisterGlobalLayer("layer_healthcare", "Healthcare", new Color(1f, 0.5f, 0.5f, 1f));
            RegisterGlobalLayer("layer_telecom", "Telecom", new Color(0.5f, 0.8f, 1f, 1f));
            RegisterGlobalLayer("layer_post", "Postal Service", new Color(1f, 0.8f, 0.2f, 1f));
            RegisterGlobalLayer("layer_edu_elementary", "Education: Elementary", new Color(1f, 1f, 0.5f, 1f));
            RegisterGlobalLayer("layer_edu_highschool", "Education: High School", new Color(1f, 0.8f, 0.4f, 1f));
            RegisterGlobalLayer("layer_edu_college", "Education: College", new Color(1f, 0.6f, 0.3f, 1f));
            RegisterGlobalLayer("layer_edu_university", "Education: University", new Color(1f, 0.4f, 0.2f, 1f));
            RegisterGlobalLayer("layer_pollution", "Pollution Producer", new Color(0.5f, 0.4f, 0.2f, 1f));

            // Pre-register all Local Effects so that they are always configurable and visible in the UI settings panel
            RegisterLocalEffectType("CoverageData", "Coverage", new Color(0f, 1f, 0f, 0.5f));
            RegisterLocalEffectType("LocalModifier_Wellbeing", "Well-being Modifier", new Color(0f, 1f, 0f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_Crime", "Crime Modifier", new Color(1f, 0f, 0f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_Health", "Health Modifier", new Color(1f, 0.5f, 0.5f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_ForestFireHazard", "Forest Fire Hazard", new Color(1f, 0.5f, 0f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_GroundPollution", "Ground Pollution", new Color(0.5f, 0.3f, 0f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_AirPollution", "Air Pollution", new Color(0.4f, 0.4f, 0.4f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_NoisePollution", "Noise Pollution", new Color(0.8f, 0.4f, 0.2f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_CrimeAccumulation", "Crime Accumulation", new Color(0.7f, 0.1f, 0.1f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_MailAccumulation", "Mail Accumulation", new Color(0.9f, 0.7f, 0.1f, 0.5f));
            // RegisterLocalEffectType("LocalModifier_GarbageAccumulation", "Garbage Accumulation", new Color(0.3f, 0.3f, 0.3f, 0.5f));
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

        private void ToggleLocal(string id, bool on) { if (m_LocalSettings.TryGetValue(id, out var s)) { s.Enabled = on; m_LocalSettingsBinding.Update(SerializeDict(m_LocalSettings)); } }
        private void ToggleAllLocal(bool on) { foreach (var kv in m_LocalSettings) kv.Value.Enabled = on; m_LocalSettingsBinding.Update(SerializeDict(m_LocalSettings)); }
        private void SetLocalColor(string id, string hex) { if (m_LocalSettings.TryGetValue(id, out var s) && ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = 1f; s.Color = c; m_LocalSettingsBinding.Update(SerializeDict(m_LocalSettings)); } }
        private void SetLocalAlpha(string id, float a) { if (m_LocalSettings.TryGetValue(id, out var s)) { s.Opacity = Mathf.Clamp01(a); m_LocalSettingsBinding.Update(SerializeDict(m_LocalSettings)); } }

        private void ToggleGlobal(string id, bool on) { if (m_GlobalSettings.TryGetValue(id, out var s)) { s.Enabled = on; m_GlobalSettingsBinding.Update(SerializeDict(m_GlobalSettings)); } }
        private void ToggleAllGlobal(bool on) { foreach (var kv in m_GlobalSettings) kv.Value.Enabled = on; m_GlobalSettingsBinding.Update(SerializeDict(m_GlobalSettings)); }
        private void SetGlobalColor(string id, string hex) { if (m_GlobalSettings.TryGetValue(id, out var s) && ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = 1f; s.Color = c; m_GlobalSettingsBinding.Update(SerializeDict(m_GlobalSettings)); } }
        private void SetGlobalAlpha(string id, float a) { if (m_GlobalSettings.TryGetValue(id, out var s)) { s.Opacity = Mathf.Clamp01(a); m_GlobalSettingsBinding.Update(SerializeDict(m_GlobalSettings)); } }

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
    }
}
