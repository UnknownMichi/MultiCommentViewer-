using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VoicevoxPlugin
{
    /// <summary>
    /// 音声合成時の調整パラメータ（audio_queryの値を上書きする）
    /// </summary>
    class VoiceParameters
    {
        public double SpeedScale { get; set; } = 1.0;
        public double PitchScale { get; set; } = 0.0;
        public double IntonationScale { get; set; } = 1.0;
        public double VolumeScale { get; set; } = 1.0;
    }

    /// <summary>
    /// 話者のスタイル1つ分（VOICEVOXでは「話者ID」はスタイル単位で振られている）
    /// </summary>
    public class SpeakerStyle
    {
        public int Id { get; }
        public string SpeakerName { get; }
        public string StyleName { get; }
        public string DisplayName => $"{SpeakerName}（{StyleName}）";
        public SpeakerStyle(int id, string speakerName, string styleName)
        {
            Id = id;
            SpeakerName = speakerName;
            StyleName = styleName;
        }
        public override string ToString() => DisplayName;
    }

    [Serializable]
    public class VoicevoxException : Exception
    {
        public VoicevoxException(string message) : base(message) { }
        public VoicevoxException(string message, Exception inner) : base(message, inner) { }
        protected VoicevoxException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// VOICEVOXエンジンのHTTP APIクライアント
    /// https://voicevox.github.io/voicevox_engine/api/
    /// </summary>
    class VoicevoxClient : IDisposable
    {
        private readonly HttpClient _http;

        public VoicevoxClient()
        {
            _http = new HttpClient
            {
                //長文の合成でも収まる程度。エンジン未起動時は接続拒否ですぐに失敗する
                Timeout = TimeSpan.FromSeconds(30),
            };
        }

        private static string Normalize(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new VoicevoxException("VOICEVOXエンジンのURLが設定されていません");
            return baseUrl.Trim().TrimEnd('/');
        }

        /// <summary>
        /// エンジンのバージョンを取得する。接続確認用
        /// </summary>
        public async Task<string> GetVersionAsync(string baseUrl, CancellationToken ct)
        {
            var url = Normalize(baseUrl) + "/version";
            var res = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct).ConfigureAwait(false);
            var s = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            //"0.14.10" のようにJSON文字列で返ってくる
            return s.Trim().Trim('"');
        }

        /// <summary>
        /// 利用可能な話者（スタイル）一覧を取得する
        /// </summary>
        public async Task<List<SpeakerStyle>> GetSpeakersAsync(string baseUrl, CancellationToken ct)
        {
            var url = Normalize(baseUrl) + "/speakers";
            var res = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = new List<SpeakerStyle>();
            foreach (var speaker in JArray.Parse(json))
            {
                var speakerName = (string)speaker["name"];
                var styles = speaker["styles"] as JArray;
                if (styles == null) continue;
                foreach (var style in styles)
                {
                    //歌唱用スタイル（type=sing等）は読み上げに使えないので除外する
                    var type = (string)style["type"];
                    if (type != null && type != "talk") continue;
                    list.Add(new SpeakerStyle((int)style["id"], speakerName, (string)style["name"]));
                }
            }
            return list;
        }

        /// <summary>
        /// テキストを音声合成してWAVのバイト列を返す
        /// </summary>
        public async Task<byte[]> SynthesizeAsync(string baseUrl, string text, int speakerId, VoiceParameters parameters, CancellationToken ct)
        {
            var root = Normalize(baseUrl);
            var speaker = speakerId.ToString(CultureInfo.InvariantCulture);

            //1. audio_query で音声合成用のクエリを作る
            var queryUrl = $"{root}/audio_query?text={Uri.EscapeDataString(text)}&speaker={speaker}";
            var queryRes = await SendAsync(new HttpRequestMessage(HttpMethod.Post, queryUrl), ct).ConfigureAwait(false);
            var queryJson = await queryRes.Content.ReadAsStringAsync().ConfigureAwait(false);

            //2. 話速などを設定値で上書きする
            var query = JObject.Parse(queryJson);
            if (parameters != null)
            {
                query["speedScale"] = parameters.SpeedScale;
                query["pitchScale"] = parameters.PitchScale;
                query["intonationScale"] = parameters.IntonationScale;
                query["volumeScale"] = parameters.VolumeScale;
            }

            //3. synthesis でWAVを取得する
            var synthUrl = $"{root}/synthesis?speaker={speaker}";
            var req = new HttpRequestMessage(HttpMethod.Post, synthUrl)
            {
                Content = new StringContent(query.ToString(Formatting.None), Encoding.UTF8, "application/json"),
            };
            var synthRes = await SendAsync(req, ct).ConfigureAwait(false);
            return await synthRes.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            HttpResponseMessage res;
            try
            {
                res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new VoicevoxException("VOICEVOXエンジンに接続できません。VOICEVOXが起動しているか確認してください", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new VoicevoxException("VOICEVOXエンジンの応答がタイムアウトしました", ex);
            }
            if (!res.IsSuccessStatusCode)
            {
                var body = "";
                try { body = await res.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }
                throw new VoicevoxException($"VOICEVOXエンジンがエラーを返しました（{(int)res.StatusCode}）: {body}");
            }
            return res;
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
