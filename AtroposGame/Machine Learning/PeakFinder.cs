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
using System.Numerics;
using System.Threading.Tasks;
using Nito.AsyncEx;
using MiscUtil;
using Atropos.DataStructures;
using System.Threading;

namespace Atropos.Machine_Learning
{
    public class PeakFinder<T> : ActivatorBase where T : struct
    {
        protected Func<List<T>, double> ScoringFunction;
        protected virtual IList<T> Data { get; set; }
        public PeakFinder(IList<T> data, Func<List<T>, double> scoringFunction, double thresholdScore, int minLength = 5, double dropFromPeakCriterion = 0.95, int numConsecutiveDecreases = 3, CancellationToken? stopToken = null)
        {
            Data = data;
            ScoringFunction = scoringFunction;
            ThresholdScore = thresholdScore;
            MinimumNumPointsToBegin = minLength;
            DropToPercentageBeforeLabelingMaximum = dropFromPeakCriterion;
            MinimumConsecutiveDecreases = numConsecutiveDecreases;

            DependsOn(stopToken ?? CancellationToken.None);
        }

        public enum Phase
        {
            NotStarted,
            TooSoon,
            Eligible,
            PastThreshold,
            PastPeak,
            Finished
        }
        public Phase CurrentPhase = Phase.NotStarted;

        protected double BestScoreThusFar = double.NegativeInfinity,
                         WorstScoreThusFar = double.PositiveInfinity;
        protected int IndexBeingProcessed = -1,
                        NumberOfConsecutiveDecreases = 0,
                        IndexOfBestScore = -1;

        protected int MinimumNumPointsToBegin,
                      MinimumConsecutiveDecreases;
        protected double ThresholdScore;
        protected double DropToPercentageBeforeLabelingMaximum;

        protected AsyncManualResetEvent FoundIt = new AsyncManualResetEvent();
        public virtual async Task<int> FindPeakIndex()
        {
            await FoundIt.WaitAsync();
            return IndexOfBestScore;
        }
        public virtual async Task<List<T>> FindBestSequence()
        {
            return GetListToConsider(await FindPeakIndex());
        }

        public virtual int SeekPeakIndex()
        {
            while (IndexBeingProcessed < Data.Count && CurrentPhase != Phase.Finished)
                ConsiderNext();
            return IndexOfBestScore;
        }
        public virtual List<T> SeekBestSequence()
        {
            return GetListToConsider(SeekPeakIndex());
        }

        // A trivial function here, not so trivial in the derived class...
        protected virtual List<T> GetListToConsider(int index)
        {
            return Data.Take(index + 1).ToList();
        }

        public void ConsiderNext()
        {
            // This is the meat of the matter!  Here we make sure the points provided are checked against the desired pattern,
            // and signal out once it's achieved.
            IndexBeingProcessed++;
            if (IndexBeingProcessed >= Data.Count) throw new ArgumentOutOfRangeException("IndexBeingProcessed", $"Cannot process the next point (#{IndexBeingProcessed}) of a sequence of length {Data.Count}!");

            // This section is basically the phase logic, step by step.  Minimum one step at each phase (just 'cause).

            // If we haven't begun, start us off!
            if (CurrentPhase == Phase.NotStarted)
            {
                CurrentPhase = Phase.TooSoon;
                return;
            }

            // Wait for a minimum number of points accumulated before you even START wasting time on calculating the score.
            if (CurrentPhase == Phase.TooSoon)
            {
                if (IndexBeingProcessed >= MinimumNumPointsToBegin) CurrentPhase = Phase.Eligible;
                return;
            }

            // Okay, time to actually calculate it.
            var Score = ScoringFunction(GetListToConsider(IndexBeingProcessed));
            // If you want to indicate "this is definitely not it" for some reason, use double.NaN to indicate that.
            if (double.IsNaN(Score)) return;

            // Eligible means we're allowed to check if we pass the threshold score yet.
            if (CurrentPhase == Phase.Eligible)
            {
                if (Score >= ThresholdScore)
                {
                    CurrentPhase = Phase.PastThreshold;
                    BestScoreThusFar = Score;
                    IndexOfBestScore = IndexBeingProcessed;
                }
                return;
            }

            // Once we pass the threshold, we're looking for a peak, so increase the best score until you drop (noticeably) below the prior best.
            if (CurrentPhase == Phase.PastThreshold)
            {
                if (Score > BestScoreThusFar)
                {
                    BestScoreThusFar = Score;
                    IndexOfBestScore = IndexBeingProcessed;
                }
                else if (Score < DropToPercentageBeforeLabelingMaximum * BestScoreThusFar)
                {
                    CurrentPhase = Phase.PastPeak;
                    WorstScoreThusFar = Score;
                    return;
                }
                // If it's in between these two, then don't take immediate action, but don't overwrite the peak info either - in other words, do nothing!
                
                return;
            }

            // If we think we found a peak, look for N consecutive decreases and then declare victory.
            if (CurrentPhase == Phase.PastPeak)
            {
                if (Score < WorstScoreThusFar)
                {
                    NumberOfConsecutiveDecreases++;
                    WorstScoreThusFar = Score;
                    if (NumberOfConsecutiveDecreases > MinimumConsecutiveDecreases)
                    {
                        CurrentPhase = Phase.Finished; // Nothing triggers off this, but it might need to be looked at from outside to see the state of things in here.
                        FoundIt.Set(); // Everything else will take care of itself in FindPeak().
                    }
                }
                else if (Score > BestScoreThusFar) // Oops! Still goin' up!
                {
                    NumberOfConsecutiveDecreases = 0;
                    BestScoreThusFar = Score;
                    CurrentPhase = Phase.PastThreshold;
                    IndexOfBestScore = IndexBeingProcessed;
                    return;
                }
                else // Not an increase beyond the max, but also not a consecutive decrease; no worries.
                {
                    NumberOfConsecutiveDecreases = 0;
                    WorstScoreThusFar = Score; // Okay, not the *absolute* worst score thus far, but whatevs.
                }
            }
        }

        public void ConsiderAllAvailable()
        {
            while (IndexBeingProcessed < Data.Count) ConsiderNext();
        }

        /// <summary>
        /// Looks for a peak, but looks at the LAST n points and increases n each time it's called.  Caution! Using this 'live" is possible but inadvisable, as when you
        /// increase the length of the dataset, you run a significant chance of confusing yourself (and this class).  Recommend using this only on "dead" datasets - it doesn't
        /// make a lot of sense otherwise, anyway, does it?
        /// </summary>
        public class BackwardsCounting<Tf> : PeakFinder<Tf> where Tf : struct
        {
            public BackwardsCounting(IList<Tf> data, Func<List<Tf>, double> scoringFunction, double thresholdScore, int minLength = 5, double dropFromPeakCriterion = 0.95, int numConsecutiveDecreases = 3, CancellationToken? stopToken = null)
                : base(data, scoringFunction, thresholdScore, minLength, dropFromPeakCriterion, numConsecutiveDecreases, stopToken)
            {

            }

            public override async Task<int> FindPeakIndex()
            {
                var indexBackward = await base.FindPeakIndex();
                return Data.Count - indexBackward - 1;
            }
            public override async Task<List<Tf>> FindBestSequence()
            {
                var indexBackward = await base.FindPeakIndex();
                return GetListToConsider(indexBackward);
            }

            protected override List<Tf> GetListToConsider(int indexBackward)
            {
                return Data.Skip(Data.Count - indexBackward - 1).ToList();
            }
        }
    }
}