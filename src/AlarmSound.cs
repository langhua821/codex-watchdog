using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodexWatchdog
{
    public sealed class AlarmSound : IDisposable
    {
        private SoundPlayer player;
        private MemoryStream stream;
        private MediaPlayer media;
        private DispatcherTimer repeat;
        public bool IsPlaying { get; private set; }
        public string Failure { get; private set; }

        public void Start(int volumePercent) { Start(volumePercent, null); }
        public void Start(int volumePercent, string soundPath)
        {
            Stop();
            Failure = null;
            try
            {
                if (String.IsNullOrWhiteSpace(soundPath))
                {
                    stream = new MemoryStream(CreateWave(volumePercent));
                    player = new SoundPlayer(stream);
                    player.Load();
                    player.PlayLooping();
                }
                else
                {
                    string path = Path.GetFullPath(soundPath);
                    if (!File.Exists(path)) throw new FileNotFoundException("找不到铃声文件。", path);
                    string extension = Path.GetExtension(path);
                    if (!String.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("请选择 WAV 或 MP3 音频文件。");
                    media = new MediaPlayer();
                    media.Volume = Math.Max(0, Math.Min(100, volumePercent)) / 100.0;
                    media.MediaEnded += OnEnded;
                    media.MediaFailed += OnFailed;
                    media.Open(new Uri(path, UriKind.Absolute));
                    media.Play();
                }
                IsPlaying = true;
            }
            catch (Exception error)
            {
                Stop();
                Failure = error.Message;
                throw;
            }
        }
        private void OnEnded(object sender, EventArgs args)
        {
            if (!IsPlaying || !Object.ReferenceEquals(sender, media)) return;
            // A short pause keeps even compact notification files comfortable in a repeating alarm.
            repeat = new DispatcherTimer();
            repeat.Interval = TimeSpan.FromSeconds(1.5);
            repeat.Tick += OnRepeat;
            repeat.Start();
        }
        private void OnRepeat(object sender, EventArgs args)
        {
            if (repeat != null) { repeat.Stop(); repeat.Tick -= OnRepeat; repeat = null; }
            if (IsPlaying && media != null) { media.Position = TimeSpan.Zero; media.Play(); }
        }
        private void OnFailed(object sender, ExceptionEventArgs args)
        {
            if (!Object.ReferenceEquals(sender, media)) return;
            string message = args.ErrorException == null ? "无法播放此音频，请换一个 WAV 或 MP3 文件。" : args.ErrorException.Message;
            Stop();
            Failure = message;
        }
        public void Stop()
        {
            IsPlaying = false;
            Failure = null;
            if (repeat != null) { repeat.Stop(); repeat.Tick -= OnRepeat; repeat = null; }
            if (media != null)
            {
                media.MediaEnded -= OnEnded;
                media.MediaFailed -= OnFailed;
                media.Stop(); media.Close(); media = null;
            }
            if (player != null) { player.Stop(); player.Dispose(); player = null; }
            if (stream != null) { stream.Dispose(); stream = null; }
        }
        public void Dispose() { Stop(); }
        public static byte[] CreateWave(int volumePercent)
        {
            const int rate = 22050;
            const double seconds = 2.4;
            int sampleCount = (int)(rate * seconds);
            double volume = Math.Max(0, Math.Min(100, volumePercent)) / 100.0 * 0.35;
            using (MemoryStream buffer = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(buffer))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(sampleCount * 2);
                for (int i = 0; i < sampleCount; i++)
                {
                    double time = i / (double)rate;
                    double cycle = time % 0.6;
                    double envelope = cycle < 0.4 ? Math.Min(1, cycle / 0.015) * Math.Min(1, (0.4 - cycle) / 0.035) : 0;
                    double frequency = cycle < 0.2 ? 740 : 988;
                    double value = Math.Sin(2 * Math.PI * frequency * time) * volume * Math.Max(0, envelope);
                    writer.Write((short)(value * short.MaxValue));
                }
                writer.Flush(); return buffer.ToArray();
            }
        }
    }
}
