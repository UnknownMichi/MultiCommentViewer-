using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VoicevoxPlugin
{
    class SpeechRequest
    {
        public string Text { get; }
        public int SpeakerId { get; }
        public SpeechRequest(string text, int speakerId)
        {
            Text = text;
            SpeakerId = speakerId;
        }
    }

    /// <summary>
    /// 読み上げ要求を1件ずつ順番に「合成→再生」するキュー。
    /// コメントが連続しても音声が重ならないよう、処理は専用の1スレッドだけで行う。
    /// </summary>
    class SpeechQueue : IDisposable
    {
        private readonly Options _options;
        private readonly VoicevoxClient _client;
        private readonly AudioPlayer _player;
        private readonly Queue<SpeechRequest> _queue = new Queue<SpeechRequest>();
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationTokenSource _skipCts = new CancellationTokenSource();
        private Task _worker;

        /// <summary>
        /// 合成や再生に失敗したときに通知する（設定画面に表示する用）
        /// </summary>
        public event EventHandler<string> ErrorOccurred;

        public SpeechQueue(Options options, VoicevoxClient client, AudioPlayer player)
        {
            _options = options;
            _client = client;
            _player = player;
        }

        public void Start()
        {
            if (_worker != null) return;
            _worker = Task.Run(() => RunAsync());
        }

        public int Count
        {
            get { lock (_lock) { return _queue.Count; } }
        }

        public void Enqueue(SpeechRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text)) return;
            lock (_lock)
            {
                _queue.Enqueue(request);
                //溜まりすぎたら古いものから捨てる。配信中に数分前のコメントを読み続けるのを防ぐ
                while (_queue.Count > _options.MaxQueueLength)
                {
                    _queue.Dequeue();
                    //捨てた分のシグナルは、ワーカーが空のキューを見て読み飛ばす
                }
            }
            _signal.Release();
        }

        /// <summary>
        /// 待ち行列を空にし、再生中のものも止める
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _queue.Clear();
                var old = _skipCts;
                _skipCts = new CancellationTokenSource();
                //ワーカーが古いトークンを使っている最中かもしれないのでDisposeはしない
                old.Cancel();
            }
        }

        private async Task RunAsync()
        {
            var ct = _cts.Token;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _signal.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                SpeechRequest request;
                CancellationToken skipToken;
                lock (_lock)
                {
                    if (_queue.Count == 0) continue;
                    request = _queue.Dequeue();
                    skipToken = _skipCts.Token;
                }

                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, skipToken))
                {
                    try
                    {
                        var parameters = new VoiceParameters
                        {
                            SpeedScale = _options.SpeedScale,
                            PitchScale = _options.PitchScale,
                            IntonationScale = _options.IntonationScale,
                            VolumeScale = _options.VolumeScale,
                        };
                        var wav = await _client.SynthesizeAsync(_options.EngineUrl, request.Text, request.SpeakerId, parameters, linked.Token).ConfigureAwait(false);
                        _player.PlaySync(wav, _options.OutputDeviceName, linked.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        //終了またはスキップ
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        ErrorOccurred?.Invoke(this, ex.Message);
                    }
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _worker?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { }
            _cts.Dispose();
            _skipCts.Dispose();
            _signal.Dispose();
        }
    }
}
