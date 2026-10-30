using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;
using CodexWatchdog;
class PerTypePlaybackTests
{
 static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);}
 static void Call(object o,string n,params object[] a){o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static MediaPlayer Media(MainForm f){return (MediaPlayer)Field(Field(f,"alarm"),"media");}
 static void Wait(Func<bool> ok){DateTime end=DateTime.UtcNow.AddSeconds(8);while(!ok()){Application.DoEvents();Thread.Sleep(15);if(DateTime.UtcNow>end)throw new Exception("media did not open");}Application.DoEvents();}
 static MediaPlayer Expect(MainForm f,string filename){MediaPlayer p=Media(f);Check(p!=null,"missing media "+filename);Wait(()=>p.NaturalDuration.HasTimeSpan);Check(Path.GetFileName(p.Source.LocalPath)==filename,"wrong actual source: "+p.Source);Check(p.Volume==0,"test audio must remain muted");return p;}
 static MonitorSnapshot Snap(string id){return new MonitorSnapshot{SourceId=id,Title=id,TurnId="t",Fingerprint="run",IsGenerating=true,LastActivityUtc=DateTime.UtcNow.AddSeconds(-5)};}
 [STAThread]static int Main(){string home=Path.Combine(Path.GetTempPath(),"watchdog-pertype-"+Guid.NewGuid().ToString("N"));try{
 Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
 WatchSettings settings=new WatchSettings{StartWithWindows=false,VolumePercent=0,StallMinutes=1};AlertSoundRouting.EnsureProfiles(settings);
 using(MainForm f=new MainForm(settings,false,home,()=>true,"",s=>true)){
 f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();((System.Windows.Forms.Timer)Field(f,"displayTimer")).Stop();((System.Threading.Timer)Field(f,"pollTimer")).Dispose();Thread.Sleep(50);Application.DoEvents();
 WatchEngine engine=(WatchEngine)Field(f,"engine");engine.Reset();DateTime now=DateTime.UtcNow;
 MonitorSnapshot done=Snap("done"),input=Snap("input"),error=Snap("error");List<MonitorSnapshot> list=new List<MonitorSnapshot>{done,input,error};
 foreach(MonitorSnapshot s in list)s.ObservedUtc=now.AddSeconds(-20);engine.Update(list,now.AddSeconds(-20));
 done.IsGenerating=false;done.ExplicitCompleted=true;done.EndedUtc=now.AddSeconds(-15);done.Fingerprint="done";
 input.IsGenerating=false;input.NeedsInput=true;input.HasBlockingInput=true;input.AttentionId="q";input.AttentionUtc=now.AddSeconds(-15);input.Fingerprint="q";
 foreach(MonitorSnapshot s in list)s.ObservedUtc=now.AddSeconds(-10);engine.Update(list,now.AddSeconds(-10));
 Check(engine.Update(list,now.AddSeconds(-5)).Count==0,"stale render must not confirm stop candidate");
 foreach(MonitorSnapshot s in list)s.ObservedUtc=now.AddSeconds(-5);engine.Update(list,now.AddSeconds(-5));Set(f,"latest",list);Call(f,"Render");Check(f.AccessibleDescription.Contains("待回答"),"orb must prioritize input");MediaPlayer old=Expect(f,"chimes.wav");
 foreach(WatchAlert a in engine.Update(list,now))if(a.Kind==AlertKind.NeedsInput)engine.Acknowledge(a.Id);Call(f,"Render");Check(old.Source==null,"old input player must close");old=Expect(f,"gentle.wav");Check(f.AccessibleDescription.Contains("已结束"),"orb must match completion");
 error.IsGenerating=false;error.HasError=true;error.AttentionId="err";error.AttentionUtc=now.AddSeconds(-4);error.Fingerprint="error";
 foreach(MonitorSnapshot s in list)s.ObservedUtc=now.AddSeconds(-4);engine.Update(list,now.AddSeconds(-4));
 foreach(MonitorSnapshot s in list)s.ObservedUtc=now;engine.Update(list,now);Call(f,"Render");Check(old.Source==null,"old completed player must close");old=Expect(f,"warm.wav");Check(f.AccessibleDescription.Contains("有异常"),"stopped error must match orb");
 Call(f,"Preview",AlertKind.Completed);Check(old.Source==null,"old error player must close on preview");MediaPlayer preview=Expect(f,"gentle.wav");settings.NeedsInputSound.SoundId="warm";Call(f,"ApplySettings");Check(Object.ReferenceEquals(preview,Media(f)),"editing other profile must not restart or replace preview");Expect(f,"gentle.wav");
 Set(f,"testUntilUtc",DateTime.UtcNow.AddSeconds(-1));Call(f,"Render");Check(preview.Source==null,"expired preview must close");old=Expect(f,"warm.wav");
 // Resume every source: each previous stop is obsolete even without manual confirmation.
 foreach(MonitorSnapshot s in list){s.TurnId="resumed";s.IsGenerating=true;s.NeedsInput=false;s.HasBlockingInput=false;s.HasError=false;s.ExplicitCompleted=false;s.Fingerprint="resumed";s.ObservedUtc=DateTime.UtcNow;}
 Call(f,"Render");Check(old.Source==null&&Media(f)==null,"new work must close stale stopped music");Check(!((AlarmSound)Field(f,"alarm")).IsPlaying,"resuming must revoke all same-source old alerts");Check(f.AccessibleDescription.Contains("运行中"),"new work must replace old ended status");
 Call(f,"Confirm");Check(!((AlarmSound)Field(f,"alarm")).IsPlaying&&Media(f)==null,"confirm must stop and close player");f.Close();
 }
 Console.WriteLine("PASS: real muted MediaPlayer paths and opened durations; fresh stable stop input/completion/error priority and resumed-work cancellation; locked preview; other-profile edit; old players closed; confirm stops");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
