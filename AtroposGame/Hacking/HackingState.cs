using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Threading.Tasks;
using System.Threading;
using Nito.AsyncEx;
using System.Numerics;
using com.Atropos.Machine_Learning;

namespace com.Atropos.Hacking
{
    class HackingAttemptState
    {
        public StateAspect authority;
        public StateAspect suspicion;
        public StateAspect alert;
        public StateAspect this[StateAspect comparisonState]
        {
            get
            {
                if (comparisonState.stateType == StateAspect.StateType.Authority) return authority;
                else if (comparisonState.stateType == StateAspect.StateType.Suspicion) return suspicion;
                else return alert;
            }
            set
            {
                if (comparisonState.stateType == StateAspect.StateType.Authority) authority = value;
                else if (comparisonState.stateType == StateAspect.StateType.Suspicion) suspicion = value;
                else alert = value;
            }
        }

        public Dictionary<string, bool> Objectives;
        public Dictionary<string, bool> Conditions;
        // The above are actually 'topologically' equivalent - both serve as a repository of named flags, specific to this run, which start out false
        // but could become true as the result of the hacking run.  Stuff like paydata, cameras-spoofed, or voiceprint-acquired.  The only
        // real difference is conceptual, for the simplicity of GMs and so forth, and some small difference if you want to know if *all*
        // of the objectives have been met (ditto 'at least one' and so forth).

        public HackingAttemptState()
        {
            authority = Authority.AuthPrompt;
            suspicion = Suspicion.None;
            alert = Alert.None;

            Objectives = new Dictionary<string, bool>();
            Conditions = new Dictionary<string, bool>();
        }

        public HackingAttemptState WithObjectives(params string[] objs)
        {
            foreach (var obj in objs) Objectives.Add(obj, false);
            return this;
        }

        public HackingAttemptState WithConditionFlags(params string[] conditions)
        {
            foreach (var obj in conditions) Conditions.Add(obj, false);
            return this;
        }

    }


    public class StateAspect
    {
        public enum StateType { Authority, Suspicion, Alert };
        public StateType stateType;
        public int severity;

        // Three separate factory methods made it easy to define them in their clusters.
        public static StateAspect Auth(int severity)
        {
            return new StateAspect() { stateType = StateType.Authority, severity = severity };
        }

        public static StateAspect Susp(int severity)
        {
            return new StateAspect() { stateType = StateType.Suspicion, severity = severity };
        }

        public static StateAspect Alert(int severity)
        {
            return new StateAspect() { stateType = StateType.Alert, severity = severity };
        }
    }

    public static class Authority
    {
        public static StateAspect LockedOut = StateAspect.Auth(-1),
                            AuthPrompt = StateAspect.Auth(0),
                            ReadOnly = StateAspect.Auth(1),
                            ReadWrite = StateAspect.Auth(2),
                            ElevatedPrivs = StateAspect.Auth(3),
                            SpecificUser = StateAspect.Auth(4),
                            Root = StateAspect.Auth(5);
    }
    public static class Suspicion
    {
        public static StateAspect None = StateAspect.Susp(0),
                            ForensicTraces = StateAspect.Susp(1),
                            EvidentTraces = StateAspect.Susp(2),
                            IdentifiableTraces = StateAspect.Susp(3),
                            UnusualActivityFlagged = StateAspect.Susp(4),
                            SysopSuspects = StateAspect.Susp(5),
                            Watchdogged = StateAspect.Susp(6),
                            SystemAlerted = StateAspect.Susp(7),
                            SysopAlerted = StateAspect.Susp(8),
                            ActiveTrace = StateAspect.Susp(9),
                            SystemLockdown = StateAspect.Susp(10);
    };
    public static class Alert
    {
        public static StateAspect None = StateAspect.Alert(0),
                            InvisibleAlarm = StateAspect.Alert(1),
                            // Invisible alarm is when it's not visible to the decker.  Silent alarm is perceptible to the decker but not to the party.
                            SilentAlarm = StateAspect.Alert(2),
                            AudibleAlarm = StateAspect.Alert(3),
                            CountermeasuresActivated = StateAspect.Alert(4),
                            BackupSummoned = StateAspect.Alert(5),
                            FacilityLockdown = StateAspect.Alert(6);
    }
}