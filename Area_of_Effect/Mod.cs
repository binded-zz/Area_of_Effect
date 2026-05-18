using Area_of_Effect.ModSettings;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace Area_of_Effect
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(Area_of_Effect)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static ModSettings.ModSettings Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            Settings = new ModSettings.ModSettings(this);
            AssetDatabase.global.LoadSettings(nameof(Area_of_Effect), Settings, new ModSettings.ModSettings(this));
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));

            updateSystem.UpdateAt<AreaOfEffectUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<AreaOfEffectSystem>(SystemUpdatePhase.ToolUpdate);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
