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

namespace Atropos.Machine_Learning
{
    public partial class MachineLearningActivity<T> where T : struct
    {
        public class MachineLearningStage : GestureRecognizerStage
        {
            protected ILoggingProvider<T> VectorProvider;
            protected DataSet<T> DataSet;

            public MachineLearningStage(string label, DataSet<T> dataset, ILoggingProvider<T> Provider, bool AutoStart = false) : base(label)
            {
                DataSet = dataset;

                VectorProvider = Provider;
                SetUpProvider(VectorProvider);
                VectorProvider.Delay = Android.Hardware.SensorDelay.Fastest; // Turns out that the default, SensorDelay.Game, doesn't capture fast enough for our style of gestures.

                if (AutoStart) Activate();
            }

            private bool softCancel = false;

            public T[] StopAndReturnResults()
            {
                VectorProvider.Deactivate();

                var ResultArray = VectorProvider.LoggedData;
                ResultArray = ResultArray.Smooth(3);
                softCancel = true;
                return ResultArray.ToArray();
            }

            protected override bool abortCriterion()
            {
                return softCancel;
            }
        }

        public class SelfEndpointingSingleGestureRecognizer : MachineLearningStage
        {
            public GestureClass Target;

            protected Classifier Classifier;
            protected SmoothedList<T> SmoothedData;

            protected Phase CurrentPhase = Phase.NotStarted;

            protected double BestScoreThusFar = double.NegativeInfinity,
                             WorstScoreThusFar = double.PositiveInfinity;
            protected int NumberOfConsecutiveDecreases = 0,
                          NumberOfPointsLastScored = 0,
                          NumPointsAtPeak = 0;

            private int MinimumNumPointsToBegin = 5,
                        MinimumConsecutiveDecreases = 3;
            private double ThresholdScore = 1.75;
            private double DropToPercentageBeforeLabelingMaximum = 0.925;

            protected enum Phase
            {
                NotStarted,
                TooSoon,
                Eligible,
                PastThreshold,
                PastPeak,
                Finished
            }

            public SelfEndpointingSingleGestureRecognizer
                (string label, Classifier classifier, ILoggingProvider<T> Provider)
                : base(label, classifier.Dataset as DataSet<T>, Provider, false)
            {
                Classifier = classifier;
                SmoothedData = new SmoothedList<T>(3); // Half-life of the "weight" of a data point: three data points later (pretty fast - this is a light smoothing.)

                InterimInterval = TimeSpan.FromMilliseconds(100);
            }


            protected AsyncManualResetEvent FoundIt = new AsyncManualResetEvent();
            private Sequence<T> ResultSequence;

            public async Task<Sequence<T>> SeekUntilFound(GestureClass target = null)
            {
                Target = target ?? Target;
                Activate();
                await FoundIt.WaitAsync();

                var resultSeq = new Sequence<T>() { SourcePath = VectorProvider.LoggedData.Take(NumPointsAtPeak).ToArray() };
                return await Current.Analyze(resultSeq) as Sequence<T>;
            }

            protected override bool interimCriterion()
            {
                return true;
            }

            protected override async Task interimActionAsync()
            {
                // This section is basically the phase logic, step by step.  Minimum one step at each phase (just 'cause).
                // If we haven't begun, start us off!
                if (CurrentPhase == Phase.NotStarted)
                {
                    CurrentPhase = Phase.TooSoon;
                    return;
                }

                // Wait for a minimum number of points accumulated before you even START wasting time on calculating the recognition score.
                if (CurrentPhase == Phase.TooSoon)
                {
                    if (VectorProvider.LoggedData.Count >= MinimumNumPointsToBegin) CurrentPhase = Phase.Eligible;
                    return;
                }

                // Okay, now we actually need the data and its score.
                var seq = VectorProvider.LoggedData;
                var Seq = new Sequence<T>() { SourcePath = seq.ToArray() };
                await Current.Analyze(Seq);

                // If we've stated that we only care about one gesture class, and it's not that, then we're obviously not there yet.
                if (Target != null && Seq.RecognizedAsIndex != Target.index) return;

                // Eligible means we're allowed to check if we pass the threshold score yet.
                if (CurrentPhase == Phase.Eligible)
                {
                    if (Seq.RecognitionScore >= ThresholdScore)
                    {
                        CurrentPhase = Phase.PastThreshold;
                        BestScoreThusFar = Seq.RecognitionScore;
                    }
                    return;
                }

                // Once we pass the threshold, we're looking for a peak, so increase the best score until you drop (noticeably) below the prior best.
                if (CurrentPhase == Phase.PastThreshold)
                {
                    if (Seq.RecognitionScore > BestScoreThusFar)
                    {
                        BestScoreThusFar = Seq.RecognitionScore;
                    }
                    else if (Seq.RecognitionScore < DropToPercentageBeforeLabelingMaximum * BestScoreThusFar)
                    {
                        NumPointsAtPeak = NumberOfPointsLastScored;
                        CurrentPhase = Phase.PastPeak;
                        WorstScoreThusFar = Seq.RecognitionScore;
                        return;
                    }

                    NumberOfPointsLastScored = VectorProvider.LoggedData.Count;
                    return;
                }

                // If we think we found a peak, look for N consecutive decreases and then declare victory (using just the sequence up to and including the peak).
                if (CurrentPhase == Phase.PastPeak)
                {
                    if (Seq.RecognitionScore < WorstScoreThusFar)
                    {
                        NumberOfConsecutiveDecreases++;
                        WorstScoreThusFar = Seq.RecognitionScore;
                        if (NumberOfConsecutiveDecreases > MinimumConsecutiveDecreases)
                        {
                            FoundIt.Set(); // Everything else will take care of itself in SeekUntilFound().
                        }
                    }
                    else if (Seq.RecognitionScore > BestScoreThusFar) // Oops! Still goin' up!
                    {
                        NumberOfConsecutiveDecreases = 0;
                        BestScoreThusFar = Seq.RecognitionScore;
                        CurrentPhase = Phase.PastThreshold;
                        NumberOfPointsLastScored = VectorProvider.LoggedData.Count;
                        return;
                    }
                    else // Not an increase, but also not a consecutive decrease; no worries.
                    {
                        NumberOfConsecutiveDecreases = 0;
                        WorstScoreThusFar = Seq.RecognitionScore; // Okay, not the *absolute* worst score thus far, but whatevs.
                    }
                }
                
            }
        }
    }
}