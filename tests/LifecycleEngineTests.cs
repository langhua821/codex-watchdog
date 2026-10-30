using System;
using CodexWatchdog;
class LifecycleEngineTests
{
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    static MonitorSnapshot State(DateTime time, string turn, bool running, bool input)
    {
        return new MonitorSnapshot { SourceId = "desktop", TurnId = turn, Fingerprint = time.Ticks.ToString(), ObservedUtc=time, LastActivityUtc = time, IsGenerating = running, NeedsInput = input, AttentionId = input ? "question" : "", AttentionUtc = input ? time : DateTime.MinValue, PendingRequestIds=input?new System.Collections.Generic.List<string>{"question"}:new System.Collections.Generic.List<string>() };
    }
    static int Main()
    {
        try
        {
            DateTime t = DateTime.UtcNow; WatchEngine engine = new WatchEngine(new WatchSettings());
            engine.Update(new[] { State(t, "old", true, false) }, t);
            MonitorSnapshot liveRequest=State(t.AddSeconds(1),"old",true,true);
            liveRequest.Transitions.Add(new MonitorTransition {Id="live-question-event",RequestId="question",TurnId="old",Kind=MonitorTransitionKind.InputRequested,OccurredUtc=t.AddSeconds(1)});
            Check(engine.Update(new[] { liveRequest }, t.AddSeconds(1)).Count == 1, "new asynchronous request during work must notify immediately");
            MonitorSnapshot blocked=State(t.AddSeconds(2),"old",true,true);blocked.HasBlockingInput=true;
            Check(engine.Update(new[]{blocked},t.AddSeconds(2)).Count==1&&!engine.IsConfirmingStop,"same request stopping must not create a duplicate candidate");
            blocked.ObservedUtc=t.AddSeconds(6);
            Check(engine.Update(new[]{blocked},t.AddSeconds(6)).Count==1,"stable blocking stop must alert");
            engine.Reset(); // Desktop app exits: discard old ringing and establish a quiet next session.
            Check(engine.Update(new MonitorSnapshot[0], t.AddSeconds(3)).Count == 0, "empty restart read must stay quiet");
            MonitorSnapshot unavailable = State(t, "old", true, true); unavailable.IsAvailable = false;
            Check(engine.Update(new[] { unavailable }, t.AddSeconds(4)).Count == 0, "unreadable first record must stay quiet");
            Check(engine.Update(new[] { State(t.AddSeconds(1), "old", true, true) }, t.AddSeconds(5)).Count == 0, "restored historical question must not ring on reopen");
            engine.Update(new[] { State(t.AddSeconds(6), "new", true, false) }, t.AddSeconds(6));
            MonitorSnapshot done = State(t.AddSeconds(7), "new", false, false); done.ExplicitCompleted = true; done.EndedUtc = t.AddSeconds(7);
            Check(engine.Update(new[] { done }, t.AddSeconds(7)).Count == 0, "new completion after reopen must settle");
            done.ObservedUtc=t.AddSeconds(11);
            Check(engine.Update(new[] { done }, t.AddSeconds(11)).Count == 1, "stable new completion after reopen must ring");
            engine.Paused = true; engine.Reset(); Check(engine.Paused, "desktop restart must preserve manual pause");
            Check(engine.Update(new[] { done }, t.AddSeconds(30)).Count == 0, "paused restart must stay silent");
            Console.WriteLine("PASS: desktop restart clears alarms, quiet baseline survives missing records, new completion rings, pause preserved"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
