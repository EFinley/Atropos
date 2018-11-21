
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Numerics;
using Android.Util;
using MiscUtil;
using Nito.AsyncEx;
using Atropos.Characters;
//using static Atropos.Hacking.HackingCondition;
using System.Reflection;

namespace Atropos.Hacking
{
    public class Hacker
    {
        public const string EFFICACY = "HackingEfficacy";
        public static Hacker Me = new Hacker();

        public double BaseEfficacy = 1.0;
        public Dictionary<string, double> EfficacyMultipliers = new Dictionary<string, double>();
        public double HackingEfficacy
        {
            get
            {
                var eff = BaseEfficacy;
                foreach (var amt in EfficacyMultipliers.Values) eff *= amt;
                return eff;
            }
        }

        public double BaseEaseOfTasks = 1.0;
        public Dictionary<string, double> EaseMultipliers = new Dictionary<string, double>();
        public double EaseOfTasks
        {
            get
            {
                var ease = BaseEaseOfTasks;
                foreach (var amt in EaseMultipliers.Values) ease *= amt;
                return ease;
            }
        }

        // The primary factors in EaseOfCasting, aside from static factors affecting the base ease, are Daze effects.
        public string AddDaze(double amount) // Use this version when multiple instances from the same source (e.g. Paralysis) are allowed to stack / overlap
        {
            var key = Guid.NewGuid().ToString();
            AddDaze(key, amount);
            return key;
        }
        public void AddDaze(string key, double amount) // Use this version when you know the name and want to override rather than stack
        {
            if (key != "Clarity" && EaseMultipliers.ContainsKey("Clarity")) return; // Blocked by Clarity effect.
            EaseMultipliers[key] = 1.0 / amount;
        }
        public void RemoveDaze(string key)
        {
            if (EaseMultipliers.ContainsKey(key)) EaseMultipliers.Remove(key);
        }
    }

    //public class HackingActionsList
    //{
    //    public HackingActionGroup FalseTrail, CoverTracks, SecurityInfo, SecurityScan, AssessPaydata, SideChannel, AttemptLogon, HackFirewall, HackProxy, Cancel;
    //    public HackingIcebreaker SleazeFirewall, BanzaiFirewall;
    //    public HackingItemSimple BounceSignal, DeleteLogs, CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic,
    //                  DetectSysad, DetectTrace, DetectAlerts, LocateSysad, 
    //                  CrackPassword,
    //                  FalsifyCertificate, BanzaiAttack, BanzaiPause, 
    //                  BypassProxy,
    //                  BeginObjective, ContinueObjective, AchieveObjective;

    //    public List<HackingAction> AllActionsList;
    //    public Dictionary<string, HackingAction> ActionsList = new Dictionary<string, HackingAction>();
    //    public virtual void RegisterAllActions()
    //    {
    //        AllActionsList = new List<HackingAction>()
    //        {
    //            FalseTrail, CoverTracks, SecurityInfo, SecurityScan, AssessPaydata, SideChannel, AttemptLogon, HackFirewall, HackProxy,
    //            BounceSignal, DeleteLogs, CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic,
    //            DetectSysad, DetectTrace, DetectAlerts, LocateSysad,
    //            CrackPassword,
    //            SleazeFirewall, FalsifyCertificate, BanzaiFirewall, BanzaiAttack, BanzaiPause,
    //            BypassProxy,
    //            BeginObjective, ContinueObjective, AchieveObjective, Cancel
    //        };

    //        foreach (var action in AllActionsList)
    //        {
    //            if (action == null) continue;
    //            foreach (var countermeasure in HackingCondition.AllCountermeasures)
    //            {
    //                if (action.Conditions.Contains(countermeasure.Placeholder))
    //                {
    //                    action.Conditions.Remove(countermeasure.Placeholder);
    //                    action.Conditions.Add(countermeasure);
    //                }
    //            } 
    //        }
    //    }

    //    public void SetUpBasicNetwork()
    //    {

    //        AttemptLogon = new HackingActionGroup("AttemptLogon", HackGesture.RightThenDown, "Attempt Logon", HackingNavigation.Root, Not(ShellAccess))
    //        {
    //            //IsRootLevel = true,
    //            OnSelect = (h) => Speech.Say($"{((Res.CoinFlip) ? "Here goes nothing." : "Time to break the locks.")}")
    //        };
    //        CrackPassword = new HackingItemSimple("CrackPassword", HackGesture.DownThenLeft, "Crack Password", AttemptLogon, Firewall, Proxy)
    //        {
    //            OnSelectText = "Loading wizard series four icebreaker",
    //            OnExecute = async (h) =>
    //            {
    //                await Task.Delay(1000);
    //                new Effect("PWCrack", Resource.Raw._349905_slowGlassShatter).Play();
    //                await Task.Delay(500);
    //                new Effect("PWCrack2", Resource.Raw._349905_slowGlassShatter).Play();
    //                await Task.Delay(750);
    //                Speech.Say("Cracked it.  Shell access granted.  New menu options available.");
    //                h.NumSuccesses++;
    //                ShellAccess.IsMet = true;
    //                HackingActivity.Current.UpdateListView();
    //            }
    //        };

    //        FalseTrail = new HackingActionGroup("FalseTrail", HackGesture.LeftThenDown, "Lay False Trail", HackingNavigation.Root, Not(ShellAccess))
    //        {
    //            //IsRootLevel = true,
    //            OnSelectText = "Redirecting input signal."
    //        };
    //        BounceSignal = new HackingItemSimple("BounceSignal", HackGesture.Typing, "Bounce Signal", FalseTrail)
    //        {
    //            OnExecute = async (h) =>
    //            {
    //                h.NumSuccesses++;
    //                var citynames = new string[] { "London", "New York", "Sydney", "Moscow", "Paris", "Bogota", "Rekyavick", "Berlin", "Nairobi", "Singapore" };
    //                await Speech.SayAllOf($"Routing through {citynames.GetRandom()}", SoundOptions.AtSpeed(1.5));
    //            },
    //            DisplayTgtSuccessess = (h) => "\u221E", // Infinity symbol
    //            TgtSuccesses = int.MaxValue
    //        };

    //        CoverTracks = new HackingActionGroup("CoverTracks", HackGesture.LeftThenDown, "Cover Your Tracks", HackingNavigation.Root, ShellAccess)
    //        {
    //            //IsRootLevel = true,
    //            OnSelectText = "Opening access logs."
    //        };
    //        DeleteLogs = new HackingItemSimple("DeleteLogs", HackGesture.Typing, "Delete Own Logs", CoverTracks)
    //        {
    //            DisplayTgtSuccessess = (h) => $"{HackingNavigation.NumTotalActions / 2}",
    //            TgtSuccesses = int.MaxValue, // Initial value
    //            OnExecute = (h) => { h.NumSuccesses++; h.TgtSuccesses = (--HackingNavigation.NumTotalActions)/2; } // Decrementing because log deletions shouldn't count toward this!
    //        };

    //        SecurityInfo = new HackingActionGroup("SecurityInfo", HackGesture.LeftThenUp, "Security Intel", HackingNavigation.Root, Not(ShellAccess))
    //        {
    //            //IsRootLevel = true,
    //            OnSelectText = "Analyzing security infrastructure."
    //        };
    //        CheckFirewalls = new HackingItemSimple("CheckFirewalls", HackGesture.DownThenRight, "Check for Firewalls", SecurityInfo)
    //        {
    //            //OnSelect = (h) =>
    //            //{
    //            //    var hItem = h as HackingItem;
    //            //    //foreach (HackingItem item in new HackingItem[] { CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic }) item.Gesture = null; // Resets them to normal
    //            //    hItem.Gesture = HackGesture.Typing;
    //            //},
    //            OnSelectText = "Loading mole version 2.6",
    //            OnExecute = (h) =>
    //            {
    //                h.NumSuccesses++;
    //                if (HackFirewall != null)
    //                {
    //                    HackFirewall.IsKnown = true;
    //                    Speech.Say("Firewall detected.");
    //                }
    //                else Speech.Say((Res.CoinFlip) ? "No firewall found." : "Trivial firewall found and circumvented.");
    //            },
    //            //OnCancel = (h) =>
    //            //{
    //            //    h.Gesture = null;
    //            //}
    //        };
    //        CheckAuthIC = new HackingItemSimple("CheckAuthIC", HackGesture.UpThenRight, "Check Authenticators", SecurityInfo)
    //        {
    //            //OnSelect = (h) =>
    //            //{
    //            //    var hItem = h as HackingItem;
    //            //    //foreach (HackingItem item in new HackingItem[] { CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic }) item.Gesture = null; // Resets them to normal
    //            //    //var coItems = new HackingItem[] { CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic };
    //            //    //if (HackingNavigation.CurrentAction.IsOneOf(coItems)) HackingNavigation.CurrentAction.Cancel();
    //            //    hItem.Gesture = HackGesture.Typing;
    //            //},
    //            OnSelectText = "Loading Sniffer two",
    //            OnExecute = (h) =>
    //            {
    //                h.NumSuccesses++;
    //                Speech.Say("Simple password prompt. Easy peasy.");
    //            },
    //            DisplayTgtSuccessess = HackingItemSimple.UnknownTgtSuccUntilMet
    //        };
    //        CheckProxy = new HackingItemSimple("CheckProxy", HackGesture.DownThenLeft, "Probe Proxy Server", SecurityInfo)
    //        {
    //            //OnSelect = (h) =>
    //            //{
    //            //    var hItem = h as HackingItem;
    //            //    //foreach (HackingItem item in new HackingItem[] { CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic }) item.Gesture = null; // Resets them to normal
    //            //    hItem.Gesture = HackGesture.Typing;
    //            //},
    //            OnSelectText = "Loading packet analyzer",
    //            OnExecute = (h) =>
    //            {
    //                h.NumSuccesses++;
    //                if (HackProxy != null)
    //                {
    //                    HackProxy.IsKnown = true;
    //                    Speech.Say("Proxy server detected.");
    //                }
    //                else Speech.Say("No relevant proxy server found.");
    //            }
    //        };
    //        CheckTraffic = new HackingItemSimple("CheckTraffic", HackGesture.UpThenLeft, "Observe Traffic", SecurityInfo)
    //        {
    //            //OnSelect = (h) =>
    //            //{
    //            //    var hItem = h as HackingItem;
    //            //    //foreach (HackingItem item in new HackingItem[] { CheckFirewalls, CheckAuthIC, CheckProxy, CheckTraffic }) item.Gesture = null; // Resets them to normal
    //            //    hItem.Gesture = HackGesture.Typing;
    //            //},
    //            OnSelectText = "Sampling nearby wireless traffic",
    //            OnExecute = (h) =>
    //            {
    //                h.NumSuccesses++;
    //                if (h.NumSuccesses % 3 == 1) Speech.Say("You observe nothing special.");
    //            },
    //            TgtSuccesses = int.MaxValue,
    //            DisplayTgtSuccessess = h => (h.NumSuccesses == 0) ? "?" : $"{h.NumSuccesses}?"
    //        };

    //        SecurityScan = new HackingActionGroup("SecurityScan", HackGesture.LeftThenUp, "Security Scan", HackingNavigation.Root, ShellAccess)
    //        {
    //            //IsRootLevel = true,
    //            OnSelectText = "Inspecting system processes"
    //        };
    //        DetectSysad = new HackingItemSimple("DetectSysad", HackGesture.DownThenRight, "Check for Sysadmin", SecurityScan)
    //        {
    //            //OnSelect = (h) => { h.Gesture = HackGesture.Typing; },
    //            OnExecute = (h) =>
    //            {
    //                if (SysopOnline.IsMet)
    //                {
    //                    h.NumSuccesses++;
    //                    Speech.Say("Tread carefully - sysop is logged on.");
    //                }
    //                else Speech.Say("Coast looks clear. For now.");
    //            },
    //            DisplayTgtSuccessess = HackingItemSimple.UnknownTgtSuccUntilMet
    //        };
    //        DetectTrace = new HackingItemSimple("DetectTrace", HackGesture.UpThenRight, "Check for Trace", SecurityScan)
    //        {
    //            //OnSelect = (h) => { h.Gesture = HackGesture.Typing; },
    //            OnSelectText = "Loading Limburger version one point oh point seven.",
    //            OnExecute = (h) =>
    //            {
    //                if (ActiveTraceTriggered.IsMet)
    //                {
    //                    h.NumSuccesses++;
    //                    var Trace = (TriggeredCondition)ActiveTraceTriggered;
    //                    var timeAvail = Trace.FiredAt + Trace.DelayBeforeConsequence - DateTime.Now;
    //                    Speech.Say($"Uh-oh.  An active trace is already running.  You have {timeAvail.ToSpeakableForm()} to log off...");
    //                }
    //                else Speech.Say("Coast looks clear. For now.");
    //            },
    //            DisplayTgtSuccessess = HackingItemSimple.UnknownTgtSuccUntilMet
    //        };
    //        DetectAlerts = new HackingItemSimple("DetectAlerts", HackGesture.DownThenLeft, "Check for Alerts", SecurityScan)
    //        {
    //            //OnSelect = (h) => { h.Gesture = HackGesture.Typing; },
    //            OnSelectText = "Filtering for security daemons",
    //            OnExecute = (h) =>
    //            {
    //                if (AudibleAlarmSounded.IsMet)
    //                {
    //                    h.NumSuccesses++;
    //                    Speech.Say("Audible alarm triggered. Wow, you have some serious focus going, there.");
    //                }
    //                else if (SilentAlarmSounded.IsMet)
    //                {
    //                    h.NumSuccesses++;
    //                    Speech.Say("Some kind of background security process has fired - looks like a silent alarm going off.");
    //                }
    //                else Speech.Say("No sign of any alarms at present.");
    //            },
    //            DisplayTgtSuccessess = HackingItemSimple.UnknownTgtSuccUntilMet
    //        };

    //        //SideChannel = new HackingActionGroup("SideChannel", HackGesture.RightThenUp, "Side-channel Attacks", Not(ShellAccess))
    //        //{
    //        //    IsRootLevel = true,
    //        //    OnSelectText = "Preparing to think outside the box."
    //        //};
    //        //LocateSysad = new HackingItem("LocateSysad", HackGesture.Typing, "Locate Admin Cell#", Category("SideChannel"))
    //        //{
    //        //    OnExecute = (h) =>
    //        //    {
    //        //        h.NumSuccesses++;
    //        //        Speech.Say("You've located the number.  If you need a voiceprint later, this will come in handy.");
    //        //    }
    //        //};

    //        //AssessPaydata = new HackingActionGroup("AssessPaydata", HackGesture.RightThenUp, "Assess Paydata", ShellAccess)
    //        //{
    //        //    IsRootLevel = true,
    //        //    OnSelectText = "Running Baksheesh version 1.6 expert system."
    //        //};

    //        Cancel = new HackingActionGroup("Cancel", HackGesture.RightThenUp, "Cancel", HackingNavigation.Root)
    //        { };

    //        //RegisterAllActions();
    //        //return this;
    //    }

    //    public void SetUpFirewall()
    //    {
    //        HackFirewall = new HackingObstacle("HackFirewall", HackGesture.DownThenRight, "Hack Firewall", "AttemptLogon");
    //        Firewall = new CountermeasureCondition(Firewall, "HackFirewall");
    //        SleazeFirewall = new HackingIcebreaker.AccumulateTyping("SleazeFirewall", HackGesture.UpThenRight, "Sleaze v.2.11",
    //            "Falsify certificate", 3, 4, 5, HackFirewall);
    //        BanzaiFirewall = new HackingIcebreaker.DoOrDoNot("BanzaiFirewall", HackGesture.DownThenLeft, "Banzai PRO",
    //            "Wap! Bam! Zip!", "Pause...", new string[] { "Kick!", "Strike!", "Punch!", "Dodge!", "Block!", "Jump!" }, new string[] { "Breathe!", "Rest!", "Hold!" }, HackFirewall);
            
    //    }
    //}
}