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
using Android.Hardware;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Accord.Statistics.Models.Regression.Linear;
using Accord.MachineLearning;
using static System.Math;
using Log = Android.Util.Log;
using Atropos.DataStructures;
using Nito.AsyncEx;

namespace Atropos
{
    public interface ILoggingProvider : IProvider
    {
        void ClearData();
    }

    public interface ILoggingProvider<T> : IProvider, ILoggingProvider where T : struct
    {
        List<T> LoggedData { get; }
        List<TimeSpan> Intervals { get; }
        List<TimeSpan> Timestamps { get; }
    }

    /// <summary>
    /// Wrapper class - can be folded around a single <see cref="IProvider{T}"/> to provide it with logging functionality.
    /// Intended for use with any form of Provider which simply needs this added capability.  Specialized LoggingProvider
    /// variations, such as the <see cref="ClusterLoggingProvider{T1, T2}"/>, require a little less overhead when they are
    /// applicable.
    /// </summary>
    /// <typeparam name="T">The data type - usually Vector2, Vector3, Quaternion, or <see cref="Vector6"/> - or anything
    /// which implements IDatapoint.</typeparam>
    public class LoggingSensorProvider<T> : MultiSensorProvider<T>, ILoggingProvider<T> where T : struct
    {
        public virtual List<T> LoggedData { get; protected set; } = new List<T>(); 
        public List<TimeSpan> Intervals { get; protected set; } = new List<TimeSpan>();
        public List<TimeSpan> Timestamps { get; protected set; } = new List<TimeSpan>();
        protected IProvider<T> Provider;

        public LoggingSensorProvider(IProvider<T> provider) : this(provider, CancellationToken.None) { }
        public LoggingSensorProvider(IProvider<T> provider, CancellationToken externalToken) : base(externalToken, provider)
        {
            Provider = provider; // This is also available as providers[0], but would require casting to T each time.
        }

        protected override void DoWhenAllDataIsReady()
        {
            LoggedData.Add(Provider.Data);
            //Timestamps.Add(Timestamps.LastOrDefault() + Provider.Interval);
            Timestamps.Add(Provider.RunTime);
            Intervals.Add(Provider.Interval);
        }

        protected override T toImplicitType()
        {
            return LoggedData.LastOrDefault();
        }

        public void ClearData()
        {
            LoggedData.Clear();
            Intervals.Clear();
            Timestamps.Clear();
        }
    }

    /// <summary>
    /// Wrapper class - can be folded around a single <see cref="IProvider{T}"/> to provide it with logging functionality
    /// and built-in smoothing.
    /// Intended for use with any form of Provider which simply needs this added capability.  Specialized LoggingProvider
    /// variations, such as the <see cref="ClusterLoggingProvider{T1, T2}"/>, require a little less overhead when they are
    /// applicable.
    /// </summary>
    /// <typeparam name="T">The data type - usually Vector2, Vector3, Quaternion, or <see cref="Vector6"/> - or anything
    /// which implements IDatapoint.</typeparam>
    public class SmoothLoggingProvider<T> : LoggingSensorProvider<T>, ILoggingProvider<T> where T : struct
    {
        private const int DEFAULTLENGTH = 3;
        protected SmoothedList<T> _smoothedData;
        public override List<T> LoggedData { get { return _smoothedData?.ToList() ?? new List<T>(); } }

        public SmoothLoggingProvider(IProvider<T> provider, int smoothingLength = DEFAULTLENGTH) : this(provider, smoothingLength, CancellationToken.None) { }
        public SmoothLoggingProvider(IProvider<T> provider, CancellationToken externalToken) : this(provider, DEFAULTLENGTH, externalToken) { }
        public SmoothLoggingProvider(IProvider<T> provider, int smoothingLength, CancellationToken externalToken) : base(provider, externalToken)
        {
            _smoothedData = new SmoothedList<T>(smoothingLength);
        }

        protected override void DoWhenAllDataIsReady()
        {
            _smoothedData.Add(Provider.Data);
            //Timestamps.Add(Timestamps.LastOrDefault() + providers[0].Interval);
            Timestamps.Add(providers[0].RunTime);
            Intervals.Add(Provider.Interval);
        }
    }

    /// <summary>
    /// An <see cref="ILoggingProvider{T}"/> intended to wrap together two vectors into a single <see cref="Datapoint{T1, T2}"/>
    /// and log the result.  Used chiefly in the Machine Learning namespace.
    /// </summary>
    public class ClusterLoggingProvider<T1, T2>
        : MultiSensorProvider<Datapoint<T1, T2>>, ILoggingProvider<Datapoint<T1, T2>>
        where T1 : struct
        where T2 : struct
    {
        protected override Datapoint<T1, T2> toImplicitType()
        {
            return LoggedData.LastOrDefault();
        }

        public virtual List<Datapoint<T1, T2>> LoggedData { get; private set; } = new List<Datapoint<T1, T2>>();
        public List<TimeSpan> Intervals { get; private set; } = new List<TimeSpan>();
        public List<TimeSpan> Timestamps { get; private set; } = new List<TimeSpan>();

        // Normal ctors... 
        public ClusterLoggingProvider(IProvider<T1> provider1, IProvider<T2> provider2)
            : base(provider1, provider2) { }

        public ClusterLoggingProvider(IProvider<T1> provider1, IProvider<T2> provider2, CancellationToken externalToken)
            : base(externalToken, provider1, provider2) { }

        #region Lists of which types of sensors ought to provide which data types (used for our shortcut ctors)
        private SensorType[] OrientationSensorTypes = new SensorType[]
        {
            SensorType.GameRotationVector, SensorType.GeomagneticRotationVector, SensorType.RotationVector
        };
        private SensorType[] VectorSensorTypes = new SensorType[]
        {
            SensorType.Accelerometer, SensorType.Gravity, SensorType.LinearAcceleration,
            SensorType.Gyroscope, SensorType.GyroscopeUncalibrated,
            SensorType.MagneticField, SensorType.MagneticFieldUncalibrated
        };
        #endregion

        public ClusterLoggingProvider(SensorType sensorType1, SensorType sensorType2, CancellationToken externalToken)
            //: this(new CorrectedAccelerometerProvider(sensorType1), new CorrectedAccelerometerProvider(sensorType2)) { }
            : base()
        {
            // Quick shortcut lambda so I don't have to type all that crap out just to get the words "SensorType.Accelerometer" anytime I want 'em.
            Func<SensorType, string> TypeName = (SensorType s) => Enum.GetName(typeof(SensorType), s);

            if (sensorType1.IsOneOf(OrientationSensorTypes))
            {
                if (!default(T1).CanConvert<T1, Quaternion>()) throw new InvalidCastException($"SensorType {TypeName(sensorType1)} requires that T1 be Quaternion type.");
                AddProvider(new FrameShiftedOrientationProvider(sensorType1));
            }
            else if (sensorType1.IsOneOf(VectorSensorTypes))
            {
                if (!default(T1).CanConvert<T1, Vector3>()) throw new InvalidCastException($"SensorType {TypeName(sensorType1)} requires that T1 be Vector3 type.");
                AddProvider(new CorrectedAccelerometerProvider(sensorType1));
            }
            else throw new ArgumentException($"Sensor type {TypeName(sensorType1)} is neither Quaternion nor Vector3.  Construct its provider manually without using this shortcut form.");

            if (sensorType2.IsOneOf(OrientationSensorTypes))
            {
                if (!default(T2).CanConvert<T2, Quaternion>()) throw new InvalidCastException($"SensorType {TypeName(sensorType2)} requires that T2 be Quaternion type.");
                AddProvider(new FrameShiftedOrientationProvider(sensorType2));
            }
            else if (sensorType2.IsOneOf(VectorSensorTypes))
            {
                if (!default(T2).CanConvert<T2, Vector3>()) throw new InvalidCastException($"SensorType {TypeName(sensorType2)} requires that T1 be Vector3 type.");
                AddProvider(new CorrectedAccelerometerProvider(sensorType2));
            }
            else throw new ArgumentException($"Sensor type {TypeName(sensorType2)} is neither Quaternion nor Vector3.  Construct its provider manually without using this shortcut form.");

        }

        public ClusterLoggingProvider(SensorType sensorType1, SensorType sensorType2)
            : this(sensorType1, sensorType2, CancellationToken.None) { }

        protected override void DoWhenAllDataIsReady()
        {
            LoggedData.Add(new Datapoint<T1, T2>() { Value1 = (providers[0] as IProvider<T1>).Data, Value2 = (providers[1] as IProvider<T2>).Data });
            //if (LoggedData.All(d => d.Magnitude() < 1e-10) && LoggedData.Count > 5) throw new Exception("Caught another zero list of data... WHY???");
            //Timestamps.Add(Timestamps.LastOrDefault() + providers[0].Interval);
            Timestamps.Add(providers[0].RunTime);
            Intervals.Add(providers[0].Interval);
        }

        public void ClearData()
        {
            LoggedData.Clear();
            Timestamps.Clear();
            Intervals.Clear();
        }
    }

    public class SmoothClusterProvider<T1, T2> : ClusterLoggingProvider<T1, T2>, ILoggingProvider<Datapoint<T1, T2>>
        where T1 : struct
        where T2 : struct
    {
        private const int DEFAULTLENGTH = 3;
        private SmoothedList<Datapoint<T1, T2>> _smoothedData;
        public override List<Datapoint<T1, T2>> LoggedData { get { return _smoothedData.ToList(); } }

        public SmoothClusterProvider(IProvider<T1> provider1, IProvider<T2> provider2, int smoothingLength = DEFAULTLENGTH) : this(provider1, provider2, smoothingLength, CancellationToken.None) { }
        public SmoothClusterProvider(IProvider<T1> provider1, IProvider<T2> provider2, CancellationToken externalToken) : this(provider1, provider2, DEFAULTLENGTH, externalToken) { }
        public SmoothClusterProvider(IProvider<T1> provider1, IProvider<T2> provider2, int smoothingLength, CancellationToken externalToken) : base(provider1, provider2, externalToken)
        {
            _smoothedData = new SmoothedList<Datapoint<T1, T2>>(smoothingLength);
        }

        public SmoothClusterProvider(SensorType sensorType1, SensorType sensorType2, int smoothingLength = DEFAULTLENGTH) : this(sensorType1, sensorType2, smoothingLength, CancellationToken.None) { }
        public SmoothClusterProvider(SensorType sensorType1, SensorType sensorType2, CancellationToken externalToken) : this(sensorType1, sensorType2, DEFAULTLENGTH, externalToken) { }
        public SmoothClusterProvider(SensorType sensorType1, SensorType sensorType2, int smoothingLength, CancellationToken externalToken) : base(sensorType1, sensorType2, externalToken)
        {
            _smoothedData = new SmoothedList<Datapoint<T1, T2>>(smoothingLength);
        }

        protected override void DoWhenAllDataIsReady()
        {
            _smoothedData.Add(new Datapoint<T1, T2>() { Value1 = (providers[0] as IProvider<T1>).Data, Value2 = (providers[1] as IProvider<T2>).Data });
            //Timestamps.Add(Timestamps.LastOrDefault() + providers[0].Interval);
            Timestamps.Add(providers[0].RunTime);
            Intervals.Add(providers[0].Interval);
        }
    }

    public class ContinuousLoggingProvider<T> : LoggingSensorProvider<T> where T : struct
    {
        private string _tag = "ContinuousLoggingProvider";

        public CancellationTokenSource SessionCTS;
        public AsyncAutoResetEvent DeactivationSignal;
        public int DeactivationCounter = 10;
        public AsyncAutoResetEvent AdditionalPointsSignal;
        protected TimeSpan LagBefore;
        public TimeSpan LagAfter;
        protected TimeSpan SessionActivationTimestamp;
        //protected TimeSpan SessionDeactivationTimestamp;

        protected TimeSpan InitialInterval = TimeSpan.Zero;
        protected TimeSpan AccumulatedRuntime = TimeSpan.Zero;
        public override TimeSpan RunTime => Provider.RunTime + AccumulatedRuntime;

        public ContinuousLoggingProvider(IProvider<T> provider, TimeSpan lagBefore = default(TimeSpan), TimeSpan lagAfter = default(TimeSpan), CancellationToken? externalToken = null)
            : base(provider, externalToken ?? CancellationToken.None)
        {
            LagBefore = (lagBefore == default(TimeSpan)) ? TimeSpan.FromMilliseconds(150) : lagBefore;
            LagAfter = (lagAfter == default(TimeSpan)) ? TimeSpan.FromMilliseconds(150) : lagAfter;

            DeactivationSignal = new AsyncAutoResetEvent();
            AdditionalPointsSignal = new AsyncAutoResetEvent();
        }

        private bool AllowDataUpdates = true;
        protected override void DoWhenAllDataIsReady()
        {
            //if (Res.DebuggingSignalFlag)
            //    Log.Debug(_tag, $"DoWhenAll: {LoggedData.Count} data pts.");
            if (AllowDataUpdates) //base.DoWhenAllDataIsReady();
            {
                LoggedData.Add(Provider.Data);
                //Timestamps.Add(Timestamps.LastOrDefault() + Provider.Interval);
                Timestamps.Add(Provider.RunTime + AccumulatedRuntime);
                Intervals.Add(Provider.Interval);
            }
            //if (SessionCTS != null && SessionCTS.IsCancellationRequested)
            //{
            //    if (DeactivationCounter == 0)
            //    {
            //        SessionCTS = null;
            //        DeactivationCounter = 10;
            //        AdditionalPointsSignal.Set();
            //    }
            //    else DeactivationCounter--;
            //}

            if (InitialInterval == TimeSpan.Zero)
                InitialInterval = Provider.Interval;
        }

        private async void DoTimeoutLoop()
        {
            while (Provider.IsActive)
            {
                await Task.Delay(1000);
                if (!SessionActive)
                {
                    ClearDataOlderThan(TimeSpan.FromSeconds(1));
                    if (Provider.Interval > InitialInterval + TimeSpan.FromMilliseconds(120))
                    {
                        // Lagging!
                        Log.Debug(_tag, $"Excessive lag detected ({Provider.Interval.TotalMilliseconds:f0} ms).  Might need to figure out how to reset Provider.");
                        //AccumulatedRuntime += Provider.RunTime;
                        //Provider.Deactivate();
                        //Provider.Activate();
                    }
                }
            }
        }

        public void ClearDataOlderThan(TimeSpan span)
        {
            if (Timestamps.Count == 0) return;
            var currentTimestamp = Timestamps.Last();
            var numToSkip = Timestamps.FindIndex(t => t + span >= currentTimestamp);
            //Log.Debug(_tag, $"At {currentTimestamp}, clearing out {numToSkip} data points, keeping as of {Timestamps.Skip(numToSkip).First()}.");
            //Provider.Deactivate();
            AllowDataUpdates = false;
            LoggedData = LoggedData.Skip(numToSkip).ToList();
            Intervals = Intervals.Skip(numToSkip).ToList();
            Timestamps = Timestamps.Skip(numToSkip).ToList();
            AllowDataUpdates = true;
            //Provider.Activate();
        }

        public void TrueActivate(CancellationToken? externalStopToken = null)
        {
            base.Activate(externalStopToken);
            DoTimeoutLoop();
        }

        public void TrueDeactivate()
        {
            base.Deactivate();
        }

        public override void Activate(CancellationToken? externalStopToken = null)
        {
            if (SessionCTS != null && !SessionCTS.IsCancellationRequested) return;
            SessionCTS = CancellationTokenSource.CreateLinkedTokenSource(externalStopToken ?? CancellationToken.None);

            ClearDataOlderThan(LagBefore);
        }

        public override void Deactivate()
        {
            //Res.DebuggingSignalFlag = true;
            //Log.Debug(_tag, $"Upon deactivation requested: {LoggedData.Count} pts @ {DateTime.Now}.");
            //Task.Delay(LagAfter).ContinueWith((t) =>
            //{
            //    Log.Debug(_tag, $"After deactivation delay: {LoggedData.Count} pts @ {DateTime.Now}.");
            //    DataReadySignal.Set();
            //    Task.Delay(100).ContinueWith((t2) => { SessionCTS?.Cancel(); }); // Allows time for other code to respond to the signal (and grab our data).                
            //});
            //DeactivationSignal.Set();
            //SessionCTS?.CancelAfter(LagAfter);
            SessionCTS?.Cancel();
        }

        //public override bool IsActive { get { return (SessionCTS != null && !SessionCTS.IsCancellationRequested); } }
        public bool SessionActive { get { return SessionCTS != null && !SessionCTS.IsCancellationRequested; } }

    }
}