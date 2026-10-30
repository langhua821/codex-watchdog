using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using CodexWatchdog;
class DropdownRefreshTests
{
 static int failures;
 static object Field(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static void Call(object o,string n){o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
 static void Pump(int ms){DateTime end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}}
 static void Open(SettingsForm w,string name){w.Activate();if(name=="targets")((Button)Field(w,"targets")).PerformClick();else {ComboBox c=(ComboBox)Field(w,name);c.Focus();c.DroppedDown=true;}Pump(70);}
 static bool IsOpen(SettingsForm w,string name){return name=="targets"?((ToolStripDropDown)Field(w,"targetPopup")).Visible:((ComboBox)Field(w,name)).DroppedDown;}
 static void Close(SettingsForm w,string name){if(name=="targets")((ToolStripDropDown)Field(w,"targetPopup")).Close();else ((ComboBox)Field(w,name)).DroppedDown=false;}
 static void Check(bool ok,string label){Console.WriteLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)failures++;}
 [STAThread]static int Main()
 {
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  WatchSettings settings=new WatchSettings{VolumePercent=0,StartWithWindows=false};
  using(MainForm ball=new MainForm(settings,false,Path.Combine(Path.GetTempPath(),"watchdog-dropdown-"+Guid.NewGuid().ToString("N")),()=>true,"",x=>true))
  using(SettingsForm window=new SettingsForm(new WatchSettings(),()=>{},k=>{},()=>{},()=>{},()=>{}))
  {
   ball.Location=new Point(-20000,-20000);ball.Show();Pump(100);
   ((System.Windows.Forms.Timer)Field(ball,"displayTimer")).Stop();((System.Threading.Timer)Field(ball,"pollTimer")).Dispose();
   while((int)Field(ball,"polling")!=0)Thread.Sleep(10);Pump(100);
   window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-21000,-20000);window.Show();Pump(100);
   var empty=new List<MonitorSnapshot>();window.RefreshStatus(empty,"idle","",false,false,"");
   foreach(string name in new[]{"sounds","categories","targets"})
   {
    Open(window,name);
    Check(IsOpen(window,name),name+" actually opened before render");
    Call(ball,"Render");Pump(70);Check(IsOpen(window,name),name+" stays open across orb render");Close(window,name);
   }
   int sequence=0;
   foreach(string name in new[]{"sounds","categories","targets"})
   {
    // A target discovery/title change must not mutate the list while any dropdown is open.
    window.RefreshStatus(empty,"idle","",false,false,"");CheckedListBox target=(CheckedListBox)Field(window,"targetChoices");int before=target.Items.Count;
    Open(window,name);
    var changed=new List<MonitorSnapshot>{new MonitorSnapshot{SourceId="new-"+(++sequence),Title="new target "+sequence}};
    window.RefreshStatus(changed,"updated status","",false,false,"");Pump(70);
    Check(IsOpen(window,name),name+" stays open during target discovery");Check(target.Items.Count==before,name+" defers target list mutation while open");
    Close(window,name);Pump(30);window.RefreshStatus(changed,"updated status","",false,false,"");
    Check(target.Items.Count==2,name+" applies deferred latest targets after close");
   }
   window.Close();ball.Close();
  }
  Console.WriteLine("Dropdown refresh failures="+failures);return failures==0?0:1;
 }
}
