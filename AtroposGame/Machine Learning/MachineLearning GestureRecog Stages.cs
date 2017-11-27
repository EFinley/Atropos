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
using com.Atropos.DataStructures;

namespace com.Atropos.Machine_Learning
{
    public partial class MachineLearningActivity<T> where T : struct
    {
        public class MachineLearningStage : GestureRecognizerStage
        {
            //private PlanarizedGestureProvider VectorProvider;
            protected ILoggingProvider<T> VectorProvider;
            protected DataSet<T> DataSet;

            //public MachineLearningStage(string label, IDataset dataset, PlanarizedGestureProvider Provider = null, bool AutoStart = false) : base(label)
            public MachineLearningStage(string label, DataSet<T> dataset, ILoggingProvider<T> Provider, bool AutoStart = false) : base(label)
            {
                DataSet = dataset;

                //VectorProvider = Provider ?? new PlanarizedGestureProvider();
                VectorProvider = Provider;
                SetUpProvider(VectorProvider);
                VectorProvider.Delay = Android.Hardware.SensorDelay.Fastest; // Turns out that the default, SensorDelay.Game, doesn't capture fast enough for our style of gestures.

                if (AutoStart) Activate();
            }

            private bool softCancel = false;
            //public Datapoint<T>[] StopAndReturnResults()
            public T[] StopAndReturnResults()
            {
                VectorProvider.Deactivate();

                ////VectorProvider.CalculateKinematics(null, false);
                ////var planarizationTask = VectorProvider.GetPlanarizedData(true, false);
                //var planarProvider = VectorProvider as PlanarizedGestureProvider;
                //var planarizationTask = planarProvider.GetRawXYData();
                //planarizationTask.Wait();
                //planarProvider.PlanarizedAccels = planarizationTask.Result.ToList();

                //var ResultArray = planarProvider.PlanarizedAccels.Smooth(3);

                // Vector6 has no predefined averaging structure, so we need to decouple, smooth, and recouple the data streams.
                var ResultArray = VectorProvider.LoggedData;
                //if (typeof(T).IsOneOf(typeof(Vector2), typeof(Vector3), typeof(Quaternion)))
                //{
                //    ResultArray = ResultArray.Smooth(3);
                //}
                //if (typeof(T) == typeof(Vector6))
                //{
                //    var results1 = ResultArray.Select(r => Operator.Convert<T, Vector6>(r).V1).Smooth(3);
                //    var results2 = ResultArray.Select(r => Operator.Convert<T, Vector6>(r).V2).Smooth(3);
                //    ResultArray = results1.Zip(results2, (r1, r2) => Operator.Convert<Vector6, T>(new Vector6() { V1 = r1, V2 = r2 })).ToList();
                //}
                // VectorCluster objects have their own overloads of Smooth() and similar; see RollingAverage<T> and similar.
                var testPoint = Datapoint.From<T>(ResultArray.Last());
                var x = 0; // Dummy line
                ResultArray = ResultArray.Smooth(3);
                softCancel = true;
                //return new Sequence<T>() { SourcePath = ResultArray.ConvertAll(r => (Datapoint<T>)r).ToArray() };
                //return ResultArray.ConvertAll(r => (Datapoint<T>)r).ToArray();
                return ResultArray.ToArray();
                //return ResultArray.ConvertAll(r => (Datapoint<T>)Operator.Convert<Vector2, T>(r)).ToArray();
            }

            protected override bool abortCriterion()
            {
                return softCancel;
            }
        }

        public class SelfEndpointingSingleGestureRecognizer : MachineLearningStage
        {
            protected Classifier Classifier;
            protected SmoothedList<T> SmoothedData;

            public SelfEndpointingSingleGestureRecognizer
                (string label, DataSet<T> dataset, Classifier classifier, ILoggingProvider<T> Provider, bool AutoStart = false)
                : base(label, dataset, Provider, false)
            {
                Classifier = classifier;
                SmoothedData = new SmoothedList<T>(3); // Half-life of the "weight" of a data point: three data points later (pretty fast - this is a light smoothing.)

                InterimInterval = TimeSpan.FromMilliseconds(100);

                if (AutoStart) Activate();
            }

            protected AsyncManualResetEvent FoundIt = new AsyncManualResetEvent();

            protected override bool interimCriterion()
            {
                return true;
            }

            protected override async Task interimActionAsync()
            {
                var seq = VectorProvider.LoggedData;
                var Seq = new Sequence<T>() { SourcePath = seq.ToArray() };

                await Current.Analyze(Seq);
                // TODO - figure-of-merit stuff (or otherwise handling variable time windows here)
            }

            public async Task<GestureClass> SeekUntilFound()
            {
                Activate();
                await FoundIt.WaitAsync();

                return Current.SelectedGestureClass;
            }
        }
    }
}