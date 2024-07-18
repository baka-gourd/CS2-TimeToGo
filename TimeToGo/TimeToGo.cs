﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using HarmonyLib;

namespace TimeToGo
{
    public class TimeToGo : IMod
    {
        public static ConcurrentDictionary<int, uint> TimerDict { get; private set; } = new();

        public static ILog Logger = LogManager.GetLogger($"{nameof(TimeToGo)}.{nameof(TimeToGo)}")
            .SetShowsErrorsInUI(false);

        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Logger.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Logger.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();

            var harmony = new Harmony("Nptr.TimeToGo");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            AssetDatabase.global.LoadSettings(nameof(TimeToGo), m_Setting, new Setting(this));
        }

        public void OnDispose()
        {
            Logger.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}