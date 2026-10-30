using System;
using System.Collections.Generic;

namespace CodexWatchdog
{
    public enum MonitorTransitionKind { Working, Completed, NeedsInput, Error, Interrupted, InputRequested }
    public sealed class MonitorTransition
    {
        public string RequestId { get; set; }
        public MonitorTransition() { TurnTrigger = ""; RequestId = ""; }
        public string TurnTrigger { get; set; }
        public string Id { get; set; }
        public string TurnId { get; set; }
        public MonitorTransitionKind Kind { get; set; }
        public DateTime OccurredUtc { get; set; }
    }
    public sealed class MonitorSnapshot
    {
        public string TurnTrigger { get; set; }
        public string SourceId { get; set; }
        public string Title { get; set; }
        public string TurnId { get; set; }
        public string Fingerprint { get; set; }
        public string AttentionId { get; set; }
        public DateTime ObservedUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public DateTime EndedUtc { get; set; }
        public DateTime AttentionUtc { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsGenerating { get; set; }
        public bool NeedsInput { get; set; }
        public bool HasError { get; set; }
        public bool ExplicitCompleted { get; set; }
        public bool WasInterrupted { get; set; }
        public List<MonitorTransition> Transitions { get; set; }
        public List<string> PendingRequestIds { get; set; }
        public bool HasBlockingInput { get; set; }
        public bool IsCaughtUp { get; set; }
        public string Detail { get; set; }
        public MonitorSnapshot()
        {
            TurnTrigger = ""; SourceId = ""; Title = "Codex 聊天"; TurnId = ""; Fingerprint = ""; AttentionId = "";
            Transitions = new List<MonitorTransition>(); PendingRequestIds = new List<string>(); IsCaughtUp = true;
            Detail = ""; IsAvailable = true;
        }
    }

    public enum AlertKind { Completed, NeedsInput, Error, Stalled, Unavailable, Interrupted }

    public sealed class WatchAlert
    {
        public WatchAlert() { TurnTrigger = ""; }
        public string TurnTrigger { get; set; }
        public string TurnId { get; set; }
        public string TransitionId { get; set; }
        public string Id { get; set; }
        public string SourceId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public AlertKind Kind { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class SoundProfile
    {
        public string SoundId { get; set; }
        public string CustomSoundPath { get; set; }
    }

    public sealed class WatchSettings
    {
        public bool MonitorAllSources { get; set; }
        public List<string> SelectedSourceIds { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ShowOnlyWithDesktopApp { get; set; }
        public bool StallAlertEnabled { get; set; }
        public string SoundId { get; set; }
        public string CustomSoundPath { get; set; }
        public SoundProfile CompletedSound { get; set; }
        public SoundProfile NeedsInputSound { get; set; }
        public SoundProfile InactivitySound { get; set; }
        public int StallMinutes { get; set; }
        public int SnoozeMinutes { get; set; }
        public int VolumePercent { get; set; }
        public string SelectedSourceId { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public WatchSettings()
        {
            MonitorAllSources = true; SelectedSourceIds = new List<string>();
            StartWithWindows = true; ShowOnlyWithDesktopApp = true;
            StallAlertEnabled = true; SoundId = "gentle"; CustomSoundPath = ""; StallMinutes = 10; SnoozeMinutes = 5; VolumePercent = 65;
            SelectedSourceId = ""; WindowX = -1; WindowY = -1;
        }
    }

    public sealed class SourcePollResult
    {
        public List<MonitorSnapshot> Snapshots { get; set; }
        public string Health { get; set; }
        public SourcePollResult() { Snapshots = new List<MonitorSnapshot>(); Health = ""; }
    }
}
