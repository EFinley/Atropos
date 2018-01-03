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

    class ActionOpportunity
    {
        public IOpportunityCriterion Criterion = new OpportunityCriteria.AlwaysAvailable();
        public TimeSpan MaxOpportunityWindow = TimeSpan.FromSeconds(20);
        public bool AlreadyPresented = false;

        public ActionOpportunity AlwaysAvailable() { Criterion = new OpportunityCriteria.AlwaysAvailable(); return this; }
        public ActionOpportunity IfStateIs(StateAspect benchmark, StateChangeCriterion criterion)
                               { Criterion = new OpportunityCriteria.IfStateIs(benchmark, criterion); return this; }
        public ActionOpportunity IfObjectiveGained(string objectiveName)
                               { Criterion = new OpportunityCriteria.IfObjectiveGained(objectiveName); return this; }
        public ActionOpportunity IfObjectiveNotGained(string objectiveName)
                               { Criterion = new OpportunityCriteria.IfObjectiveGained(objectiveName, true); return this; }
        public ActionOpportunity IfConditionIsTrue(string conditionName)
                               { Criterion = new OpportunityCriteria.IfObjectiveGained(conditionName); return this; }
        public ActionOpportunity IfConditionIsFalse(string conditionName)
                               { Criterion = new OpportunityCriteria.IfObjectiveGained(conditionName, true); return this; }

        public ActionOpportunity(string introText = null)
        {
            IntroText = introText ?? string.Empty;
        }

        public string IntroText { get; set; }
        
        public virtual async Task<IStateChange> Attempt(HackingAttemptState initialState)
        {
            await Speech.SayAllOf(IntroText, useSpeakerMode: false);

            // Generate possible Outcomes (lists of StateChanges) here, based on the specifics of what the player is doing.

            // Also, remember to set reTryPossible on the returned Outcome, if you want it to indeed be an option.

            // If relevant the special stateChanges for dumping the hacker or switching to another system should probably be the
            // last state changes in the outcome stack.

            return StateChange.NoChange;
        }
    }

    class SpeakAndPause : ActionOpportunity
    {
        private TimeSpan _pause;
        public SpeakAndPause(string textToSpeak, TimeSpan pauseLength) : base(textToSpeak) { _pause = pauseLength; }

        public override async Task<IStateChange> Attempt(HackingAttemptState initialState)
        {
            await Speech.SayAllOf(IntroText);
            await Task.Delay(_pause);
            return StateChange.NoChange;
        }
    }

    class PlaySFX : ActionOpportunity
    {
        private IEffect _soundEffect;
        private TimeSpan Duration;
        private bool persist;

        public PlaySFX(IEffect soundEffect, TimeSpan durationLimit, bool persistForFullDuration = false)
        {
            _soundEffect = soundEffect;
            Duration = durationLimit;
            persist = persistForFullDuration;
        }

        public override async Task<IStateChange> Attempt(HackingAttemptState initialState)
        {
            if (persist)
            {
                await Task.WhenAll(_soundEffect.PlayToCompletion(), Task.Delay(Duration));
            }
            else
            {
                await Task.WhenAny(_soundEffect.PlayToCompletion(), Task.Delay(Duration));
            }
            return StateChange.NoChange;
        }
    }

    class PlayerChoiceOpportunity : ActionOpportunity
    {
        private Machine_Learning.Classifier Classifier;
        private Dictionary<string, IStateChange> OutcomeOptions = new Dictionary<string, IStateChange>();
        public PlayerChoiceOpportunity SetOutcome(string GestureClassName, IStateChange Outcome)
        {
            if (!Classifier.Dataset.ClassNames.Contains(GestureClassName)) throw new Exception($"Cannot find gesture class {GestureClassName} in classifier's list (which is {Classifier.Dataset.ClassNames.Join()})");
            OutcomeOptions.Add(GestureClassName, Outcome);
            return this;
        }
        //private Atropos.Machine_Learning.PlanarizedGestureProvider Provider;

        public PlayerChoiceOpportunity(string introText = null, Classifier classifier = null, params IStateChange[] outcomes) : base(introText)
        {
            Classifier = classifier ?? LeftRightSwipeClassifier;
            for (int i = 0; i < outcomes.Length && i < Classifier.Dataset.Classes.Count; i++)
            {
                OutcomeOptions.Add(Classifier.Dataset.ClassNames[i], outcomes[i]);
            }
        }

        public override async Task<IStateChange> Attempt(HackingAttemptState initialState)
        {
            var MostRecentSample = new Sequence<Vector2>();

            //// Watch for the "key" gestures here; if detected, launch the associated ICEbreaker and await its results.
            //var featureVectors = (new MachineLearningActivity.MachineLearningStage("", null, null)).StopAndReturnResults(); // Change!!!
            //MostRecentSample.SourcePath = featureVectors;

            int selectionIndex = -1;
            if (Classifier.MachineOnline)
            {
                selectionIndex = await Classifier.Recognize(MostRecentSample);
            }
            string selectedOutcomeName = Classifier.Dataset.ActualGestureClasses.ElementAtOrDefault(selectionIndex).className;

            if (selectionIndex == -1 || !OutcomeOptions.ContainsKey(selectedOutcomeName)) return HackOutcome.NoChange;

            else return OutcomeOptions[selectedOutcomeName];
        }

        public static Classifier LeftRightSwipeClassifier { get; } // TODO - actually create this!
    }



    class ICEncounter : ActionOpportunity
    {
        public Dictionary<int, ICEbreaker> SuitableBreakers;
        public ICEbreaker ChosenBreaker;

        private Classifier Classifier;

        public override async Task<IStateChange> Attempt(HackingAttemptState initialState)
        {
            var MostRecentSample = new Sequence<Vector2>();

            //// Watch for the "key" gestures here; if detected, launch the associated ICEbreaker and await its results.
            //var featureVectors = (new MachineLearningActivity.MachineLearningStage("", null, null)).StopAndReturnResults(); // Change!!!
            //MostRecentSample.SourcePath = featureVectors;

            int selectionIndex = -1;
            if (Classifier.MachineOnline)
            {
                selectionIndex = await Classifier.Recognize(MostRecentSample);
            }

            if (selectionIndex == -1) return StateChange.NoChange;

            ChosenBreaker = SuitableBreakers[selectionIndex];

            return await ChosenBreaker.Break();
        }
    }
}