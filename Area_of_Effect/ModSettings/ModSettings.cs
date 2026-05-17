using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace Area_of_Effect.ModSettings
{
    [FileLocation(nameof(Area_of_Effect))]
    [SettingsUIGroupOrder(kMainGroup)]
    [SettingsUIShowGroupName(kMainGroup)]
    public class ModSettings : ModSetting
    {
        public const string kSection = "Main";
        public const string kMainGroup = "AreaOfEffectSettings";

        public enum NavBarSide { Left, Right }
        public enum VisualPreset { Rings, Glow, Classic }
        public enum UILayoutMode { Card, Sidebar, Floating, Grid }

        public ModSettings(IMod mod) : base(mod) { SetDefaults(); }

        [SettingsUISection(kSection, kMainGroup)]
        public bool IsEnabled { get; set; } = true;

        [SettingsUISection(kSection, kMainGroup)]
        public UILayoutMode LayoutMode { get; set; } = UILayoutMode.Card;

        [SettingsUISection(kSection, kMainGroup)]
        public bool ShowTopButton { get; set; } = true;

        [SettingsUISection(kSection, kMainGroup)]
        public NavBarSide ButtonSide { get; set; } = NavBarSide.Left;

        [SettingsUISlider(min = 10, max = 100, step = 10, scalarMultiplier = 1, unit = Unit.kPercentage)]
        [SettingsUISection(kSection, kMainGroup)]
        public int Opacity { get; set; } = 50;

        [SettingsUISection(kSection, kMainGroup)]
        public VisualPreset Preset { get; set; } = VisualPreset.Rings;

        [SettingsUISlider(min = 10, max = 300, step = 5)]
        [SettingsUISection(kSection, kMainGroup)]
        public int GlobalCircleSize { get; set; } = 100;

        [SettingsUISlider(min = 0, max = 100, step = 1)]
        [SettingsUISection(kSection, kMainGroup)]
        public int OverlayHeight { get; set; } = 1;

        [SettingsUISlider(min = 50, max = 2000, step = 50)]
        [SettingsUISection(kSection, kMainGroup)]
        public int LabelDistance { get; set; } = 200;

        [SettingsUISection(kSection, kMainGroup)]
        public bool ShowStats { get; set; } = true;

        [SettingsUISection(kSection, kMainGroup)]
        public bool HighVis { get; set; } = false;

        public override void SetDefaults()
        {
            IsEnabled = true;
            LayoutMode = UILayoutMode.Card;
            ShowTopButton = true;
            ButtonSide = NavBarSide.Left;
            Opacity = 50;
            Preset = VisualPreset.Rings;
            GlobalCircleSize = 100;
            OverlayHeight = 1;
            LabelDistance = 200;
            ShowStats = true;
            HighVis = false;
        }
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
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LayoutMode)), "UI Layout Mode" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LayoutMode)), "Choose between different UI styles (Card, Sidebar, Floating, Grid)." },
                { m_Setting.GetEnumValueLocaleID(ModSettings.UILayoutMode.Card), "Option A: Card" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.UILayoutMode.Sidebar), "Option B: Sidebar" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.UILayoutMode.Floating), "Option C: Floating" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.UILayoutMode.Grid), "Option D: Grid" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShowTopButton)), "Show Floating Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShowTopButton)), "Show a floating button in the navigation bar." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ButtonSide)), "Button Side" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ButtonSide)), "Which side to place the floating button." },
                { m_Setting.GetEnumValueLocaleID(ModSettings.NavBarSide.Left), "Top Left" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.NavBarSide.Right), "Top Right" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.Opacity)), "Overlay Opacity" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.Opacity)), "Adjust the transparency of area of effect circles." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.Preset)), "Visual Preset" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.Preset)), "Choose the ring rendering style." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.GlobalCircleSize)), "Global Circle Size (m)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.OverlayHeight)), "Overlay Height (m)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShowStats)), "Show Floating Stats" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShowStats)), "Display real-time service bonuses (+Wellbeing, etc.) over buildings." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LabelDistance)), "Label Visibility Distance (m)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LabelDistance)), "How far labels are visible when zooming out." },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HighVis)), "High Visibility Mode" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HighVis)), "Boost bubble contrast and font sizes for easier reading." },
                { m_Setting.GetEnumValueLocaleID(ModSettings.VisualPreset.Rings), "Neon Rings" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.VisualPreset.Glow), "Soft Glow" },
                { m_Setting.GetEnumValueLocaleID(ModSettings.VisualPreset.Classic), "Classic" },
            };
        }

        public void Unload() { }
    }
}
