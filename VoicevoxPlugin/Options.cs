using System.Globalization;
using Common;

namespace VoicevoxPlugin
{
    /// <summary>
    /// VOICEVOX読み上げプラグインの設定。
    /// 保存形式は他プラグインと同じ「名前=値」の行形式（DynamicOptionsBase）。
    /// </summary>
    class Options : DynamicOptionsBase
    {
        public bool IsEnabled { get { return GetValue(); } set { SetValue(value); } }

        /// <summary>VOICEVOXエンジンのURL（既定 http://localhost:50021）</summary>
        public string EngineUrl { get { return GetValue(); } set { SetValue(value); } }

        /// <summary>読み上げに使う話者（スタイル）ID。既定は冥鳴ひまり（ノーマル）=14</summary>
        public int SpeakerId { get { return GetValue(); } set { SetValue(value); } }

        /// <summary>話速 0.5～2.0</summary>
        public double SpeedScale { get { return GetValue(); } set { SetValue(value); } }
        /// <summary>音高 -0.15～0.15</summary>
        public double PitchScale { get { return GetValue(); } set { SetValue(value); } }
        /// <summary>抑揚 0.0～2.0</summary>
        public double IntonationScale { get { return GetValue(); } set { SetValue(value); } }
        /// <summary>音量 0.0～2.0</summary>
        public double VolumeScale { get { return GetValue(); } set { SetValue(value); } }

        /// <summary>出力先オーディオデバイス名。空文字なら既定のデバイス</summary>
        public string OutputDeviceName { get { return GetValue(); } set { SetValue(value); } }

        public bool IsReadHandleName { get { return GetValue(); } set { SetValue(value); } }
        public bool IsAppendNickTitle { get { return GetValue(); } set { SetValue(value); } }
        public string NickTitle { get { return GetValue(); } set { SetValue(value); } }
        public bool Want184Read { get { return GetValue(); } set { SetValue(value); } }
        /// <summary>URLを「URL省略」に置き換える</summary>
        public bool IsReplaceUrl { get { return GetValue(); } set { SetValue(value); } }

        /// <summary>読み上げる最大文字数。超えた分は「以下略」</summary>
        public int MaxTextLength { get { return GetValue(); } set { SetValue(value); } }
        /// <summary>読み上げ待ちの最大件数。超えたら古いものから捨てる</summary>
        public int MaxQueueLength { get { return GetValue(); } set { SetValue(value); } }

        protected override void Init()
        {
            Dict.Add(nameof(IsEnabled), BoolItem(false));
            Dict.Add(nameof(EngineUrl), new Item { DefaultValue = "http://localhost:50021", Predicate = s => !string.IsNullOrWhiteSpace(s), Serializer = s => s, Deserializer = s => s });
            Dict.Add(nameof(SpeakerId), IntItem(14, 0, int.MaxValue));

            Dict.Add(nameof(SpeedScale), DoubleItem(1.0, 0.5, 2.0));
            Dict.Add(nameof(PitchScale), DoubleItem(0.0, -0.15, 0.15));
            Dict.Add(nameof(IntonationScale), DoubleItem(1.0, 0.0, 2.0));
            Dict.Add(nameof(VolumeScale), DoubleItem(1.0, 0.0, 2.0));

            Dict.Add(nameof(OutputDeviceName), new Item { DefaultValue = "", Predicate = s => s != null, Serializer = s => s, Deserializer = s => s });

            Dict.Add(nameof(IsReadHandleName), BoolItem(true));
            Dict.Add(nameof(IsAppendNickTitle), BoolItem(true));
            Dict.Add(nameof(NickTitle), new Item { DefaultValue = "さん", Predicate = s => s != null, Serializer = s => s, Deserializer = s => s });
            Dict.Add(nameof(Want184Read), BoolItem(false));
            Dict.Add(nameof(IsReplaceUrl), BoolItem(true));

            Dict.Add(nameof(MaxTextLength), IntItem(60, 1, 1000));
            Dict.Add(nameof(MaxQueueLength), IntItem(10, 1, 1000));
        }

        private static Item BoolItem(bool defaultValue)
        {
            return new Item { DefaultValue = defaultValue, Predicate = b => true, Serializer = b => b.ToString(), Deserializer = s => bool.Parse(s) };
        }
        private static Item IntItem(int defaultValue, int min, int max)
        {
            return new Item { DefaultValue = defaultValue, Predicate = n => n >= min && n <= max, Serializer = n => n.ToString(CultureInfo.InvariantCulture), Deserializer = s => int.Parse(s, CultureInfo.InvariantCulture) };
        }
        private static Item DoubleItem(double defaultValue, double min, double max)
        {
            //ロケールによって小数点が","になるのを避けるためInvariantCultureで保存する
            return new Item { DefaultValue = defaultValue, Predicate = d => d >= min && d <= max, Serializer = d => ((double)d).ToString("R", CultureInfo.InvariantCulture), Deserializer = s => double.Parse(s, CultureInfo.InvariantCulture) };
        }
    }
}
