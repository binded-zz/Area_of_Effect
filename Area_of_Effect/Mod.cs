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
            try
            {
                string assetPath = null;
                if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset) && asset != null)
                {
                    assetPath = asset.path;
                    log.Info($"Current mod asset at {assetPath ?? "NULL"}");
                }

                Settings = new ModSettings.ModSettings(this);

                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.global != null)
                {
                    try
                    {
                        AssetDatabase.global.LoadSettings("ModsSettings/Area_of_Effect/Area_of_Effect", Settings, new ModSettings.ModSettings(this));
                    }
                    catch (System.Exception ex)
                    {
                        log.Error(ex, "Failed to load settings via AssetDatabase.global.LoadSettings");
                    }
                }
                else
                {
                    log.Warn("Mod executable asset path is null or AssetDatabase.global is null, skipping LoadSettings.");
                }

                Settings.RegisterInOptionsUI();
                GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));

                updateSystem.UpdateAt<AreaOfEffectUISystem>(SystemUpdatePhase.UIUpdate);
                updateSystem.UpdateAt<AreaOfEffectSystem>(SystemUpdatePhase.ToolUpdate);
                updateSystem.UpdateAt<VisualizationDispatcherSystem>(SystemUpdatePhase.UIUpdate);
            }
            catch (System.Exception ex)
            {
                log.Error(ex, "Failed to initialize Mod");
            }
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
