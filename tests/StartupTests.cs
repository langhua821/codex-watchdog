using System;
using Microsoft.Win32;
using CodexWatchdog;
class StartupTests
{
    static int checks;
    static void Check(bool condition,string name) {if(!condition){Console.WriteLine("FAIL: "+name);throw new Exception(name);}checks++;}
    static void Main()
    {
        string keyPath="Software\\CodexWatchdogTests\\"+Guid.NewGuid().ToString("N");
        try
        {
            using(var key=Registry.CurrentUser.CreateSubKey(keyPath))key.SetValue("OtherApplication","keep-me");
            var registration=new StartupRegistration(keyPath);
            string executable="C:\\Users\\Test User\\My Tools\\CodexWatchdog.exe";
            string error;
            Check(!registration.IsEnabled(executable),"initially disabled");
            Check(registration.TrySetEnabled(true,executable,out error)&&error=="","enable succeeds");
            using(var key=Registry.CurrentUser.OpenSubKey(keyPath))Check((string)key.GetValue("CodexWatchdog")=="\""+executable+"\"","quotes full spaced path");
            Check(registration.IsEnabled(executable),"enabled readback");
            Check(!registration.IsEnabled("C:\\Other\\CodexWatchdog.exe"),"different executable not enabled");
            Check(!registration.TrySetEnabled(true,"relative.exe",out error)&&error.Length>0,"reject relative path");
            Check(!registration.TrySetEnabled(true,"C:relative.exe",out error)&&error.Length>0,"reject drive relative path");
            Check(!registration.TrySetEnabled(true,"C:\\Bad\"Path\\app.exe",out error)&&error.Length>0,"reject command injection quote");
            Check(registration.IsEnabled(executable),"invalid update preserves existing value");
            Check(registration.TrySetEnabled(false,executable,out error),"disable succeeds");
            Check(!registration.IsEnabled(executable),"disabled readback");
            Check(registration.TrySetEnabled(false,executable,out error),"disable idempotent");
            using(var key=Registry.CurrentUser.OpenSubKey(keyPath))Check((string)key.GetValue("OtherApplication")=="keep-me","other value preserved");
            var invalidRegistry=new StartupRegistration(null);
            Check(!invalidRegistry.TrySetEnabled(true,executable,out error)&&error.Length>0,"registry exception reported");
            Check(!invalidRegistry.IsEnabled(executable),"registry exception read returns false");
            Console.WriteLine("PASS: "+checks+" startup checks");
        }
        finally {Registry.CurrentUser.DeleteSubKeyTree(keyPath,false);}
    }
}
