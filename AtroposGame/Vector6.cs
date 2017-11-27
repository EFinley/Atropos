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
using Android.Hardware;
using System.Threading;

namespace com.Atropos
{
    /// <summary>
    /// A utility object intended to hold two vector quantities, such as acceleration and gyroscope data, or linear
    /// acceleration & gravity, tightly coupled.  Used particularly in things like the Machine Learning namespace,
    /// see for example <see cref="Machine_Learning.Feature{T}"/>. 
    /// </summary>
    public struct Vector6
    {
        public Vector3 V1;
        public Vector3 V2;
    }

    /// <summary>
    /// An <see cref="ILoggingProvider{T}"/> intended to wrap together two vectors into a single <see cref="Vector6"/>
    /// and log the result.  Used chiefly in the Machine Learning namespace.
    /// </summary>
    public class DualVector3LoggingProvider : MultiSensorProvider<Vector6>, ILoggingProvider<Vector6>
    {
        protected new Vector6 toImplicitType()
        {
            return LoggedData.LastOrDefault();
        }

        public List<Vector6> LoggedData { get; private set; } = new List<Vector6>();
        public List<TimeSpan> Intervals { get; private set; } = new List<TimeSpan>();

        public DualVector3LoggingProvider(IProvider<Vector3> provider1, IProvider<Vector3> provider2)
            : base(provider1, provider2) { }

        public DualVector3LoggingProvider(IProvider<Vector3> provider1, IProvider<Vector3> provider2, CancellationToken externalToken)
            : base(externalToken, provider1, provider2) { }

        public DualVector3LoggingProvider(SensorType sensorType1, SensorType sensorType2)
            : this(new CorrectedAccelerometerProvider(sensorType1), new CorrectedAccelerometerProvider(sensorType2)) { }

        public DualVector3LoggingProvider(SensorType sensorType1, SensorType sensorType2, CancellationToken externalToken)
            : this(new CorrectedAccelerometerProvider(sensorType1), new CorrectedAccelerometerProvider(sensorType2), externalToken) { }

        protected override void DoWhenAllDataIsReady()
        {
            LoggedData.Add(new Vector6() { V1 = (providers[0] as IProvider<Vector3>).Data, V2 = (providers[1] as IProvider<Vector3>).Data });
            Intervals.Add(providers[0].Interval);
        }
    }
}