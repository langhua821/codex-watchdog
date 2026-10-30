using System;
using CodexWatchdog;
class PresenceTests {
 static void Check(bool ok,string message){if(!ok){Console.WriteLine("FAIL: "+message);Environment.Exit(1);}}
 static void Main(string[] args){string package=@"C:\Program Files\WindowsApps\OpenAI.Codex_26.1016.8622.0_x64__2p2nqsd0c76g0\app\";
 Check(DesktopPresence.IsDesktopCandidate("ChatGPT.exe",package+"ChatGPT.exe","Codex"),"actual desktop");
 Check(DesktopPresence.IsDesktopCandidate("Codex.exe",package+"Codex.exe","Codex"),"packaged desktop renamed executable");
 Check(!DesktopPresence.IsDesktopCandidate("codex.exe",@"C:\Users\test\AppData\Local\OpenAI\Codex\bin\hash\codex.exe",""),"actual CLI excluded");
 Check(!DesktopPresence.IsDesktopCandidate("codex.exe",@"C:\Users\test\AppData\Local\OpenAI\Codex\bin\hash\codex.exe","Codex Desktop"),"CLI location takes precedence");
 Check(!DesktopPresence.IsDesktopCandidate("codex.exe",null,null),"inaccessible CLI does not imply desktop");
 Check(!DesktopPresence.IsDesktopCandidate("codex.exe",@"C:\tools\codex.exe","Codex"),"unknown Codex executable not desktop by name");
 Check(!DesktopPresence.IsDesktopCandidate("ChatGPT.exe",@"C:\Other\ChatGPT.exe","ChatGPT"),"ChatGPT app is not Codex desktop");
 Check(DesktopPresence.IsDesktopCandidate("Codex",@"C:\Apps\Codex\Codex.exe","Codex Desktop"),"explicit desktop product");
 Console.WriteLine("Presence classification tests passed");if(args.Length>0)Console.WriteLine("DesktopRunning="+DesktopPresence.IsRunning()); }
}
