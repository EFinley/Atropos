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

namespace Atropos
{
    public interface ILoggingProvider : IProvider
    {
        // No content here - purely present as a 'marker' interface.
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
        public virtual List<T> LoggedData { get; private set; } = new List<T>(); 
        public List<TimeSpan> Intervals { get; private set; } = new List<TimeSpan>();
        public List<TimeSpan> Timestamps { get; private set; } = new List<TimeSpan>();
        protected IProvider<T> Provider;

        public LoggingSensorProvider(IProvider<T> provider) : this(provider, CancellationToken.None) { }
        public LoggingSensorProvider(IProvider<T> provider, CancellationToken externalToken) : base(externalToken, provider)
        {
            Provider = provider; // This is also available as providers[0], but would require casting to T each time.
        }

        protected override void DoWhenAllDataIsReady()
        {
            LoggedData.Add(Provider.Data);
            Timestamps.Add(Intervals.LastOrDefault() + Provider.Interval);
            Intervals.Add(Provider.Interval);
        }

        protected override T toImplicitType()
        {
            return LoggedData.LastOrDefault();
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
        public override List<T> LoggedData { get { return _smoothedData.ToList(); } }

        public SmoothLoggingProvider(IProvider<T> provider, int smoothingLength = DEFAULTLENGTH) : this(provider, smoothingLength, CancellationToken.None) { }
        public SmoothLoggingProvider(IProvider<T> provider, CancellationToken externalToken) : this(provider, DEFAULTLENGTH, externalToken) { }
        public SmoothLoggingProvider(IProvider<T> provider, int smoothingLength, CancellationToken externalToken) : base(provider, externalToken)
        {
            _smoothedData = new SmoothedList<T>(smoothingLength);
        }

        protected override void DoWhenAllDataIsReady()
        {
            _smoothedData.Add(Provider.Data);
            Timestamps.Add(Intervals.LastOrDefault() + providers[0].Interval);
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
            if (LoggedData.All(d => d.Magnitude() < 1e-10) && LoggedData.Count > 5) throw new Exception("Caught another zero list of data... WHY???");
            Timestamps.Add(Intervals.LastOrDefault() + providers[0].Interval);
            Intervals.Add(providers[0].Interval);
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
            Timestamps.Add(Intervals.LastOrDefault() + providers[0].Interval);
            Intervals.Add(providers[0].Interval);
        }
    }

    ///// <summary>
    ///// Keeps a log of accelerometer data and transforms it into the best-fit frame wherein the data lie
    ///// generally in a plane defined by gravity and by the axis in which most of the movement occurs.
    ///// </summary>
    //public class PlanarizedGestureProvider_new : LoggingSensorProvider<Vector2>
    //{
    //    //private List<Vector3> rawData = new List<Vector3>();
    //    //private List<Vector3> gravVectors = new List<Vector3>();
    //    //private Quaternion frameShift = Quaternion.Identity;
    //    //public IEnumerable<Vector3> transformedData { get { return rawData.Select(v => v.RotatedBy(frameShift)); } }
    //    private List<Vector6> data = new List<Vector6>();
    //    //public List<AugVector2> PlanarizedAccels, PlanarizedVelocs, PlanarizedDisplacements;
    //    public List<Vector2> PlanarizedAccels, PlanarizedVelocs, PlanarizedDisplacements;
    //    //public List<TimeSpan> Intervals;

    //    public double TotalWidth, TotalHeight;
    //    public TimeSpan TotalTime;
    //    public Vector3 PlaneNormal = Vector3.Zero;

    //    protected IVector3Provider AccelProvider;
    //    //protected FrameShiftedOrientationProvider GravProvider;
    //    protected IVector3Provider GravProvider;

    //    // Constructor inheritance must be explicit
    //    public PlanarizedGestureProvider_new(CancellationToken? externalStopToken = null)
    //        : base(
    //              //new MultiSensorProvider<Vector2>(externalStopToken ?? CancellationToken.None,
    //              //    new CorrectedAccelerometerProvider(SensorType.LinearAcceleration),
    //              //    new CorrectedAccelerometerProvider(SensorType.Gravity)),
    //              null,
    //              externalStopToken ?? CancellationToken.None)
    //    {
    //        AccelProvider = new CorrectedAccelerometerProvider(SensorType.LinearAcceleration);
    //        GravProvider = new CorrectedAccelerometerProvider(SensorType.Gravity);
    //        AddProvider(new MultiSensorProvider<Vector2>(StopToken, AccelProvider, GravProvider));

    //        //data = new List<AxisSeparatedVector>();
    //        PlanarizedAccels = new List<Vector2>();
    //        PlanarizedVelocs = new List<Vector2>();
    //        PlanarizedDisplacements = new List<Vector2>();
    //        //Intervals = new List<TimeSpan>();
    //    }

    //    protected override void DoWhenAllDataIsReady()
    //    {
    //        base.DoWhenAllDataIsReady();
    //        //rawData.Add(AccelProvider.Vector.RotatedBy(GravProvider.Quaternion));
    //        //rawData.Add(AccelProvider.Vector);
    //        //gravVectors.Add(GravProvider.Vector.Normalize());
    //        //data.Add(new AxisSeparatedVector() { Vector = AccelProvider.Vector, Gravity = GravProvider.Vector, Interval = AccelProvider.Interval });
    //        //Intervals.Add(AccelProvider.Interval);
    //        data.Add(new Vector6() { V1 = AccelProvider.Vector, V2 = GravProvider.Vector });
    //    }

    //    public void CalculateKinematics(Vector2? vInitial = null, bool forcePlaneVertical = true)
    //    {
    //        // TODO: Add smoothing??
    //        var planarizationTask = GetPlanarizedData(true, forcePlaneVertical);
    //        planarizationTask.Wait();
    //        PlanarizedAccels = planarizationTask.Result.ToList();

    //        // Work out the velocities & displacements, usually based on the assumption that the initial velocity is zero.
    //        // It won't be, which will introduce systematic errors, but we will address that separately.
    //        Vector2 V = vInitial ?? Vector2.Zero;
    //        Vector2 D = Vector2.Zero;
    //        TotalTime = TimeSpan.Zero;
    //        foreach (var A in PlanarizedAccels
    //            .Zip(Intervals, (Vector2 accel, TimeSpan interv)
    //                => new { a = accel, i = interv }).Skip(1))
    //        {
    //            V += A.a * (float)A.i.TotalSeconds;
    //            PlanarizedVelocs.Add(V);
    //            D += V * (float)A.i.TotalSeconds;
    //            PlanarizedDisplacements.Add(D);

    //            TotalTime += A.i;
    //        }

    //        var xValues = PlanarizedDisplacements.Select(d => d.X);
    //        var yValues = PlanarizedDisplacements.Select(d => d.Y);
    //        TotalWidth = xValues.Max() - xValues.Min();
    //        TotalHeight = yValues.Max() - yValues.Min();
    //    }

    //    public void RescaleKinematics(GestureClass tgtClass, double rescaleStrength = 1.0)
    //    {
    //        if (TotalTime == null) CalculateKinematics();

    //        if (tgtClass.AverageDuration.TotalMilliseconds < 1) // [Effectively] zero => no averages set yet.
    //        {
    //            tgtClass.UpdateAverages(TotalWidth, TotalHeight, TotalTime);
    //            return;
    //        }

    //        List<double> differences = new List<double>
    //        {
    //            TotalWidth - tgtClass.AverageWidth,
    //            TotalHeight - tgtClass.AverageHeight,
    //            (TotalTime - tgtClass.AverageDuration).TotalMilliseconds
    //        };

    //        List<double> sigmas = new List<double>
    //        {
    //            tgtClass.WidthSigma,
    //            tgtClass.HeightSigma,
    //            tgtClass.DurationSigma
    //        };

    //        // Chosen formula: adjust towards mean by (error) * arctan(error / 2sigma) * 2/pi.
    //        // At < 1sigma, this introduces only a small correction (0.3sigma at 1sigma), 
    //        // while at larger errors it basically corrects by all-but-one-sigma, give or take.
    //        List<float> corrections = differences
    //            .Zip(
    //            sigmas,
    //            (d, sigma) =>
    //            {
    //                return (float)(d * (Atan(d / sigma / 2)) * 2 / PI);
    //            }).ToList();

    //        // Hmmm.  Should I (a) adjust the total time and then use the adjusted time to work out the velocity changes,
    //        // or (b) ignore the total time adjustment because we're trying to just rescale the kinematics?
    //        // For now, let's go with (b).
    //        var AdjustmentVector = new Vector2(-corrections[0], -corrections[1]);
    //        AdjustmentVector = AdjustmentVector / ((float)TotalTime.TotalSeconds);

    //        CalculateKinematics(AdjustmentVector);
    //    }

    //    /// <summary>
    //    ///   Multiple Linear Regression.
    //    /// </summary>
    //    /// 
    //    /// <remarks>
    //    /// <para>
    //    ///   In multiple linear regression, the model specification is that the dependent
    //    ///   variable, denoted y_i, is a linear combination of the parameters (but need not
    //    ///   be linear in the independent x_i variables). As the linear regression has a
    //    ///   closed form solution, the regression coefficients can be computed by calling
    //    ///   the <see cref="Regress(double[][], double[])"/> method only once.</para>
    //    /// </remarks>
    //    /// 
    //    /// <example>
    //    ///  <para>
    //    ///   The following example shows how to fit a multiple linear regression model
    //    ///   to model a plane as an equation in the form ax + by + c = z. </para>
    //    ///   
    //    ///   <code>
    //    ///   // We will try to model a plane as an equation in the form
    //    ///   // "ax + by + c = z". We have two input variables (x and y)
    //    ///   // and we will be trying to find two parameters a and b and 
    //    ///   // an intercept term c.
    //    ///   
    //    ///   // Create a multiple linear regression for two input and an intercept
    //    ///   MultipleLinearRegression target = new MultipleLinearRegression(2, true);
    //    ///   
    //    ///   // Now suppose we have some points
    //    ///   double[][] inputs = 
    //    ///   {
    //    ///       new double[] { 1, 1 },
    //    ///       new double[] { 0, 1 },
    //    ///       new double[] { 1, 0 },
    //    ///       new double[] { 0, 0 },
    //    ///   };
    //    ///   
    //    ///   // located in the same Z (z = 1)
    //    ///   double[] outputs = { 1, 1, 1, 1 };
    //    ///   
    //    ///   
    //    ///   // Now we will try to fit a regression model
    //    ///   double error = target.Regress(inputs, outputs);
    //    ///   
    //    ///   // As result, we will be given the following:
    //    ///   double a = target.Coefficients[0]; // a = 0
    //    ///   double b = target.Coefficients[1]; // b = 0
    //    ///   double c = target.Coefficients[2]; // c = 1
    //    ///   
    //    ///   // Now, considering we were trying to find a plane, which could be
    //    ///   // described by the equation ax + by + c = z, and we have found the
    //    ///   // aforementioned coefficients, we can conclude the plane we were
    //    ///   // trying to find is giving by the equation:
    //    ///   //
    //    ///   //   ax + by + c = z
    //    ///   //     -> 0x + 0y + 1 = z
    //    ///   //     -> 1 = z.
    //    ///   //
    //    ///   // The plane containing the aforementioned points is, in fact,
    //    ///   // the plane given by z = 1.
    //    ///   </code>
    //    public async static Task<Vector3> FindBestFitPlane(IEnumerable<Vector6> inData, int inputIndex1 = 0, int inputIndex2 = 1, int outputIndex = 2)
    //    {

    //        // Create a multiple linear regression for two inputs, don't bother to calculate the intercept term.
    //        MultipleLinearRegression target = new MultipleLinearRegression(2, false);

    //        // Now use the X & Y axes of our accelerometer data
    //        double[][] inputs = inData.Select(v => new double[] { v.V1.Component(inputIndex1), v.V1.Component(inputIndex2) }).ToArray();

    //        // and their Z accelerations (note that since the Z ones are the most likely to be small, we may want to use (say) Y as an output of X & Z.
    //        double[] outputs = inData.Select(v => (double)v.V1.Component(outputIndex)).ToArray();

    //        //await Task.Run(() =>
    //        //{
    //        //    // Now we will try to fit a regression model
    //        //    double error = target.Regress(inputs, outputs);
    //        //});
    //        double error = target.Regress(inputs, outputs);

    //        // As result, we will be given the following:
    //        double a = target.Coefficients[0];
    //        double b = target.Coefficients[1];

    //        //var planeNormal = new Vector3((float)a, (float)b, -1).Normalize(); // Follows from the relationship between the normal vector and the equation of a plane.
    //        var planeNormal = new Vector3();
    //        planeNormal = planeNormal.SetComponent(inputIndex1, (float)a);
    //        planeNormal = planeNormal.SetComponent(inputIndex2, (float)b);
    //        planeNormal = planeNormal.SetComponent(outputIndex, -1);
    //        planeNormal = planeNormal.Normalize();

    //        Log.Debug("planeNormal", $"With {outputIndex} in terms of {inputIndex1} & {inputIndex2}, Vnormal = {planeNormal:f3}, error = {error:f3}");

    //        // Enforce a sign convention (since the same best-fit plane will match forwards or backwards and we want to get rid of the ambiguity)
    //        var maxDot = new Vector3[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ }
    //                .OrderByDescending(v => Math.Abs(v.Dot(planeNormal)))
    //                .First();
    //        if (maxDot.Dot(planeNormal) < 0) planeNormal = -planeNormal;
    //        return planeNormal;

    //    }

    //    public async Task<Vector2[]> GetPlanarizedData(bool replanarize = false, bool forcePlaneVertical = true)
    //    {
    //        if (replanarize || PlaneNormal == Vector3.Zero)
    //        {
    //            PlaneNormal = await FindBestFitPlane(data, 0, 1, 2);
    //            await FindBestFitPlane(data, 0, 2, 1);
    //            await FindBestFitPlane(data, 1, 2, 0);
    //        }

    //        //if (forcePlaneVertical) return data.Select(v => v.GetInPlaneZ(PlaneNormal)).ToArray();
    //        //else return data.Select(v => v.GetInPlane2(PlaneNormal)).ToArray();
    //        if (forcePlaneVertical) return data.Select(v => AxisSeparatedVector.GetInPlaneZ(v, PlaneNormal)).ToArray();
    //        else return data.Select(v => AxisSeparatedVector.GetInPlane2(v, PlaneNormal)).ToArray();
    //    }

    //    public async Task<Vector2[]> GetRawXYData()
    //    {
    //        return data.Select(v => new Vector2(v.V1.X, v.V1.Y)).ToArray();
    //    }

    //    public struct AxisSeparatedVector
    //    {
    //        public Vector3 Vector;
    //        public Vector3 Gravity;
    //        public TimeSpan Interval;

    //        // This version of the math expresses a vector as a component along the z-axis (world space)
    //        // and along an axis defined by the primary x-y vector of the sequence.  In essence, it
    //        // takes the gesture's plane and rotates it around an axis of constant Z until it is fully upright.
    //        public static Vector2 GetInPlaneZ(Vector6 source, Vector3 Vnormal)
    //        {
    //            var Vz = (Vector3.Dot(source.V1, source.V2) * source.V2 / source.V2.LengthSquared());
    //            var Vxy = source.V1 - Vz;
    //            var Voop = (Vector3.Dot(Vxy, Vnormal) * Vnormal / Vnormal.LengthSquared());
    //            var Vip = Vxy - Voop;
    //            return new Vector2(Vip.Length(), Vz.Length());
    //        }

    //        // This version of the math expresses the vector using basis vectors which define the primary
    //        // plane of the sequence
    //        public static Vector2 GetInPlane2(Vector6 source, Vector3 Vnormal)
    //        {
    //            var Nz = (Vector3.Dot(Vnormal, source.V2) * source.V2 / source.V2.LengthSquared());
    //            var Vz_ip = -1 * (Vnormal - Nz).Normalize();
    //            var Vperp_ip = Vector3.Cross(Vz_ip, Vnormal.Normalize());
    //            return new Vector2(Vector3.Dot(source.V1, Vz_ip), Vector3.Dot(source.V1, Vperp_ip));
    //        }
    //    }

    //    public struct AugVector2
    //    {
    //        public Vector2 Vector;
    //        public TimeSpan Interval;

    //        public AugVector2(float Vx, float Vz, TimeSpan t)
    //        {
    //            Vector = new Vector2(Vx, Vz);
    //            Interval = t;
    //        }
    //        public AugVector2(Vector2 vector, TimeSpan t)
    //        {
    //            Vector = vector;
    //            Interval = t;
    //        }
    //    }
    //}
}