using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace VoicevoxPlugin
{
    class ConfigViewModel : ViewModelBase
    {
        private const string DefaultDeviceLabel = "（既定のデバイス）";
        private readonly Options _options;
        private readonly VoicevoxClient _client;
        private readonly SpeechQueue _queue;
        private readonly Dispatcher _dispatcher;

        public ConfigViewModel(Options options, VoicevoxClient client, SpeechQueue queue)
        {
            _options = options;
            _client = client;
            _queue = queue;
            _dispatcher = Dispatcher.CurrentDispatcher;

            RefreshSpeakersCommand = new RelayCommand(RefreshSpeakers);
            TestSpeakCommand = new RelayCommand(TestSpeak);
            ClearQueueCommand = new RelayCommand(ClearQueue);

            if (_queue != null)
            {
                _queue.ErrorOccurred += (s, message) =>
                {
                    _dispatcher.BeginInvoke((Action)(() => StatusMessage = "エラー: " + message));
                };
            }
            StatusMessage = $"話者ID {_options.SpeakerId} を使用中。「接続テスト・話者一覧を取得」を押すと話者を選べます。";
        }

        /// <summary>
        /// デザイナ用
        /// </summary>
        public ConfigViewModel() : this(new Options(), new VoicevoxClient(), null) { }

        /// <summary>
        /// 設定画面が表示されたときに呼ぶ
        /// </summary>
        public void OnShown()
        {
            RefreshDevices();
            if (Speakers.Count == 0)
            {
                RefreshSpeakers();
            }
        }

        #region 基本設定
        public bool IsEnabled
        {
            get { return _options.IsEnabled; }
            set { _options.IsEnabled = value; RaisePropertyChanged(); }
        }
        public string EngineUrl
        {
            get { return _options.EngineUrl; }
            set { _options.EngineUrl = value; RaisePropertyChanged(); }
        }
        public bool IsReadHandleName
        {
            get { return _options.IsReadHandleName; }
            set { _options.IsReadHandleName = value; RaisePropertyChanged(); }
        }
        public bool IsAppendNickTitle
        {
            get { return _options.IsAppendNickTitle; }
            set { _options.IsAppendNickTitle = value; RaisePropertyChanged(); }
        }
        public string NickTitle
        {
            get { return _options.NickTitle; }
            set { _options.NickTitle = value ?? ""; RaisePropertyChanged(); }
        }
        public bool Want184Read
        {
            get { return _options.Want184Read; }
            set { _options.Want184Read = value; RaisePropertyChanged(); }
        }
        public bool IsReplaceUrl
        {
            get { return _options.IsReplaceUrl; }
            set { _options.IsReplaceUrl = value; RaisePropertyChanged(); }
        }
        public int MaxTextLength
        {
            get { return _options.MaxTextLength; }
            set { _options.MaxTextLength = value; RaisePropertyChanged(); }
        }
        public int MaxQueueLength
        {
            get { return _options.MaxQueueLength; }
            set { _options.MaxQueueLength = value; RaisePropertyChanged(); }
        }
        #endregion

        #region 声の調整
        //Sliderからはdoubleで来るので小数第2位で丸めて保存する
        public double SpeedScale
        {
            get { return _options.SpeedScale; }
            set { _options.SpeedScale = Math.Round(value, 2); RaisePropertyChanged(); }
        }
        public double PitchScale
        {
            get { return _options.PitchScale; }
            set { _options.PitchScale = Math.Round(value, 2); RaisePropertyChanged(); }
        }
        public double IntonationScale
        {
            get { return _options.IntonationScale; }
            set { _options.IntonationScale = Math.Round(value, 2); RaisePropertyChanged(); }
        }
        public double VolumeScale
        {
            get { return _options.VolumeScale; }
            set { _options.VolumeScale = Math.Round(value, 2); RaisePropertyChanged(); }
        }
        #endregion

        #region 話者
        public ObservableCollection<SpeakerStyle> Speakers { get; } = new ObservableCollection<SpeakerStyle>();

        private SpeakerStyle _selectedSpeaker;
        public SpeakerStyle SelectedSpeaker
        {
            get { return _selectedSpeaker; }
            set
            {
                _selectedSpeaker = value;
                if (value != null)
                {
                    _options.SpeakerId = value.Id;
                }
                RaisePropertyChanged();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            private set { _isBusy = value; RaisePropertyChanged(); }
        }

        public ICommand RefreshSpeakersCommand { get; }
        private async void RefreshSpeakers()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "VOICEVOXに接続しています…";
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var version = await _client.GetVersionAsync(_options.EngineUrl, cts.Token);
                    var speakers = await _client.GetSpeakersAsync(_options.EngineUrl, cts.Token);
                    Speakers.Clear();
                    foreach (var speaker in speakers)
                    {
                        Speakers.Add(speaker);
                    }
                    var current = Speakers.FirstOrDefault(sp => sp.Id == _options.SpeakerId);
                    SelectedSpeaker = current;
                    StatusMessage = current != null
                        ? $"接続OK（VOICEVOX {version}）。話者: {current.DisplayName}"
                        : $"接続OK（VOICEVOX {version}）。設定中の話者ID {_options.SpeakerId} が見つかりません。話者を選び直してください。";
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "エラー: VOICEVOXの応答がありません。VOICEVOXが起動しているか確認してください。";
            }
            catch (Exception ex)
            {
                StatusMessage = "エラー: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region 出力デバイス
        public ObservableCollection<string> OutputDevices { get; } = new ObservableCollection<string>();

        public string SelectedOutputDevice
        {
            get { return string.IsNullOrEmpty(_options.OutputDeviceName) ? DefaultDeviceLabel : _options.OutputDeviceName; }
            set
            {
                //デバイス一覧の更新中に一時的にnullが来るので無視する
                if (value == null) return;
                _options.OutputDeviceName = value == DefaultDeviceLabel ? "" : value;
                RaisePropertyChanged();
            }
        }

        private void RefreshDevices()
        {
            var selected = SelectedOutputDevice;
            OutputDevices.Clear();
            OutputDevices.Add(DefaultDeviceLabel);
            try
            {
                foreach (var name in AudioPlayer.GetDeviceNames())
                {
                    OutputDevices.Add(name);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "エラー: オーディオデバイスの取得に失敗しました: " + ex.Message;
            }
            //保存されているデバイスが今は無い場合も、設定値は消さずに一覧に残しておく
            if (!OutputDevices.Contains(selected))
            {
                OutputDevices.Add(selected);
            }
            RaisePropertyChanged(nameof(SelectedOutputDevice));
        }
        #endregion

        #region テスト
        private string _testText = "テストです。コメントをこの声で読み上げます。";
        public string TestText
        {
            get { return _testText; }
            set { _testText = value; RaisePropertyChanged(); }
        }

        public ICommand TestSpeakCommand { get; }
        private void TestSpeak()
        {
            if (string.IsNullOrWhiteSpace(TestText)) return;
            StatusMessage = "テスト再生します…";
            _queue?.Enqueue(new SpeechRequest(TestText, _options.SpeakerId));
        }

        public ICommand ClearQueueCommand { get; }
        private void ClearQueue()
        {
            _queue?.Clear();
            StatusMessage = "読み上げを中止し、待ちを空にしました。";
        }
        #endregion

        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set { _statusMessage = value; RaisePropertyChanged(); }
        }
    }
}
