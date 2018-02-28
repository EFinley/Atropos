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

            protected bool softStop = false;

            public T[] StopAndReturnResults()
            {
                VectorProvider.Deactivate();

                var ResultArray = VectorProvider.LoggedData;
                ResultArray = ResultArray.Smooth(3);
                softStop = true; // Pushes the actual stop out to the next phase of execution.
                return ResultArray.ToArray();
            }

            protected override bool nextStageCriterion()
            {
                return softStop;
            }

            public SequenceMetadata GetMetadata(Func<T, double> GetAccel = null, int startIndex = 0, int count = -1)
            {
                // Establish the actual defaults based on the passed parameters.
                count = (count <= 0 || count + startIndex > VectorProvider.LoggedData.Count) ? VectorProvider.LoggedData.Count - startIndex : count;
                GetAccel = GetAccel ?? SequenceMetadata.GetOverallMagnitude;

                // Initialize the result with the relevant 
                var result = new SequenceMetadata()
                {
                    NumPoints = count,
                    Delay = VectorProvider.Timestamps[startIndex],
                    Duration = VectorProvider.Timestamps[startIndex + count - 1] - VectorProvider.Timestamps[startIndex]
                };

                //Android.Util.Log.Debug("MachineLearning|MLStage", $"Sequence logged with {count} points, {result.Delay.TotalMilliseconds} ms delay, and {result.Duration.TotalMilliseconds} ms duration.");
                //Android.Util.Log.Debug("MachineLearning|MLStage", $"Timestamps went from {VectorProvider.Timestamps[0].TotalMilliseconds:f1}, {VectorProvider.Timestamps[1].TotalMilliseconds:f1}, {VectorProvider.Timestamps[2].TotalMilliseconds:f1}, to {VectorProvider.Timestamps.Last().TotalMilliseconds:f1} ");

                // Scan through (while we have the data to do so) and get the peak accel value using the supplied function
                foreach (int i in Enumerable.Range(startIndex, count))
                {
                    result.PeakAccel = Math.Max(result.PeakAccel, GetAccel(VectorProvider.LoggedData[i]));
                }

                // We can't get the Quality Score yet - have to fill that in after analysis (see Classifier.Recognize()).
                return result;
            }
        }

        public class SelfEndpointingSingleGestureRecognizer : MachineLearningStage
        {
            public GestureClass Target;

            protected Classifier Classifier;
            protected SmoothedList<T> SmoothedData;

            protected PeakFinder<T> PeakFinder;
            protected double ThresholdScore = 1.75;

            public SelfEndpointingSingleGestureRecognizer
                (string label, Classifier classifier, ILoggingProvider<T> Provider)
                : base(label, classifier.Dataset as DataSet<T>, Provider, false)
            {
                Classifier = classifier;
                SmoothedData = new SmoothedList<T>(3); // Half-life of the "weight" of a data point: three data points later (pretty fast - this is a light smoothing.)

                PeakFinder = new PeakFinder<T>(SmoothedData,
                    (seq) =>
                    {
                        var Seq = new Sequence<T>() { SourcePath = seq.ToArray() };
                        var analyzedSeq = Current.Analyze(Seq).Result;

                        // If we've stated that we only care about one gesture class, and it's not that, then we're obviously not there yet.
                        if (Target != null && Seq.RecognizedAsIndex != Target.index) return double.NaN;
                        else return analyzedSeq.RecognitionScore;
                    }, thresholdScore: 1.0, minLength: Classifier.Dataset?.MinSequenceLength ?? 5);
                InterimInterval = TimeSpan.FromMilliseconds(100);
            }

            public async Task<Sequence<T>> RunUntilFound(GestureClass target = null)
            {
                Target = target ?? Target;
                Activate();

                var foundSeqeuence = new Sequence<T>() { SourcePath = (await PeakFinder.FindBestSequenceAsync()).ToArray() };
                softStop = true;

                return Current.Analyze(foundSeqeuence).Result as Sequence<T>;
            }

            protected override bool interimCriterion()
            {
                return true;
            }

            protected override async Task interimActionAsync()
            {
                for (int i = SmoothedData.Count; i < VectorProvider.LoggedData.Count; i++)
                {
                    SmoothedData.Add(VectorProvider.LoggedData[i]);
                }

                PeakFinder.ConsiderAllAvailable(); // Will cause FindBestSequence() to fire, if indeed it has (now) found one.
            }

            protected override bool abortCriterion()
            {
                var overTime = RunTime > TimeSpan.FromSeconds(5);
                return overTime;
            }

            protected override Task abortActionAsync()
            {
                return Speech.SayAllOf("Timed out; try again.");
            }

            protected override Task nextStageActionAsync()
            {
                return Speech.SayAllOf("Got it.", new SoundOptions() { Speed = 1.75 });
            }
        }
    }

}