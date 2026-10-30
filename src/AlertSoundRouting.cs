using System;
using System.Collections.Generic;

namespace CodexWatchdog
{
    public static class AlertSoundRouting
    {
        public static void EnsureProfiles(WatchSettings settings)
        {
            if(settings==null)return;
            settings.SoundId=String.IsNullOrWhiteSpace(settings.SoundId)?"gentle":settings.SoundId.Trim();
            settings.CustomSoundPath=settings.CustomSoundPath??"";
            // Clone only aliased profiles; callers may retain a profile reference while editing.
            if(settings.CompletedSound!=null&&Object.ReferenceEquals(settings.CompletedSound,settings.NeedsInputSound))settings.NeedsInputSound=Copy(settings.NeedsInputSound);
            if(settings.InactivitySound!=null&&(Object.ReferenceEquals(settings.InactivitySound,settings.CompletedSound)||Object.ReferenceEquals(settings.InactivitySound,settings.NeedsInputSound)))settings.InactivitySound=Copy(settings.InactivitySound);
            if(settings.CompletedSound==null)settings.CompletedSound=new SoundProfile {SoundId=settings.SoundId,CustomSoundPath=settings.CustomSoundPath};
            if(settings.NeedsInputSound==null)settings.NeedsInputSound=new SoundProfile {SoundId="chimes",CustomSoundPath=""};
            if(settings.InactivitySound==null)settings.InactivitySound=new SoundProfile {SoundId="warm",CustomSoundPath=""};
            Normalize(settings.CompletedSound,"gentle");Normalize(settings.NeedsInputSound,"chimes");Normalize(settings.InactivitySound,"warm");
        }
        static SoundProfile Copy(SoundProfile profile) {return new SoundProfile {SoundId=profile.SoundId,CustomSoundPath=profile.CustomSoundPath};}
        static void Normalize(SoundProfile profile,string fallback)
        {
            profile.SoundId=String.IsNullOrWhiteSpace(profile.SoundId)?fallback:profile.SoundId.Trim();
            profile.CustomSoundPath=String.IsNullOrWhiteSpace(profile.CustomSoundPath)?"":profile.CustomSoundPath;
        }
        public static SoundProfile GetProfile(WatchSettings settings,AlertKind kind)
        {
            if(settings==null)settings=new WatchSettings();
            EnsureProfiles(settings);
            if(kind==AlertKind.NeedsInput)return settings.NeedsInputSound;
            if(kind==AlertKind.Completed||kind==AlertKind.Interrupted)return settings.CompletedSound;
            return settings.InactivitySound;
        }
        public static WatchAlert SelectAlert(IEnumerable<WatchAlert> alerts)
        {
            WatchAlert selected=null;
            if(alerts==null)return null;
            foreach(var alert in alerts)
            {
                if(alert==null)continue;
                if(selected==null||Compare(alert,selected)<0)selected=alert;
            }
            return selected;
        }
        static int Compare(WatchAlert a,WatchAlert b)
        {
            int order=Rank(a.Kind).CompareTo(Rank(b.Kind));
            if(order!=0)return order;
            order=a.CreatedUtc.CompareTo(b.CreatedUtc);
            return order!=0?order:String.Compare(a.Id,b.Id,StringComparison.Ordinal);
        }
        static int Rank(AlertKind kind)
        {
            switch(kind)
            {
                case AlertKind.NeedsInput:return 0;
                case AlertKind.Error:return 1;
                case AlertKind.Unavailable:return 2;
                case AlertKind.Stalled:return 3;
                case AlertKind.Interrupted:return 4;
                default:return 5;
            }
        }
    }
}
