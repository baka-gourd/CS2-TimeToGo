using Colossal;
using Colossal.IO.AssetDatabase;

using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

using System.Collections.Generic;

using Unity.Mathematics;

using UnityEngine;

namespace TimeToGo
{
    [FileLocation("ModsSettings/TimeToGo/setting")]
    public class Setting : ModSetting
    {
        [SettingsUISlider(max = 60, min = 2, step = 1)]
        public int Minutes { get; set; } = 5;

        public uint MinutesInFrame => (uint)math.floor(Minutes * 182.0444444444444444f);

        [SettingsUIButton]
        public bool ApplyButton
        {
            set
            {
                TransportVehicleStopTimer.Interval.Data = MinutesInFrame;
                TimeToGo.Logger.InfoFormat("Now max boarding time: {0}", TransportVehicleStopTimer.Interval.Data);
            }
        }

        public Setting(IMod mod) : base(mod)
        {
        }

        public override void SetDefaults()
        {
            Minutes = 5;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting _setting;

        public LocaleEN(Setting setting)
        {
            _setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "Time To Go" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ApplyButton)), "Apply" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ApplyButton)), "Apply Time" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.Minutes)), "Max Boarding Time (minutes)" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.Minutes)), "Set Max Boarding Time (minutes)" },
            };
        }

        public void Unload()
        {
        }
    }
}