using System;
using System.IO;
using System.Collections.Generic;
using CodexWatchdog;
class AlertSoundRoutingTests
{
    static int checks;
    static void Check(bool condition,string name){if(!condition){Console.WriteLine("FAIL: "+name);throw new Exception(name);}checks++;}
    static void Main()
    {
        var s=new WatchSettings {SoundId="bright"};AlertSoundRouting.EnsureProfiles(s);
        Check(s.CompletedSound.SoundId=="bright"&&s.NeedsInputSound.SoundId=="chimes"&&s.InactivitySound.SoundId=="warm","legacy built in migration");
        s=new WatchSettings {SoundId="custom",CustomSoundPath=@"C:\My Sounds\bell.wav"};AlertSoundRouting.EnsureProfiles(s);Check(s.CompletedSound.SoundId=="custom"&&s.CompletedSound.CustomSoundPath==s.CustomSoundPath,"legacy custom migration");
        s.NeedsInputSound.SoundId="custom";s.NeedsInputSound.CustomSoundPath="question.wav";Check(s.CompletedSound.CustomSoundPath!=s.NeedsInputSound.CustomSoundPath&&s.InactivitySound.SoundId=="warm","custom profiles independent");
        s=new WatchSettings {CompletedSound=new SoundProfile {SoundId="own",CustomSoundPath="own.wav"}};AlertSoundRouting.EnsureProfiles(s);Check(s.CompletedSound.SoundId=="own"&&s.CompletedSound.CustomSoundPath=="own.wav"&&s.NeedsInputSound.SoundId=="chimes","partial config preserved");
        var shared=new SoundProfile {SoundId="tone"};s.CompletedSound=shared;s.NeedsInputSound=shared;s.InactivitySound=shared;AlertSoundRouting.EnsureProfiles(s);s.CompletedSound.SoundId="changed";Check(s.NeedsInputSound.SoundId=="tone"&&s.InactivitySound.SoundId=="tone"&&!Object.ReferenceEquals(s.NeedsInputSound,s.InactivitySound),"shared references separated");
        s=new WatchSettings();AlertSoundRouting.EnsureProfiles(s);Check(s.CompletedSound.SoundId=="gentle"&&s.NeedsInputSound.SoundId=="chimes"&&s.InactivitySound.SoundId=="warm","fresh defaults");
        s.CompletedSound.SoundId=" ";s.NeedsInputSound.CustomSoundPath=null;AlertSoundRouting.EnsureProfiles(s);Check(s.CompletedSound.SoundId=="gentle"&&s.NeedsInputSound.CustomSoundPath=="","empty normalization");
        Check(Object.ReferenceEquals(AlertSoundRouting.GetProfile(s,AlertKind.Interrupted),s.CompletedSound)&&Object.ReferenceEquals(AlertSoundRouting.GetProfile(s,AlertKind.NeedsInput),s.NeedsInputSound)&&Object.ReferenceEquals(AlertSoundRouting.GetProfile(s,AlertKind.Error),s.InactivitySound),"category routing");
        var list=new List<WatchAlert>();var time=DateTime.UtcNow;foreach(AlertKind kind in Enum.GetValues(typeof(AlertKind)))list.Add(new WatchAlert {Kind=kind,CreatedUtc=time,Id=kind.ToString()});Check(AlertSoundRouting.SelectAlert(list).Kind==AlertKind.NeedsInput,"waiting priority");
        AlertKind[] priority={AlertKind.NeedsInput,AlertKind.Error,AlertKind.Unavailable,AlertKind.Stalled,AlertKind.Interrupted,AlertKind.Completed};foreach(var kind in priority){Check(AlertSoundRouting.SelectAlert(list).Kind==kind,"priority "+kind);list.RemoveAll(delegate(WatchAlert a){return a.Kind==kind;});}
        list.Add(new WatchAlert {Kind=AlertKind.Completed,Id="z",CreatedUtc=time});list.Add(new WatchAlert {Kind=AlertKind.Completed,Id="a",CreatedUtc=time});list.Add(new WatchAlert {Kind=AlertKind.Completed,Id="first",CreatedUtc=time.AddSeconds(-1)});Check(AlertSoundRouting.SelectAlert(list).Id=="first","older first");list.RemoveAt(2);Check(AlertSoundRouting.SelectAlert(list).Id=="a","stable id order");Check(AlertSoundRouting.SelectAlert(null)==null&&AlertSoundRouting.SelectAlert(new WatchAlert[]{null})==null,"empty null input");
        string path=Path.Combine(Path.GetTempPath(),"watchdog-routing-"+Guid.NewGuid().ToString("N")+".json");
        try {s.StartWithWindows=false;s.CompletedSound.SoundId="custom";s.CompletedSound.CustomSoundPath="done.wav";Check(SettingsStore.Save(s,path),"save isolated config");var loaded=SettingsStore.Load(path);Check(!loaded.StartWithWindows&&loaded.CompletedSound.CustomSoundPath=="done.wav"&&loaded.NeedsInputSound.SoundId=="chimes","load readback preserves settings");File.WriteAllText(path,"{\"SoundId\":\"old\",\"StartWithWindows\":false}");loaded=SettingsStore.Load(path);Check(loaded.CompletedSound.SoundId=="old"&&!loaded.StartWithWindows,"load old config migration");File.WriteAllText(path,"broken");Check(SettingsStore.Load(path).CompletedSound.SoundId=="gentle","corrupt config fallback");}
        finally {if(File.Exists(path))File.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}
        Console.WriteLine("PASS: "+checks+" sound routing checks");
    }
}
