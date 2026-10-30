using System;
using System.Diagnostics;
using System.IO;
namespace CodexWatchdog {
 public static class DesktopPresence {
  public static bool IsDesktopCandidate(string name,string executablePath,string productName){
   string processName=Path.GetFileNameWithoutExtension(name??"");
   if(!String.Equals(processName,"ChatGPT",StringComparison.OrdinalIgnoreCase)&&!String.Equals(processName,"Codex",StringComparison.OrdinalIgnoreCase))return false;
   if(String.IsNullOrEmpty(executablePath))return false;
   string path=executablePath.Replace('/','\\');
   // The bundled command-line binary is a separate process and may outlive the desktop.
   if(path.IndexOf(@"\OpenAI\Codex\bin\",StringComparison.OrdinalIgnoreCase)>=0)return false;
   bool packaged=path.IndexOf(@"\WindowsApps\OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)>=0&&path.IndexOf(@"\app\",StringComparison.OrdinalIgnoreCase)>=0;
   if(packaged)return true;
   if(String.Equals(productName,"Codex Desktop",StringComparison.OrdinalIgnoreCase))return true;
   return String.Equals(processName,"ChatGPT",StringComparison.OrdinalIgnoreCase)&&String.Equals(productName,"Codex",StringComparison.OrdinalIgnoreCase);
  }
  public static bool IsRunning(){
   foreach(string name in new[]{"ChatGPT","Codex"}){
    Process[] processes;try{processes=Process.GetProcessesByName(name);}catch{continue;}
    try{
     foreach(Process process in processes){try{
      string path=process.MainModule.FileName;
      string product="";try{product=FileVersionInfo.GetVersionInfo(path).ProductName;}catch{}
      if(IsDesktopCandidate(name,path,product))return true;
     }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(NotSupportedException){}}
    }finally{foreach(Process process in processes)process.Dispose();}
   }
   return false;
  }
 }
}
