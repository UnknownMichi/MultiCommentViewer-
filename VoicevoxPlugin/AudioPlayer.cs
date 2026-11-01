using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace VoicevoxPlugin
{
    /// <summary>
    /// WAVを指定のオーディオデバイスで再生する。
    /// OBSで読み上げ音声だけを別トラックにしたい場合に、仮想オーディオデバイスを選べるようにしている。
    /// </summary>
    class AudioPlayer
    {
        /// <summary>
        /// 再生に使えるデバイス名の一覧
        /// </summary>
        public static List<string> GetDeviceNames()
        {
            var list = new List<string>();
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                list.Add(WaveOut.GetCapabilities(i).ProductName);
            }
            return list;
        }

        /// <summary>
        /// デバイス名からデバイス番号を求める。見つからなければ既定のデバイス(-1)
        /// </summary>
        private static int FindDeviceNumber(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return -1;
            for (var i = 0; i < WaveOut.DeviceCount; i++)
            {
                if (WaveOut.GetCapabilities(i).ProductName == deviceName)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 再生が終わるまでブロックする。キャンセルされたら再生を止めて戻る
        /// </summary>
        public void PlaySync(byte[] wav, string deviceName, CancellationToken ct)
        {
            using (var stream = new MemoryStream(wav))
            using (var reader = new WaveFileReader(stream))
            using (var output = new WaveOutEvent { DeviceNumber = FindDeviceNumber(deviceName) })
            using (var stopped = new ManualResetEventSlim(false))
            {
                Exception playbackError = null;
                output.PlaybackStopped += (s, e) =>
                {
                    playbackError = e.Exception;
                    //キャンセル時はStop()後にこのイベントが遅れて来ることがあるので、破棄済みでも落ちないようにする
                    try { stopped.Set(); } catch (ObjectDisposedException) { }
                };
                output.Init(reader);
                output.Play();
                try
                {
                    stopped.Wait(ct);
                }
                catch (OperationCanceledException)
                {
                    output.Stop();
                    throw;
                }
                if (playbackError != null)
                {
                    throw new VoicevoxException("音声の再生に失敗しました: " + playbackError.Message, playbackError);
                }
            }
        }
    }
}
