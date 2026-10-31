using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CodexWatchdog;
class AlertSourcePopupTests
{
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll",EntryPoint="GetWindowLong")]static extern int GetWindowLong(IntPtr h,int index);
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static void Set(object o,string name,object value){o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);}
 static void Call(object o,string name,params object[] args){o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static IEnumerable<Control> Tree(Control root){yield return root;foreach(Control child in root.Controls)foreach(Control descendant in Tree(child))yield return descendant;}
 static string Read(Control root){return string.Join("\n",Tree(root).Select(c=>(c.AccessibleDescription??"")+"\n"+c.Text));}
 static void Pump(){Application.DoEvents();Thread.Sleep(20);Application.DoEvents();}
 static WatchAlert Alert(string id,string title,string message){return new WatchAlert{Id=id,SourceId=id,Title=title,Message=message,Kind=AlertKind.Completed};}
 static MonitorSnapshot Working(string id,string title,DateTime now){return new MonitorSnapshot{SourceId=id,Title=title,TurnId="test-turn",IsGenerating=true,ObservedUtc=now,LastActivityUtc=now};}
 static Form Popup(MainForm form){return (Form)Field(form,"sourcePopup");}
 static void CheckHidden(MainForm form,string reason){Check(!Popup(form).Visible,reason);}
 [STAThread]static int Main()
 {
  try
  {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(AlertSourcePopup popup=new AlertSourcePopup())
   {
    // Keep our own notification transparent while testing real native visibility/placement.
    popup.Opacity=0;IntPtr foreground=GetForegroundWindow();Rectangle area=Screen.PrimaryScreen.WorkingArea;
    var alerts=new List<WatchAlert>{Alert("one","Synthetic Alpha","Synthetic completion"),Alert("two","Synthetic Beta","Synthetic question")};
    popup.RefreshAlerts(alerts,new Rectangle(area.Right-72,area.Bottom-72,72,72));popup.Show();Pump();
    Check(popup.Visible,"nonempty alerts must expose source card");
    Check(GetForegroundWindow()==foreground,"showing source card must not activate it or steal focus");
    Check((GetWindowLong(popup.Handle,-20)&0x08000000)!=0,"source card must have WS_EX_NOACTIVATE");
    Check(!popup.ShowInTaskbar,"source card must not occupy taskbar");Check(area.Contains(popup.Bounds),"card must remain in screen working area");
    string contents=Read(popup);foreach(var alert in alerts)Check(contents.Contains(alert.Title)&&contents.Contains(alert.Message),"each source title and reason must both remain available");
    for(int i=0;i<4;i++)popup.RefreshAlerts(alerts,new Rectangle(area.Left,area.Top,72,72));Pump();
    Check(GetForegroundWindow()==foreground,"repeated source refresh must not steal focus");
    var many=new List<WatchAlert>();for(int i=0;i<30;i++)many.Add(Alert("many-"+i,"Synthetic page "+i,"Synthetic reason "+i));
    popup.RefreshAlerts(many,new Rectangle(area.Right-72,area.Top,72,72));Pump();contents=Read(popup);
    foreach(var alert in many)Check(contents.Contains(alert.Title)&&contents.Contains(alert.Message),"many-alert card must retain every title/reason pair");
    Check(Tree(popup).OfType<ScrollableControl>().Any(c=>c.AutoScroll),"many alerts require a scrollable container");Check(area.Contains(popup.Bounds),"many-alert card must stay inside work area");
    var longAlerts=new List<WatchAlert>{Alert("preview-alpha","示例页面甲：这是一个用于检查换行、完整来源标题与多条提醒配对的较长聊天名称","本轮工作已结束，等待确认"),Alert("preview-beta","示例页面乙：产品讨论与下一步安排","新的问题等待你的回答或授权")};
    popup.RefreshAlerts(longAlerts,new Rectangle(area.Right-144,area.Top+40,144,144));Pump();
    var labels=Tree(popup).OfType<Label>().OrderBy(c=>c.Top).ToList();
    Check(labels.Count==4&&labels[0].Text==longAlerts[0].Title&&labels[1].Text==longAlerts[0].Message&&labels[2].Text==longAlerts[1].Title&&labels[3].Text==longAlerts[1].Message,"source title and reason must stay paired in displayed order");
    foreach(var label in labels)Check(label.Height>=TextRenderer.MeasureText(label.Text,label.Font,new Size(label.Width,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height,"long title/reason must receive enough wrapping height");
    using(var preview=new Bitmap(popup.Width,popup.Height)){popup.DrawToBitmap(preview,new Rectangle(Point.Empty,preview.Size));preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"alert-source-preview.png"));}
    popup.RefreshAlerts(new List<WatchAlert>(),Rectangle.Empty);Pump();Check(!popup.Visible,"empty alerts hide card");
   }
   string home=Path.Combine(Path.GetTempPath(),"watchdog-source-card-"+Guid.NewGuid().ToString("N"));
   var settings=new WatchSettings{StartWithWindows=false,VolumePercent=0};bool present=true;
   using(MainForm form=new MainForm(settings,false,home,()=>present,"",s=>true))
   {
    form.Location=new Point(-20000,-20000);Popup(form).Opacity=0;form.Show();Pump();
    var worker=(System.Threading.Timer)Field(form,"pollTimer");worker.Change(Timeout.Infinite,Timeout.Infinite);while((int)Field(form,"polling")!=0)Thread.Sleep(5);Pump();((System.Windows.Forms.Timer)Field(form,"displayTimer")).Stop();
    WatchEngine engine=(WatchEngine)Field(form,"engine");engine.Reset();DateTime now=DateTime.UtcNow;
    var first=Working("synthetic-first","Synthetic task page",now.AddSeconds(-10));var second=Working("synthetic-second","Synthetic question page",now.AddSeconds(-10));
    var list=new List<MonitorSnapshot>{first,second};engine.Update(list,now.AddSeconds(-10));
    first.IsGenerating=false;first.ExplicitCompleted=true;first.ObservedUtc=now.AddSeconds(-5);second.ObservedUtc=now.AddSeconds(-5);engine.Update(list,now.AddSeconds(-5));
    first.ObservedUtc=now;second.ObservedUtc=now;second.NeedsInput=true;second.PendingRequestIds.Add("synthetic-request");second.Transitions.Add(new MonitorTransition{Id="synthetic-question-event",TurnId=second.TurnId,RequestId="synthetic-request",Kind=MonitorTransitionKind.InputRequested});engine.Update(list,now);
    Set(form,"latest",list);Call(form,"Render");Pump();Check(Popup(form).Visible,"actual simultaneous engine reminders show card");
    string content=Read(Popup(form));Check(content.Contains(first.Title)&&content.Contains(second.Title),"actual card must include both active pages");
    Check((form.AccessibleDescription??"").Contains(second.Title),"orb accessibility must identify the prioritized active page");
    Call(form,"Preview",AlertKind.Completed);Pump();CheckHidden(form,"preview must not pretend to be a page reminder");Call(form,"StopPreview");Check(Popup(form).Visible,"ending preview restores real reminders");
    Call(form,"Snooze");CheckHidden(form,"snoozing active reminders hides their card");
    // Bring the actual snoozed records due without sleeping for minutes.
    foreach(var due in engine.Update(list,now.AddMinutes(10)))engine.Snooze(due.Id,now.AddMinutes(-10));Call(form,"Render");Check(Popup(form).Visible,"due reminders show their sources again");
    Call(form,"Confirm");CheckHidden(form,"confirm removes source card");
    engine.Reset();second.Transitions.Clear();second.NeedsInput=false;second.PendingRequestIds.Clear();first.IsGenerating=true;first.ExplicitCompleted=false;foreach(var s in list)s.ObservedUtc=now.AddSeconds(-10);engine.Update(list,now.AddSeconds(-10));
    first.IsGenerating=false;first.ExplicitCompleted=true;foreach(var s in list)s.ObservedUtc=now.AddSeconds(-5);engine.Update(list,now.AddSeconds(-5));foreach(var s in list)s.ObservedUtc=now;engine.Update(list,now);Call(form,"Render");Check(Popup(form).Visible,"new stopped work shows card");
    first.IsGenerating=true;first.ExplicitCompleted=false;first.TurnId="synthetic-resumed";Call(form,"Render");CheckHidden(form,"resumed work removes obsolete completed source");
    second.NeedsInput=true;second.PendingRequestIds.Add("synthetic-later");second.Transitions.Add(new MonitorTransition{Id="synthetic-later-event",TurnId=second.TurnId,RequestId="synthetic-later",Kind=MonitorTransitionKind.InputRequested});Call(form,"Render");Check(Popup(form).Visible,"new selected question shows source");
    settings.MonitorAllSources=false;settings.SelectedSourceIds=new List<string>{first.SourceId};Call(form,"ApplySettings");CheckHidden(form,"excluding reminder source hides card");
    // Restore a synthetic selected request, then pause an actually visible card.
    engine.Reset();first.Transitions.Clear();first.NeedsInput=false;first.PendingRequestIds.Clear();first.ObservedUtc=DateTime.UtcNow;engine.Update(list,DateTime.UtcNow);
    first.NeedsInput=true;first.PendingRequestIds.Add("synthetic-pause");first.Transitions.Add(new MonitorTransition{Id="synthetic-pause-event",TurnId=first.TurnId,RequestId="synthetic-pause",Kind=MonitorTransitionKind.InputRequested});Set(form,"latest",list);Call(form,"Render");Check(Popup(form).Visible,"pause fixture card must be visible first");
    Call(form,"TogglePause");CheckHidden(form,"pause removes visible source card");Call(form,"TogglePause");
    engine.Reset();first.Transitions.Clear();first.NeedsInput=false;first.PendingRequestIds.Clear();engine.Update(list,DateTime.UtcNow);
    first.NeedsInput=true;first.PendingRequestIds.Add("synthetic-exit");first.Transitions.Add(new MonitorTransition{Id="synthetic-exit-event",TurnId=first.TurnId,RequestId="synthetic-exit",Kind=MonitorTransitionKind.InputRequested});Set(form,"latest",list);Call(form,"Render");Check(Popup(form).Visible,"exit fixture card must be visible first");
    present=false;Call(form,"Poll",new object[]{null});Pump();CheckHidden(form,"desktop exit removes visible source card");form.Close();Check(Popup(form).IsDisposed,"closing monitor disposes source card");
   }
   Console.WriteLine("PASS: source card native no-activation, bounded placement, complete multi-alert contents, and integrated reminder lifecycle");return 0;
  }
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
