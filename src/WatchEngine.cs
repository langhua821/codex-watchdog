using System;
using System.Collections.Generic;

namespace CodexWatchdog
{
    // New input requests notify immediately; completed work must first remain stopped.
    public sealed class WatchEngine
    {
        const int MaxSources=128, MaxAlerts=256, MaxEvents=512;
        static readonly TimeSpan SettleTime=TimeSpan.FromSeconds(4);
        sealed class StopCandidate
        {
            public WatchAlert Alert;
            public DateTime FirstObserved;
        }
        sealed class State
        {
            public bool HasBaseline, Working;
            public string Turn="";
            public long Epoch;
            public DateTime LastSeen, LastObserved;
            public StopCandidate Candidate;
            public HashSet<string> Seen=new HashSet<string>(StringComparer.Ordinal);
            public Queue<string> EventOrder=new Queue<string>();
        }
        sealed class Pending {public WatchAlert Alert;public DateTime Until;public string RequestId="";}
        readonly WatchSettings settings;
        readonly Dictionary<string,State> states=new Dictionary<string,State>(StringComparer.Ordinal);
        readonly List<Pending> pending=new List<Pending>();
        bool paused, resumeBaseline;
        public WatchEngine(WatchSettings settings) {this.settings=settings??new WatchSettings();}
        public bool IsConfirmingStop {get{foreach(var state in states.Values)if(state.Candidate!=null)return true;return false;}}
        public bool Paused {get{return paused;}set{if(paused!=value){paused=value;Reset();resumeBaseline=!value;}}}
        public void Reset(){states.Clear();pending.Clear();resumeBaseline=false;}
        public void Acknowledge(string alertId){pending.RemoveAll(delegate(Pending p){return p.Alert.Id==alertId;});}
        public void Snooze(string alertId,DateTime nowUtc){foreach(var p in pending)if(p.Alert.Id==alertId)p.Until=nowUtc.AddMinutes(Math.Max(1,settings.SnoozeMinutes));}

        public List<WatchAlert> Update(IEnumerable<MonitorSnapshot> snapshots,DateTime nowUtc)
        {
            var result=new List<WatchAlert>();
            if(paused)return result;
            // The reader owns the bounded tracking membership. Losing that membership
            // is neither a stop nor a reason to keep an orphaned reminder ringing.
            var incoming=new List<MonitorSnapshot>();
            var incomingIds=new HashSet<string>(StringComparer.Ordinal);
            if(snapshots!=null)foreach(var item in snapshots)
                if(item!=null&&!String.IsNullOrEmpty(item.SourceId)&&incomingIds.Add(item.SourceId))incoming.Add(item);
            var noLongerTracked=new List<string>();
            foreach(var entry in states)if(!incomingIds.Contains(entry.Key))noLongerTracked.Add(entry.Key);
            foreach(string id in noLongerTracked)states.Remove(id);
            pending.RemoveAll(delegate(Pending p){return !incomingIds.Contains(p.Alert.SourceId);});
            var visited=new HashSet<string>(StringComparer.Ordinal);
            bool sawReadable=false;
            foreach(var snapshot in incoming)
            {
                if(snapshot==null||String.IsNullOrEmpty(snapshot.SourceId)||!visited.Add(snapshot.SourceId))continue;
                State state;
                if(!states.TryGetValue(snapshot.SourceId,out state))
                {
                    if(states.Count>=MaxSources&&!MakeRoom())continue;
                    state=new State();states.Add(snapshot.SourceId,state);
                }
                state.LastSeen=nowUtc;
                // Missing data is unknown, never a work-stop event or confirmation.
                if(!snapshot.IsAvailable||snapshot.ObservedUtc<state.LastObserved)continue;
                sawReadable=true;
                state.LastObserved=snapshot.ObservedUtc;
                bool initial=!state.HasBaseline;
                if(!state.HasBaseline)
                {
                    state.HasBaseline=true;state.Turn=snapshot.TurnId??"";
                    state.Working=snapshot.IsGenerating&&!snapshot.HasBlockingInput;
                    if(state.Working)state.Epoch++;
                }
                if(snapshot.Transitions!=null)foreach(var transition in snapshot.Transitions)
                {
                    if(transition==null)continue;
                    string id=EventId(transition);
                    if(!Remember(state,id))continue;
                    if(resumeBaseline)continue;
                    string turn=transition.TurnId??"";
                    if(transition.Kind==MonitorTransitionKind.InputRequested)
                    {
                        if(String.Equals(state.Turn,turn,StringComparison.Ordinal))NotifyInput(state,snapshot,transition,nowUtc);
                    }
                    else if(transition.Kind==MonitorTransitionKind.Working)Resume(state,snapshot.SourceId,turn);
                    else if(state.Working&&String.Equals(state.Turn,turn,StringComparison.Ordinal))
                        Stop(state,snapshot,Kind(transition.Kind),turn,id,transition.TurnTrigger,nowUtc);
                }
                string currentTurn=snapshot.TurnId??"";
                bool currentlyWorking=snapshot.IsGenerating&&!snapshot.HasBlockingInput;
                if(currentlyWorking)
                {
                    if(!state.Working||state.Turn!=currentTurn)Resume(state,snapshot.SourceId,currentTurn);
                }
                else if(state.Turn!=currentTurn)
                {
                    // A different stopped turn is not evidence of its unseen work phase.
                    state.Working=false;state.Candidate=null;state.Turn=currentTurn;
                }
                else if(state.Working)
                {
                    AlertKind? kind=null;
                    if(snapshot.HasBlockingInput||(!snapshot.IsGenerating&&snapshot.NeedsInput))kind=AlertKind.NeedsInput;
                    else if(!snapshot.IsGenerating)
                    {
                        if(snapshot.WasInterrupted)kind=AlertKind.Interrupted;
                        else if(snapshot.HasError)kind=AlertKind.Error;
                        else if(snapshot.ExplicitCompleted)kind=AlertKind.Completed;
                    }
                    if(kind.HasValue)Stop(state,snapshot,kind.Value,currentTurn,"snapshot:"+state.Epoch.ToString(),snapshot.TurnTrigger,nowUtc);
                }
                StopCandidate candidate=state.Candidate;
                bool stoppedWaiting=snapshot.HasBlockingInput||(!snapshot.IsGenerating&&snapshot.NeedsInput);
                pending.RemoveAll(delegate(Pending p){return p.Alert.SourceId==snapshot.SourceId&&p.Alert.Kind==AlertKind.NeedsInput&&
                    (p.Alert.TurnId!=currentTurn||(!(snapshot.NeedsInput||snapshot.HasBlockingInput))||
                    (!String.IsNullOrEmpty(p.RequestId)&&(snapshot.PendingRequestIds==null||!snapshot.PendingRequestIds.Contains(p.RequestId))));});
                if(candidate!=null&&candidate.Alert.Kind==AlertKind.NeedsInput&&!stoppedWaiting)
                {
                    state.Candidate=null;candidate=null;
                }
                if(candidate!=null&&!state.Working&&candidate.Alert.TurnId==currentTurn&&snapshot.IsCaughtUp&&
                    snapshot.ObservedUtc>=candidate.FirstObserved+SettleTime&&nowUtc>=candidate.FirstObserved+SettleTime&&
                    (candidate.Alert.Kind!=AlertKind.NeedsInput||stoppedWaiting))
                {
                    candidate.Alert.CreatedUtc=nowUtc;
                    if(pending.Count>=MaxAlerts)pending.RemoveAt(0);
                    pending.Add(new Pending {Alert=candidate.Alert});state.Candidate=null;
                }
                // Requests already present when attaching are history, not new notifications.
                if(initial&&snapshot.PendingRequestIds!=null)foreach(string requestId in snapshot.PendingRequestIds)
                    if(!String.IsNullOrEmpty(requestId))Remember(state,RequestKey(currentTurn,requestId));
            }
            var expired=new List<string>();
            foreach(var entry in states)if(nowUtc-entry.Value.LastSeen>TimeSpan.FromHours(24))expired.Add(entry.Key);
            foreach(string id in expired)states.Remove(id);
            if(sawReadable)resumeBaseline=false;
            foreach(var p in pending)if(p.Until<=nowUtc)result.Add(p.Alert);
            return result;
        }
        bool MakeRoom()
        {
            string victim=null;DateTime oldest=DateTime.MaxValue;
            foreach(var entry in states)
            {
                if(entry.Value.Working||entry.Value.Candidate!=null||HasPending(entry.Key))continue;
                if(victim==null||entry.Value.LastSeen<oldest)
                {
                    victim=entry.Key;oldest=entry.Value.LastSeen;
                }
            }
            if(victim==null)return false;
            states.Remove(victim);return true;
        }
        bool HasPending(string source){foreach(var p in pending)if(p.Alert.SourceId==source)return true;return false;}
        void Resume(State state,string source,string turn)
        {
            pending.RemoveAll(delegate(Pending p){return p.Alert.SourceId==source&&(p.Alert.Kind!=AlertKind.NeedsInput||p.Alert.TurnId!=turn);});
            state.Candidate=null;state.Working=true;state.Turn=turn;state.Epoch++;
        }
        static void Stop(State state,MonitorSnapshot snapshot,AlertKind kind,string turn,string transitionId,string turnTrigger,DateTime now)
        {
            state.Working=false;
            state.Candidate=null;
            if(kind==AlertKind.NeedsInput&&AlreadyNotified(state,snapshot,turn))return;
            state.Candidate=new StopCandidate {FirstObserved=now,Alert=new WatchAlert {
                Id=Guid.NewGuid().ToString("N"),SourceId=snapshot.SourceId,Title=snapshot.Title,
                TurnId=turn,TransitionId=transitionId,TurnTrigger=turnTrigger??"",Kind=kind,
                Message=kind==AlertKind.NeedsInput?"工作已停下，等待你的回答或授权":kind==AlertKind.Error?"工作已因错误停下":kind==AlertKind.Interrupted?"工作已中断":"本轮工作已结束"}};
        }
        void NotifyInput(State state,MonitorSnapshot snapshot,MonitorTransition transition,DateTime now)
        {
            string requestId=String.IsNullOrEmpty(transition.RequestId)?transition.Id:transition.RequestId;
            if(String.IsNullOrEmpty(requestId)||!Remember(state,RequestKey(transition.TurnId??"",requestId)))return;
            if(state.Candidate!=null&&state.Candidate.Alert.Kind==AlertKind.NeedsInput)state.Candidate=null;
            if(pending.Count>=MaxAlerts)pending.RemoveAt(0);
            pending.Add(new Pending {RequestId=requestId,Alert=new WatchAlert {
                Id=Guid.NewGuid().ToString("N"),SourceId=snapshot.SourceId,Title=snapshot.Title,
                TurnId=transition.TurnId??"",TransitionId=EventId(transition),TurnTrigger=transition.TurnTrigger??"",
                Kind=AlertKind.NeedsInput,CreatedUtc=now,Message="有新问题或授权请求，等待你的回答"}});
        }
        static string RequestKey(string turn,string requestId){return "request:"+turn.Length+":"+turn+":"+requestId;}
        static bool AlreadyNotified(State state,MonitorSnapshot snapshot,string turn)
        {
            if(snapshot.PendingRequestIds==null||snapshot.PendingRequestIds.Count==0)return false;
            foreach(string id in snapshot.PendingRequestIds)if(!state.Seen.Contains(RequestKey(turn,id??"")))return false;
            return true;
        }
        static AlertKind Kind(MonitorTransitionKind kind)
        {
            switch(kind){case MonitorTransitionKind.NeedsInput:return AlertKind.NeedsInput;case MonitorTransitionKind.Error:return AlertKind.Error;case MonitorTransitionKind.Interrupted:return AlertKind.Interrupted;default:return AlertKind.Completed;}
        }
        static string EventId(MonitorTransition transition)
        {
            return !String.IsNullOrEmpty(transition.Id)?transition.Id:(transition.TurnId??"")+":"+transition.Kind+":"+transition.OccurredUtc.Ticks.ToString();
        }
        static bool Remember(State state,string id)
        {
            if(!state.Seen.Add(id))return false;
            state.EventOrder.Enqueue(id);if(state.EventOrder.Count>MaxEvents)state.Seen.Remove(state.EventOrder.Dequeue());return true;
        }
    }
}
