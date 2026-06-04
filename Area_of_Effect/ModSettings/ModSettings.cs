using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace Area_of_Effect.ModSettings
{
    [FileLocation("ModsSettings/Area_of_Effect/Area_of_Effect")]
    [SettingsUIGroupOrder(kMainGroup)]
    [SettingsUIShowGroupName(kMainGroup)]
    public class ModSettings : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "AreaOfEffectSettings";


        public enum VisualPreset { Rings, Glow, Classic }

        public ModSettings(IMod mod) : base(mod) { SetDefaults(); }

        [SettingsUISection(kSection, kMainGroup)]
        public bool IsEnabled { get; set; } = true;



        [SettingsUISlider(min = 10, max = 100, step = 10, scalarMultiplier = 1, unit = Unit.kPercentage)]
        [SettingsUISection(kSection, kMainGroup)]
        public int Opacity { get; set; } = 50;

        [SettingsUISection(kSection, kMainGroup)]
        public VisualPreset Preset { get; set; } = VisualPreset.Rings;

        [SettingsUISlider(min = 10, max = 200, step = 5)]
        [SettingsUISection(kSection, kMainGroup)]
        public int GlobalCircleSize { get; set; } = 100;

        [SettingsUISlider(min = 0, max = 100, step = 1)]
        [SettingsUISection(kSection, kMainGroup)]
        public int OverlayHeight { get; set; } = 1;

        [SettingsUISlider(min = 1500, max = 3600, step = 50)]
        [SettingsUISection(kSection, kMainGroup)]
        public int LabelDistance { get; set; } = 1500;

        [SettingsUISlider(min = 50, max = 200, step = 5)]
        [SettingsUISection(kSection, kMainGroup)]
        public int BubbleSize { get; set; } = 100;

        [SettingsUISection(kSection, kMainGroup)]
        public bool ShowStats { get; set; } = false;

        [SettingsUISection(kSection, kMainGroup)]
        public bool HighVis { get; set; } = false;

        [SettingsUISection(kSection, kMainGroup)]
        public bool ShowOnHover { get; set; } = false;

        [SettingsUISection(kSection, kMainGroup)]
        public bool EnablePreplacement { get; set; } = true;

        [SettingsUISection(kSection, kMainGroup)]
        public bool EnableMiniInspector { get; set; } = true;

        public override void SetDefaults()
        {
            IsEnabled = true;
            Opacity = 50;
            Preset = VisualPreset.Rings;
            GlobalCircleSize = 100;
            OverlayHeight = 1;
            LabelDistance = 1500;
            BubbleSize = 100;
            ShowStats = false;
            HighVis = false;
            ShowOnHover = false;
            EnablePreplacement = true;
            EnableMiniInspector = true;
            InspectorX = 100f;
            InspectorY = 300f;
            ActiveMode = 0;
            SavedLocalSettings = "";
            SavedGlobalSettings = "";
            WindowX = 100f;
            WindowY = 100f;
            SavedPresets = "[]";
        }

        [SettingsUIHidden]
        public int ActiveMode { get; set; } = 0;

        [SettingsUIHidden]
        public string SavedLocalSettings { get; set; } = "";

        [SettingsUIHidden]
        public string SavedGlobalSettings { get; set; } = "";

        [SettingsUIHidden]
        public float WindowX { get; set; } = 100f;

        [SettingsUIHidden]
        public float WindowY { get; set; } = 100f;

        [SettingsUIHidden]
        public string SavedPresets { get; set; } = "[]";

        [SettingsUIHidden]
        public float InspectorX { get; set; } = 100f;

        [SettingsUIHidden]
        public float InspectorY { get; set; } = 300f;
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly ModSettings m_Setting;
        public LocaleEN(ModSettings setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Area of Effect" },
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainGroup), "Area of Effect Settings" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.IsEnabled)), "Enable Mod" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.IsEnabled)), "Turn the Area of Effect overlay on or off." },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.Opacity)), "Overlay Opacity" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.Opacity)), "Adjust the transparency of area of effect circles." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.Preset)), "Visual Preset" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.Preset)), "Choose the ring rendering style." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.GlobalCircleSize)), "Global Circle Size (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.GlobalCircleSize)), "Adjust the size of the global service indicators." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OverlayHeight)), "Overlay Height (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.OverlayHeight)), "Adjust the height offset of rendering overlays above ground." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShowStats)), "Show Floating Stats" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShowStats)), "Display real-time service bonuses (+Wellbeing, etc.) over buildings." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LabelDistance)), "Label Visibility Distance (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LabelDistance)), "How far labels are visible when zooming out." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.BubbleSize)), "Floating Stats Scale" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.BubbleSize)), "Adjust the size of floating statistic bubbles." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HighVis)), "High Visibility Mode" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HighVis)), "Boost bubble contrast and font sizes for easier reading." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShowOnHover)), "Show Overlay on Hover" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShowOnHover)), "Show the AoE radius overlay when hovering over a building." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnablePreplacement)), "Enable Pre-placement Ring" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnablePreplacement)), "Draw the range boundary ring on terrain during building placement." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableMiniInspector)), "Enable Mini-Inspector" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableMiniInspector)), "Show a compact mini-inspector window when hovering or selecting a building." },
                { m_Setting.GetEnumValueLocaleID(ModSettings.VisualPreset.Rings), "Neon Rings" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.VisualPreset.Glow), "Soft Glow" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.VisualPreset.Classic), "Classic" },
            };
        }

        public void Unload() { }
    }
}
