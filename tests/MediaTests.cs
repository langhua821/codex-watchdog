using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Media;
using CodexWatchdog;

internal static class MediaTests
{
    [STAThread]
    private static int Main(string[] args)
    {
        int endings = 0; Exception failure = null;
        int timeoutSeconds = args.Length > 1 ? Math.Max(9, Math.Min(60, int.Parse(args[1]))) : 9;
        DateTime started = DateTime.UtcNow;
        using (AlarmSound sound = new AlarmSound())
        using (Form form = new Form { ShowInTaskbar = false, Opacity = 0, Width = 1, Height = 1 })
        using (Timer timer = new Timer { Interval = 100 })
        {
            form.Shown += delegate
            {
                try
                {
                    string file = Path.GetFullPath(args[0]);
                    try { sound.Start(0, file + ".missing"); } catch (FileNotFoundException) { }
                    if (string.IsNullOrEmpty(sound.Failure)) throw new Exception("Missing sound error must be visible");
                    sound.Stop();
                    if (!string.IsNullOrEmpty(sound.Failure)) throw new Exception("Stopping a failed preview must clear stale failure before choosing a new sound");
                    sound.Start(0, file);
                    MediaPlayer media = (MediaPlayer)typeof(AlarmSound).GetField("media", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sound);
                    media.MediaEnded += delegate { endings++; };
                    started = DateTime.UtcNow; timer.Start();
                }
                catch (Exception error) { failure = error; form.Close(); }
            };
            timer.Tick += delegate
            {
                if (!string.IsNullOrEmpty(sound.Failure)) { failure = new Exception(sound.Failure); form.Close(); }
                else if (endings >= 2) { sound.Stop(); if (sound.IsPlaying) failure = new Exception("Alarm did not stop"); form.Close(); }
                else if (DateTime.UtcNow - started > TimeSpan.FromSeconds(timeoutSeconds)) { failure = new Exception("Playback failed to loop in the Windows Forms message pump; endings=" + endings); form.Close(); }
            };
            Application.Run(form);
        }
        if (failure != null) { Console.Error.WriteLine(failure.Message); return 1; }
        Console.WriteLine("PASS: real local media ended " + endings + " times, repeated in WinForms, stopped and released (muted)");
        return 0;
    }
}
