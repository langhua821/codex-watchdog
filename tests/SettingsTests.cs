using System;
using System.Web.Script.Serialization;
using CodexWatchdog;
class SettingsTests {
 static void Main(){var j=new JavaScriptSerializer();var old=j.Deserialize<WatchSettings>("{\"StallMinutes\":10}");if(!old.StallAlertEnabled||!old.StartWithWindows||!old.ShowOnlyWithDesktopApp)throw new Exception("legacy default");var disabled=j.Deserialize<WatchSettings>("{\"StallAlertEnabled\":false,\"StartWithWindows\":false,\"ShowOnlyWithDesktopApp\":false}");if(disabled.StallAlertEnabled||disabled.StartWithWindows||disabled.ShowOnlyWithDesktopApp)throw new Exception("explicit false");Console.WriteLine("PASS: legacy JSON defaults enabled; explicit false preserved");}
}
