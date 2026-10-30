using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using CodexWatchdog;
class HeartbeatAlarmTests {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();static string file;static DateTime stamp;
 static void Add(string type,object payload){File.AppendAllText(file,Json.Serialize(new{timestamp=stamp.ToString("o"),type=type,payload=payload})+"\n");stamp=stamp.AddMilliseconds(1);}
 static void Check(bool b,string text){if(!b){Console.WriteLine("FAIL: "+text);Environment.Exit(1);}}
 static void Start(string turn,string trigger){if(trigger==null)Add("event_msg",new{type="task_started",turn_id=turn,root_turn_id=turn});else Add("event_msg",new{type="task_started",turn_id=turn,root_turn_id=turn,turn_attribution=new{turn_id=turn,turn_trigger=trigger,parent_turn_id=(string)null,initiating_agent_path=(string)null,root_turn_id=turn}});}
 static void End(string turn){Add("event_msg",new{type="task_complete",turn_id=turn,root_turn_id=turn,started_at=stamp.AddSeconds(-11).ToString("o"),completed_at=stamp.ToString("o"),duration_ms=11054,time_to_first_token_ms=1000});}
 static void Scenario(string trigger,string mode,int expected){string home=Path.Combine(Path.GetTempPath(),"heartbeat-test-"+Guid.NewGuid());Directory.CreateDirectory(Path.Combine(home,"sessions/2026/10/25"));file=Path.Combine(home,"sessions/2026/10/25/rollout-main.jsonl");stamp=new DateTime(2026,10,25,4,0,0,DateTimeKind.Utc);try{Add("session_meta",new{id="main",originator="Codex Desktop",source="vscode"});using(var source=new CodexSessionSource(home)){var engine=new WatchEngine(new WatchSettings());engine.Update(source.Poll(stamp).Snapshots,stamp);Start("turn",trigger);var live=source.Poll(stamp);Check(live.Snapshots.Single().TurnTrigger==(trigger??""),"source preserves exact trigger metadata");engine.Update(live.Snapshots,stamp);
 if(mode=="blocking"||mode=="async")Add("response_item",new{type="function_call",name=mode=="async"?"functions.request_user_input_async":"functions.request_user_input",call_id="question",arguments="{}"});else{if(mode=="error"){Add("event_msg",new{type="error"});Check(engine.Update(source.Poll(stamp).Snapshots,stamp).Count==0,"heartbeat error while working is not stop");}End("turn");}
 var now=stamp;Check(engine.Update(source.Poll(now).Snapshots,now).Count==((mode=="blocking"||mode=="async")?1:0),"questions immediate and completion settles");var alarms=engine.Update(source.Poll(now.AddSeconds(5)).Snapshots,now.AddSeconds(5));Console.WriteLine("Replay trigger="+(trigger??"missing")+" mode="+mode+" alarms="+alarms.Count);Check(alarms.Count==expected,"expected "+expected+" alarm for "+(trigger??"missing")+" "+mode);}}
 finally{Directory.Delete(home,true);}}
 static void Main(){Scenario("automation_heartbeat_scheduled","complete",1);Scenario("user","complete",1);Scenario(null,"complete",1);Scenario("goal_continuation","complete",1);Scenario("automation_heartbeat_scheduled","blocking",1);Scenario("automation_heartbeat_scheduled","async",1);Scenario("automation_heartbeat_scheduled","error",1);Console.WriteLine("Heartbeat alarm replay tests passed");}
}
