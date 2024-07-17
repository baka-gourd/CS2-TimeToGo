﻿using System.Reflection;

using Colossal.IO.AssetDatabase;
using Colossal.Logging;

using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;

using HarmonyLib;

namespace TimeToGo
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(TimeToGo)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();

            var harmony = new Harmony("Nptr.TimeToGo");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            var x = new TransportCarTickJobWithTimer()
            { m_BoardingData = new TransportBoardingHelpers.BoardingData.Concurrent() };
            AssetDatabase.global.LoadSettings(nameof(TimeToGo), m_Setting, new Setting(this));
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
