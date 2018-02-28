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
        public PeakFinder(IList<T> data, Func<List<T>, double> scoringFunction, double thresholdScore, int minLength = 7, double dropFromPeakCriterion = 0.95, int numConsecutiveDecreases = 3, CancellationToken? stopToken = null)
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
        public virtual async Task<int> FindPeakIndexAsync()
        {
            await FoundIt.WaitAsync();
            return IndexOfBestScore;
        }
        public virtual async Task<List<T>> FindBestSequenceAsync()
        {
            return GetListToConsider(await FindPeakIndexAsync());
        }

        public virtual int FindPeakIndex()
        {
            while (IndexBeingProcessed < Data.Count && CurrentPhase != Phase.Finished)
                ConsiderNext();
            return IndexOfBestScore;
        }
        public virtual List<T> FindBestSequence()
        {
            return GetListToConsider(FindPeakIndex());
        }

        // A trivial function here, not so trivial in the derived class...
        protected virtual List<T> GetListToConsider(int index)
        {
            return Data.Take(index + 1).ToList();
        }

        public virtual void ConsiderNext()
        {
            var score = Consider();
            Android.Util.Log.Debug("PeakFinder", $"Considered point {IndexBeingProcessed}, score is {score}, current phase is {CurrentPhase}.");

            if (CurrentPhase == Phase.Finished) FoundIt.Set(); // Everything else will take care of itself in FindPeak().
            IndexBeingProcessed++;
        }
        public double Consider()
        {
            // This is the meat of the matter!  Here we make sure the points provided are checked against the desired pattern,
            // and signal out once it's achieved.
            if (IndexBeingProcessed >= Data.Count) throw new ArgumentOutOfRangeException("IndexBeingProcessed", $"Cannot process the next point (#{IndexBeingProcessed}) of a sequence of length {Data.Count}!");

            // This section is basically the phase logic, step by step.  Minimum one step at each phase (just 'cause).

            // If we haven't begun, start us off!
            if (CurrentPhase == Phase.NotStarted)
            {
                CurrentPhase = Phase.TooSoon;
                return double.NaN;
            }

            // Wait for a minimum number of points accumulated before you even START wasting time on calculating the score.
            if (CurrentPhase == Phase.TooSoon)
            {
                if (IndexBeingProcessed >= MinimumNumPointsToBegin) CurrentPhase = Phase.Eligible;
                return double.NaN;
            }

            // Okay, time to actually calculate it.
            var Score = ScoringFunction(GetListToConsider(IndexBeingProcessed));
            // If you want to indicate "this is definitely not it" for some reason, use double.NaN to indicate that.
            if (double.IsNaN(Score))
            {
                if (CurrentPhase != Phase.PastPeak) return Score;
                else Score = 0.0; // If we're past the peak, then "definitely not it" still counts as "worse than the last one" and therefore can't just skip the rest of this logic.
            }

            // Eligible means we're allowed to check if we pass the threshold score yet.
            if (CurrentPhase == Phase.Eligible)
            {
                if (Score >= ThresholdScore)
                {
                    CurrentPhase = Phase.PastThreshold;
                    BestScoreThusFar = Score;
                    IndexOfBestScore = IndexBeingProcessed;
                }
                return Score;
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
                    return Score;
                }
                // If it's in between these two, then don't take immediate action, but don't overwrite the peak info either - in other words, do nothing!

                return Score;
            }

            // If we think we found a peak, look for N consecutive decreases and then declare victory.
            if (CurrentPhase == Phase.PastPeak)
            {
                if (Score <= WorstScoreThusFar)
                {
                    NumberOfConsecutiveDecreases++;
                    WorstScoreThusFar = Score;
                    if (NumberOfConsecutiveDecreases >= MinimumConsecutiveDecreases)
                    {
                        CurrentPhase = Phase.Finished;
                        //FoundIt.Set(); // Everything else will take care of itself in FindPeak().
                    }
                }
                else if (Score > BestScoreThusFar) // Oops! Still goin' up!
                {
                    NumberOfConsecutiveDecreases = 0;
                    BestScoreThusFar = Score;
                    CurrentPhase = Phase.PastThreshold;
                    IndexOfBestScore = IndexBeingProcessed;
                }
                else // Not an increase beyond the max, but also not a consecutive decrease; no worries.
                {
                    NumberOfConsecutiveDecreases = 0;
                    WorstScoreThusFar = Score; // Okay, not the *absolute* worst score thus far, but whatevs.
                }
            }

            return Score;
        }

        public void ConsiderAllAvailable()
        {
            while (IndexBeingProcessed < Data.Count)
            {
                ConsiderNext();
                if (CurrentPhase == Phase.Finished) break;
            }
        }

        /// <summary>
        /// Looks for a peak, but does so by incrementing the *first* index examined, instead of the last one.
        /// Use only on a "dead" dataset - one which isn't being added to anymore! - or all warranties are void. ;)
        /// </summary>
        public class IncrementingStartIndexVariant<T2> : PeakFinder<T2> where T2 : struct
        {
            public IncrementingStartIndexVariant(IList<T2> data, Func<List<T2>, double> scoringFunction, double thresholdScore, int minLength = 7, double dropFromPeakCriterion = 0.95, int numConsecutiveDecreases = 3, CancellationToken? stopToken = null)
                : base(data, scoringFunction, thresholdScore, minLength, dropFromPeakCriterion, numConsecutiveDecreases, stopToken)
            {
                // Nothing added, but constructor inheritance must be explicit.
            }
            
            protected override List<T2> GetListToConsider(int indexOfSkip)
            {
                return Data.Skip(indexOfSkip).ToList();
            }
        }
    }
}