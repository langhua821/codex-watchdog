using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Media;
using System.Windows.Media;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using CodexWatchdog;
class SoundCatalogTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [STAThread] static int Main()
    {
        try
        {
            List<SoundOption> options = SoundCatalog.BuiltIn();
            Check(options.Count == 21, "Expected 21 playable built-in music alarms; found " + options.Count);
            Check(options.Select(o => o.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 21, "Duplicate sound identifiers");
            Check(options.All(o => File.Exists(o.FilePath)), "A catalog sound file is missing");
            HashSet<string> hashes = new HashSet<string>();
            foreach (SoundOption option in options)
            {
                using (FileStream file = File.OpenRead(option.FilePath)) using (SHA256 sha = SHA256.Create()) Check(hashes.Add(BitConverter.ToString(sha.ComputeHash(file))), "Duplicate audio file: " + option.Id);
                using (SoundPlayer wave = new SoundPlayer(option.FilePath)) wave.Load();
                MediaPlayer media = new MediaPlayer(); DispatcherFrame frame = new DispatcherFrame(); bool opened = false; Exception failure = null;
                DispatcherTimer timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                try
                {
                    media.Volume = 0;
                    media.MediaOpened += delegate { opened = true; frame.Continue = false; };
                    media.MediaFailed += delegate(object sender, ExceptionEventArgs error) { failure = error.ErrorException; frame.Continue = false; };
                    timeout.Tick += delegate { frame.Continue = false; }; timeout.Start();
                    media.Open(new Uri(option.FilePath)); Dispatcher.PushFrame(frame);
                    Check(failure == null && opened, "Native audio load failed: " + option.Id);
                    Check(media.NaturalDuration.HasTimeSpan && media.NaturalDuration.TimeSpan.TotalSeconds >= 8 && media.NaturalDuration.TimeSpan.TotalSeconds <= 25, "Unexpected alarm duration: " + option.Id);
                    Check(SoundCatalog.Resolve(option.Id, "") == option.FilePath, "Catalog resolver mismatch");
                }
                finally { timeout.Stop(); media.Close(); }
            }
            object[] extras = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "extras.json"))) as object[];
            Check(extras != null && extras.Length == 18, "Expected 18 new catalog items");
            int styles = extras.Cast<Dictionary<string, object>>().Select(d => d["style"] as string).Distinct().Count(); Check(styles >= 6, "Insufficient musical styles");
            bool rejected = false; try { SoundCatalog.Resolve("not-installed", ""); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Missing selection must not silently play another song");
            Console.WriteLine("PASS: 21 distinct audio files, 18 additions, " + styles + " styles, PCM/native Windows loads, durations and resolver"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
