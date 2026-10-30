using System;
using System.IO;
using Microsoft.Win32;

namespace CodexWatchdog
{
    /// <summary>Owns only CodexWatchdog's current-user startup registry value.</summary>
    public sealed class StartupRegistration
    {
        public const string DefaultKeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName="CodexWatchdog";
        readonly string keyPath;
        public StartupRegistration():this(DefaultKeyPath) {}
        // A separate HKCU path allows real registry tests without changing startup settings.
        public StartupRegistration(string keyPath) {this.keyPath=keyPath;}

        public bool TrySetEnabled(bool enabled,string executablePath,out string error)
        {
            error="";
            try
            {
                string command=enabled?BuildCommand(executablePath):null;
                if(enabled)
                {
                    using(var key=Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        if(key==null)throw new IOException("无法打开当前用户启动注册项。");
                        key.SetValue(ValueName,command,RegistryValueKind.String);
                        if(!String.Equals(key.GetValue(ValueName) as string,command,StringComparison.Ordinal))throw new IOException("启动注册项写入后校验失败。");
                    }
                }
                else
                {
                    using(var key=Registry.CurrentUser.OpenSubKey(keyPath,true))
                    {
                        if(key!=null)
                        {
                            key.DeleteValue(ValueName,false);
                            if(key.GetValue(ValueName)!=null)throw new IOException("启动注册项删除后校验失败。");
                        }
                    }
                }
                return true;
            }
            catch(Exception ex) {error="无法更新开机启动设置："+ex.Message;return false;}
        }

        public bool IsEnabled(string executablePath)
        {
            try
            {
                string command=BuildCommand(executablePath);
                using(var key=Registry.CurrentUser.OpenSubKey(keyPath,false))
                    return key!=null&&String.Equals(key.GetValue(ValueName) as string,command,StringComparison.OrdinalIgnoreCase);
            }
            catch {return false;}
        }

        static string BuildCommand(string executablePath)
        {
            if(String.IsNullOrEmpty(executablePath)||executablePath.IndexOf('"')>=0||executablePath.IndexOf('\r')>=0||executablePath.IndexOf('\n')>=0||!Path.IsPathRooted(executablePath))
                throw new ArgumentException("需要不含引号或换行的完整程序路径。");
            string full=Path.GetFullPath(executablePath);
            // Drive-relative paths such as C:app.exe are not absolute executable paths.
            if(!full.StartsWith(@"\\",StringComparison.Ordinal)&&!(executablePath.Length>=3&&executablePath[1]==':'&&(executablePath[2]=='\\'||executablePath[2]=='/')))
                throw new ArgumentException("需要含盘符或共享位置的完整程序路径。");
            return "\""+full+"\"";
        }
    }
}
