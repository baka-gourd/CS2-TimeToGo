using System.IO;
using System.Reflection;

using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;

using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;

namespace TimeToGo
{
    public class TimeToGo : IMod
    {
        public static ILog Logger = LogManager.GetLogger($"{nameof(TimeToGo)}.{nameof(TimeToGo)}")
            .SetShowsErrorsInUI(false);

        public static TimeToGo Instance { get; private set; }

        public Setting Setting { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Logger.Info($"Current mod asset at {asset.path}");

            var dir = new DirectoryInfo(Path.Combine(
                EnvPath.kUserDataPath,
                "ModsSettings",
                "TimeToGo"));

            if (!dir.Exists)
            {
                dir.Create();
            }

            Setting = new Setting(this);
            Setting.RegisterInOptionsUI();

            var harmony = new Harmony("Nptr.TimeToGo");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Instance = this;
            TransportVehicleStopTimer.Interval.Data = Setting.MinutesInFrame;
            Logger.InfoFormat("Now max boarding time: {0}", TransportVehicleStopTimer.Interval.Data);

            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Setting));

            AssetDatabase.global.LoadSettings(nameof(TimeToGo), Setting, new Setting(this));
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
            if (Setting != null)
            {
                Setting.UnregisterInOptionsUI();
                Setting = null;
            }
        }
    }
}