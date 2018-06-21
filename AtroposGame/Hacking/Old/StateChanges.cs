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
using Atropos.Machine_Learning;

namespace Atropos.Hacking
{
    internal delegate bool StateChangeCriterion(StateAspect target, HackingAttemptState state);

    internal interface IStateChange
    {
        HackingAttemptState AppliedTo(HackingAttemptState PriorState);
        bool reTryPermitted { get; }
    };

    #region Easy-access criteria (for doing stuff like "StateChange(Authority.ReadWrite, orHigher)"
    // Recommended: "using Only = Atropos.Hacking.StandardCriteria" - will make it much easier to refer to.
    // Or simply "using static Atropos.Hacking.StandardCriteria".
    internal static class StandardCriteria
    {
        public static StateChangeCriterion ifHigher = (t, h) =>
                {
                    return t.severity > h[t].severity && ifPossible(t, h);
                };

        public static StateChangeCriterion ifHigherOrEqual = (t, h) =>
                {
                    return t.severity >= h[t].severity && ifPossible(t, h);
                };

        public static StateChangeCriterion ifEqual = (t, h) =>
                {
                    return t.severity == h[t].severity && ifPossible(t, h);
                };

        public static StateChangeCriterion ifLower = (t, h) =>
                {
                    return t.severity < h[t].severity && ifPossible(t, h);
                };

        public static StateChangeCriterion ifLowerOrEqual = (t, h) =>
                {
                    return t.severity <= h[t].severity && ifPossible(t, h);
                };

        public static StateChangeCriterion orHigher { get { return ifLower; } }
        public static StateChangeCriterion orLower { get { return ifHigher; } }
        public static StateChangeCriterion exactlyTo = (t, h) => true; // Note the lack of "ifPossible" here.

        public static StateChangeCriterion ifPossible
        {
            get
            {
                return (t, h) =>
                {
                    // Insert complicated logic here for what's allowed and what's not.
                    return true;
                };
            }
        }

        public static StateChangeCriterion never = (t, h) => false;

        //public static int MarginOfSuccess(State newState, HackingAttemptState oldState, StateChangeCriterion criterion)
        //{
        //    int newIsHigherBy = newState.severity - oldState.CorrespondingTo(newState).severity;

        //    if (criterion == ifHigher || criterion == ifHigherOrEqual || criterion == orLower)
        //        return newIsHigherBy;

        //    if (criterion == ifLower || criterion == ifLowerOrEqual || criterion == orHigher)
        //        return -newIsHigherBy;

        //    else return (2 - Math.Abs(newIsHigherBy));
        //}
    }
    #endregion

    internal class StateChange : IStateChange
    {
        public StateAspect ChangeTo;
        private StateChangeCriterion Constraint;
      
        public StateChange(StateAspect changeTo, StateChangeCriterion constraint = null)
        {
            ChangeTo = changeTo;
            Constraint = constraint ?? StandardCriteria.ifPossible;

        }

        public HackingAttemptState AppliedTo(HackingAttemptState PriorState)
        {
            var resultState = PriorState;

            if (Constraint(ChangeTo, PriorState)) resultState[ChangeTo] = ChangeTo;

            return resultState;
        }
        public bool reTryPermitted { get; set; } = false;

        
        public static IStateChange NoChange = new ArbitraryStateChange();
        private static IStateChange _setFlag(string flagname, bool setTo, bool itsAnObjective = true)
        {
            if (itsAnObjective)
                return new ArbitraryStateChange((state) => { state.Objectives[flagname] = setTo; return state; });
            else
                return new ArbitraryStateChange((state) => { state.Conditions[flagname] = setTo; return state; });
        } 

        public static IStateChange ReachObjective(string objectiveName)
        {
            return _setFlag(objectiveName, true, true);
        }
        public static IStateChange SetCondition(string conditionName, bool setTo)
        {
            return _setFlag(conditionName, setTo, false);
        }
    }

    internal class ArbitraryStateChange : IStateChange
    {
        protected readonly Func<HackingAttemptState, HackingAttemptState> _changeEffect;
        public ArbitraryStateChange(Func<HackingAttemptState, HackingAttemptState> changeEffect = null)
        {
            _changeEffect = changeEffect;
        }
        public HackingAttemptState AppliedTo(HackingAttemptState PriorState)
        {
            return _changeEffect?.Invoke(PriorState) ?? PriorState;
        }
        public bool reTryPermitted { get; } = false;
    }

    internal class HackOutcome : List<IStateChange>, IStateChange
    {
        public HackingAttemptState AppliedTo(HackingAttemptState PriorState)
        {
            var state = PriorState;
            foreach (IStateChange change in this)
                state = change.AppliedTo(state);
            return state;
        }
        public bool reTryPermitted { get; set; } = false;

        public static IStateChange NoChange = StateChange.NoChange;
    }

    internal class EndHackingAttempt : IStateChange
    {
        private string DumpSpeech;
        private Effect DumpSFX;
        private bool FXafterSpeech;

        public EndHackingAttempt(string dumpSpeech = null, Effect dumpFX = null, bool FXcomesAfterVoice = false)
        {
            DumpSpeech = dumpSpeech;
            DumpSFX = dumpFX;
            FXafterSpeech = FXcomesAfterVoice;
        }

        public bool reTryPermitted { get { return false; } }
        public HackingAttemptState AppliedTo(HackingAttemptState PriorState)
        {
            NodeNarrative.EndCurrentNarrative();

            if (FXafterSpeech)
            {
                Speech.SayAllOf(DumpSpeech, useSpeakerMode: false).Wait();
                DumpSFX?.Play(useSpeakers: true);
            }
            else
            {
                DumpSFX?.Play(useSpeakers: true);
                Speech.Say(DumpSpeech, useSpeakerMode: false);
            }

            return PriorState;
        }
    }

    internal class JumpToNewNarrativePath : IStateChange
    {
        private NodeNarrative _nextNarrative;
        public bool reTryPermitted { get { return false; } }

        public JumpToNewNarrativePath(NodeNarrative newNarrative)
        {
            _nextNarrative = newNarrative;
        }

        public HackingAttemptState AppliedTo(HackingAttemptState PriorState)
        {
            NodeNarrative.SwitchToNarrative(_nextNarrative);
            return PriorState;
        }
    }
}