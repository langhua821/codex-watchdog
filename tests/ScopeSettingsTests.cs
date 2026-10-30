using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CodexWatchdog;
class ScopeSettingsTests
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
 static int Main(){string file=Path.Combine(Path.GetTempPath(),"watchdog-scope-"+Guid.NewGuid().ToString("N")+".json");try{
  File.WriteAllText(file,"{\"SelectedSourceId\":\"old-chat\",\"StartWithWindows\":false,\"SoundId\":\"custom\",\"CustomSoundPath\":\"bell.wav\"}");
  var s=SettingsStore.Load(file);Check(!s.MonitorAllSources&&s.SelectedSourceIds.SequenceEqual(new[]{"old-chat"}),"legacy single migrates to one checked chat");Check(!s.StartWithWindows&&s.CompletedSound.CustomSoundPath=="bell.wav","migration preserves startup and audio");
  File.WriteAllText(file,"{\"SelectedSourceId\":\"\"}");s=SettingsStore.Load(file);Check(s.MonitorAllSources&&s.SelectedSourceIds.Count==0,"legacy all stays all");
  s.MonitorAllSources=false;s.SelectedSourceIds=new List<string>{"a","b","a"," ",null};Check(SettingsStore.Save(s,file),"save multiple");s=SettingsStore.Load(file);Check(!s.MonitorAllSources&&s.SelectedSourceIds.SequenceEqual(new[]{"a","b"}),"two chosen chats survive reload without duplicates");
  s.SelectedSourceIds.Clear();Check(SettingsStore.Save(s,file),"save none");s=SettingsStore.Load(file);Check(!s.MonitorAllSources&&s.SelectedSourceIds.Count==0,"none stays none after restart, never silently all");
  File.WriteAllText(file,"{\"MonitorAllSources\":false,\"SelectedSourceIds\":null,\"SelectedSourceId\":\"old\"}");s=SettingsStore.Load(file);Check(!s.MonitorAllSources&&s.SelectedSourceIds.Count==0,"explicit none overrides legacy single");
  s.MonitorAllSources=true;Check(SettingsStore.Save(s,file),"save all");Check(SettingsStore.Load(file).MonitorAllSources,"explicit all survives restart");
  Console.WriteLine("PASS: legacy scope migration, explicit multiple/none/all persistence, independent audio/startup preservation");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(File.Exists(file))File.Delete(file);if(File.Exists(file+".tmp"))File.Delete(file+".tmp");}}
}
