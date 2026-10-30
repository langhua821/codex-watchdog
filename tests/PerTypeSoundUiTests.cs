using System;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using CodexWatchdog;
class PerTypeSoundUiTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            WatchSettings settings = new WatchSettings(); AlertSoundRouting.EnsureProfiles(settings);
            int applies = 0; AlertKind previewed = AlertKind.Error;
            ConstructorInfo ctor = typeof(SettingsForm).GetConstructors()[0];
            object preview = ctor.GetParameters()[2].ParameterType == typeof(Action) ? (object)new Action(() => {}) : new Action<AlertKind>(kind => previewed = kind);
            using (SettingsForm form = (SettingsForm)ctor.Invoke(new object[] { settings, new Action(() => applies++), preview, new Action(() => {}), new Action(() => {}), new Action(() => {}) }))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.Show(); Application.DoEvents();
                ComboBox category = form.Controls.OfType<ComboBox>().FirstOrDefault(c => c.AccessibleName == "提醒类型");
                Check(category != null, "per-type sound category control is missing");
                ComboBox sound = form.Controls.OfType<ComboBox>().Single(c => c.AccessibleName == "提醒铃声");
                category.SelectedIndex = 0; Check(((SoundOption)sound.SelectedItem).Id == "chimes", "pending profile must load independently");
                sound.SelectedItem = sound.Items.Cast<SoundOption>().First(o => o.Id == "warm");
                Check(settings.NeedsInputSound.SoundId == "warm" && settings.CompletedSound.SoundId == "gentle", "editing pending must not replace completed");
                int before = applies; category.SelectedIndex = 1;
                Check(((SoundOption)sound.SelectedItem).Id == "gentle" && applies == before, "category switching must not save old values into another profile");
                settings.CompletedSound.CustomSoundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds", "gentle.wav");
                sound.SelectedItem = sound.Items.Cast<SoundOption>().First(o => o.Id == "custom");
                Check(settings.CompletedSound.SoundId == "custom" && settings.NeedsInputSound.CustomSoundPath == "", "custom files must be independent");
                category.SelectedIndex = 2;
                Check(((SoundOption)sound.SelectedItem).Id == "warm", "inactivity profile must retain default");
                form.Controls.OfType<Button>().Single(b => b.AccessibleName == "试听 8 秒").PerformClick();
                Check(previewed == AlertKind.Stalled, "preview must carry the selected alarm category");
                category.SelectedIndex = 1;
                Check(((SoundOption)sound.SelectedItem).Id == "custom", "custom choice must survive category round trip");
                using (Bitmap image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "per-type-sounds.png")); }
                form.Close();
            }
            Console.WriteLine("PASS: per-type settings isolate choices and custom files; category changes do not overwrite; preview uses correct category"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
