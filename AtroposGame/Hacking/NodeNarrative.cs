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
    /// <summary>
    /// High-level class which defines a hacking run.  Will be accessed by GMs and GM tools to construct the runs; is serviced by several
    /// underlying structures.
    /// </summary>
    /// <example>
    ///     A typical run might be defined like this:
    ///     
    ///     using com.Atropos.Hacking;
    ///     using static Atropos.Hacking.StandardCriteria;
    ///     
    ///     var myNewMatrix = new NodeNarrative(
    ///         new HackingAttemptState()
    ///             .WithConditions("InvokedHelpSystem"),
    ///         new PlayerChoiceOpportunity(
    ///             "Welcome to Ono-Sendai Cybernetics distributorship portal.  Swipe left if you have an authorized account with us, or right to speak to an expert system.",
    ///             new StateChange(Suspicion.ForensicTraces),
    ///             StateChange.SetCondition("InvokedHelpSystem", true)),
    ///         new ICEncounter("Fuchi DataWall version six point four", ICEbreakers.FalseFlag, ICEbreakers.Banzai),
    ///         new PlaySFX( etc. etc. ).IfStateIs(Alarm.SilentAlarm, orHigher),
    ///         
    ///             
    /// </example>
    class NodeNarrative
    {
        public HackingAttemptState AttemptState;
        public List<ActionOpportunity> Opportunities = new List<ActionOpportunity>();
        public ActionOpportunity NextOpportunity
        {
            get
            {
                return Opportunities
                        .Where(o => o.Criterion.IsAvailable(AttemptState) && !o.AlreadyPresented)
                        //.OrderByDescending(o => o.Criterion.HasRelevance(AttemptState))
                        .FirstOrDefault();
            }
        }

        public NodeNarrative(HackingAttemptState initialState = null, params ActionOpportunity[] opportunities)
        {
            AttemptState = initialState ?? new HackingAttemptState();
            Opportunities.AddRange(opportunities);
        }

        private static TaskCompletionSource _endNarrativeSignal = new TaskCompletionSource();
        public static void EndCurrentNarrative() { _endNarrativeSignal.SetResult(); }
        private static NodeNarrative _nextNarrative;
        public static void SwitchToNarrative(NodeNarrative nextNarrative)
        {
            _nextNarrative = nextNarrative;
            _endNarrativeSignal.SetResult();
        }

        public async Task RunIt(HackingAttemptState initialState = null)
        {
            AttemptState = initialState ?? AttemptState;
            var CurrentOpportunity = NextOpportunity;

            while (CurrentOpportunity != default(ActionOpportunity))
            {
                var outcome = CurrentOpportunity.Attempt(AttemptState);

                if (await outcome.Before(CurrentOpportunity.MaxOpportunityWindow, _endNarrativeSignal.Task))
                {
                    AttemptState = outcome.Result.AppliedTo(AttemptState);

                    if (!outcome.Result.reTryPermitted)
                    {
                        CurrentOpportunity.AlreadyPresented = true;
                        CurrentOpportunity = NextOpportunity;
                    }
                }
                else
                {
                    if (_endNarrativeSignal.Task.Status == TaskStatus.RanToCompletion)
                            { CurrentOpportunity = default(ActionOpportunity); break; }

                    CurrentOpportunity.AlreadyPresented = true;
                    CurrentOpportunity = NextOpportunity;
                }
            }

            if (_endNarrativeSignal.Task.Status == TaskStatus.RanToCompletion && _nextNarrative != null)
            {
                var nextNar = _nextNarrative;
                _nextNarrative = null;
                _endNarrativeSignal = new TaskCompletionSource();
                
                await nextNar.RunIt(AttemptState);
                return;
            }
            
        }
    }
}