using Plugin;
using SitePlugin;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace VoicevoxPlugin
{
    /// <summary>
    /// 受信したコメントをVOICEVOXで読み上げるプラグイン
    /// </summary>
    [Export(typeof(IPlugin))]
    public class VoicevoxPlugin : IPlugin, IDisposable
    {
        private readonly Options _options;
        private readonly VoicevoxClient _client;
        private readonly AudioPlayer _player;
        private readonly SpeechQueue _queue;
        private ConfigView _settingsView;
        private ConfigViewModel _settingsViewModel;

        public string Name => "VOICEVOX読み上げ";
        public string Description => "コメントをVOICEVOXの音声で読み上げます。";
        public IPluginHost Host { get; set; }

        public VoicevoxPlugin()
        {
            _options = new Options();
            _client = new VoicevoxClient();
            _player = new AudioPlayer();
            _queue = new SpeechQueue(_options, _client, _player);
        }

        public void OnLoaded()
        {
            try
            {
                var s = Host.LoadOptions(GetSettingsFilePath());
                _options.Deserialize(s);
            }
            catch (System.IO.FileNotFoundException) { }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            _queue.Start();
        }

        public void OnClosing()
        {
            _settingsView?.ForceClose();
            try
            {
                Host.SaveOptions(GetSettingsFilePath(), _options.Serialize());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            Dispose();
        }

        public void OnMessageReceived(ISiteMessage message, IMessageMetadata messageMetadata)
        {
            try
            {
                if (!_options.IsEnabled || message == null || messageMetadata == null)
                    return;
                //NGユーザー、接続直後に取得した過去コメントは読まない
                if (messageMetadata.IsNgUser || messageMetadata.IsSiteNgUser || messageMetadata.IsInitialComment)
                    return;
                if (messageMetadata.Is184 && !_options.Want184Read)
                    return;

                var (name, comment) = PluginCommon.Tools.GetData(message);
                if (string.IsNullOrWhiteSpace(comment))
                    return;

                //コテハンが設定されていればそちらを優先する
                var nickname = messageMetadata.User?.Nickname;
                if (!string.IsNullOrEmpty(nickname))
                {
                    name = nickname;
                }

                var text = SpeechTextBuilder.Build(name, comment, _options);
                _queue.Enqueue(new SpeechRequest(text, ResolveSpeakerId(messageMetadata)));
            }
            catch (Exception ex)
            {
                //プラグインの例外でコメビュ本体を落とさない
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// コメントを読み上げる話者を決める。
        /// TODO: ステップ2で投稿者ごとに話者を割り当てる
        /// </summary>
        private int ResolveSpeakerId(IMessageMetadata messageMetadata)
        {
            return _options.SpeakerId;
        }

        public void ShowSettingView()
        {
            if (_settingsView == null)
            {
                _settingsViewModel = new ConfigViewModel(_options, _client, _queue);
                _settingsView = new ConfigView
                {
                    DataContext = _settingsViewModel,
                };
            }
            _settingsView.Topmost = Host.IsTopmost;
            _settingsView.Left = Host.MainViewLeft;
            _settingsView.Top = Host.MainViewTop;
            _settingsView.Show();
            _settingsViewModel.OnShown();
        }

        public void OnTopmostChanged(bool isTopmost)
        {
            if (_settingsView != null)
            {
                _settingsView.Topmost = isTopmost;
            }
        }

        public string GetSettingsFilePath()
        {
            return System.IO.Path.Combine(Host.SettingsDirPath, $"{Name}.xml");
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _queue.Dispose();
            _client.Dispose();
        }
    }
}
