//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//using Android.App;
//using Android.Content;
//using Android.OS;
//using Android.Runtime;
//using Android.Views;
//using Android.Widget;
//using Android.Hardware;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Numerics;
//using Accord.Statistics.Models.Regression.Linear;
//using Accord.MachineLearning;
//using static System.Math;
//using Log = Android.Util.Log;

//namespace com.Atropos.Machine_Learning
//{
//    /// <summary>
//    /// Keeps a log of accelerometer data and transforms it into the best-fit frame wherein the data lie
//    /// generally in a plane defined by gravity and by the axis in which most of the movement occurs.
//    /// </summary>
//    public class PlanarizedGestureProvider : MultiSensorProvider, ILoggingProvider<Vector2>
//    {
//        //private List<Vector3> rawData = new List<Vector3>();
//        //private List<Vector3> gravVectors = new List<Vector3>();
//        //private Quaternion frameShift = Quaternion.Identity;
//        //public IEnumerable<Vector3> transformedData { get { return rawData.Select(v => v.RotatedBy(frameShift)); } }
//        private List<AxisSeparatedVector> data;
//        //public List<AugVector2> PlanarizedAccels, PlanarizedVelocs, PlanarizedDisplacements;
//        public List<Vector2> PlanarizedAccels, PlanarizedVelocs, PlanarizedDisplacements;
//        public List<TimeSpan> Intervals { get; private set; }

//        public List<Vector2> LoggedData { get { return PlanarizedAccels ?? new List<Vector2>(); } }

//        public double TotalWidth, TotalHeight;
//        public TimeSpan TotalTime;
//        public Vector3 PlaneNormal = Vector3.Zero;

//        protected IVector3Provider AccelProvider;
//        //protected FrameShiftedOrientationProvider GravProvider;
//        protected IVector3Provider GravProvider;

//        // Constructor inheritance must be explicit
//        public PlanarizedGestureProvider(CancellationToken? externalStopToken = null)
//            : base(externalStopToken ?? CancellationToken.None,
//                  new CorrectedAccelerometerProvider(SensorType.LinearAcceleration),
//                  new CorrectedAccelerometerProvider(SensorType.Gravity))
//        {
//            AccelProvider = providers[0] as CorrectedAccelerometerProvider;
//            GravProvider = providers[1] as CorrectedAccelerometerProvider;

//            data = new List<AxisSeparatedVector>();
//            PlanarizedAccels = new List<Vector2>();
//            PlanarizedVelocs = new List<Vector2>();
//            PlanarizedDisplacements = new List<Vector2>();
//            Intervals = new List<TimeSpan>();
//        }

//        protected override void DoWhenAllDataIsReady()
//        {
//            //rawData.Add(AccelProvider.Vector.RotatedBy(GravProvider.Quaternion));
//            //rawData.Add(AccelProvider.Vector);
//            //gravVectors.Add(GravProvider.Vector.Normalize());
//            data.Add(new AxisSeparatedVector() { Vector = AccelProvider.Vector, Gravity = GravProvider.Vector, Interval = AccelProvider.Interval });
//            Intervals.Add(AccelProvider.Interval);
//        }

//        public void CalculateKinematics(Vector2? vInitial = null, bool forcePlaneVertical = true)
//        {
//            // TODO: Add smoothing??
//            var planarizationTask = GetPlanarizedData(true, forcePlaneVertical);
//            planarizationTask.Wait();
//            PlanarizedAccels = planarizationTask.Result.ToList();

//            // Work out the velocities & displacements, usually based on the assumption that the initial velocity is zero.
//            // It won't be, which will introduce systematic errors, but we will address that separately.
//            Vector2 V = vInitial ?? Vector2.Zero;
//            Vector2 D = Vector2.Zero;
//            TotalTime = TimeSpan.Zero;
//            foreach (var A in PlanarizedAccels
//                .Zip(Intervals, (Vector2 accel, TimeSpan interv)
//                    => new { a = accel, i = interv }).Skip(1))
//            {
//                V += A.a * (float)A.i.TotalSeconds;
//                PlanarizedVelocs.Add(V);
//                D += V * (float)A.i.TotalSeconds;
//                PlanarizedDisplacements.Add(D);

//                TotalTime += A.i;
//            }

//            var xValues = PlanarizedDisplacements.Select(d => d.X);
//            var yValues = PlanarizedDisplacements.Select(d => d.Y);
//            TotalWidth = xValues.Max() - xValues.Min();
//            TotalHeight = yValues.Max() - yValues.Min();
//        }

//        public void RescaleKinematics(GestureClass tgtClass, double rescaleStrength = 1.0)
//        {
//            if (TotalTime == null) CalculateKinematics();

//            if (tgtClass.AverageDuration.TotalMilliseconds < 1) // [Effectively] zero => no averages set yet.
//            {
//                tgtClass.UpdateAverages(TotalWidth, TotalHeight, TotalTime);
//                return;
//            }

//            List<double> differences = new List<double>
//            {
//                TotalWidth - tgtClass.AverageWidth,
//                TotalHeight - tgtClass.AverageHeight,
//                (TotalTime - tgtClass.AverageDuration).TotalMilliseconds
//            };

//            List<double> sigmas = new List<double>
//            {
//                tgtClass.WidthSigma,
//                tgtClass.HeightSigma,
//                tgtClass.DurationSigma
//            };

//            // Chosen formula: adjust towards mean by (error) * arctan(error / 2sigma) * 2/pi.
//            // At < 1sigma, this introduces only a small correction (0.3sigma at 1sigma), 
//            // while at larger errors it basically corrects by all-but-one-sigma, give or take.
//            List<float> corrections = differences
//                .Zip(
//                sigmas,
//                (d, sigma) =>
//                {
//                    return (float)(d * (Atan(d / sigma / 2)) * 2 / PI);
//                }).ToList();

//            // Hmmm.  Should I (a) adjust the total time and then use the adjusted time to work out the velocity changes,
//            // or (b) ignore the total time adjustment because we're trying to just rescale the kinematics?
//            // For now, let's go with (b).
//            var AdjustmentVector = new Vector2(-corrections[0], -corrections[1]);
//            AdjustmentVector = AdjustmentVector / ((float)TotalTime.TotalSeconds);

//            CalculateKinematics(AdjustmentVector);
//        }

//        /// <summary>
//        ///   Multiple Linear Regression.
//        /// </summary>
//        /// 
//        /// <remarks>
//        /// <para>
//        ///   In multiple linear regression, the model specification is that the dependent
//        ///   variable, denoted y_i, is a linear combination of the parameters (but need not
//        ///   be linear in the independent x_i variables). As the linear regression has a
//        ///   closed form solution, the regression coefficients can be computed by calling
//        ///   the <see cref="Regress(double[][], double[])"/> method only once.</para>
//        /// </remarks>
//        /// 
//        /// <example>
//        ///  <para>
//        ///   The following example shows how to fit a multiple linear regression model
//        ///   to model a plane as an equation in the form ax + by + c = z. </para>
//        ///   
//        ///   <code>
//        ///   // We will try to model a plane as an equation in the form
//        ///   // "ax + by + c = z". We have two input variables (x and y)
//        ///   // and we will be trying to find two parameters a and b and 
//        ///   // an intercept term c.
//        ///   
//        ///   // Create a multiple linear regression for two input and an intercept
//        ///   MultipleLinearRegression target = new MultipleLinearRegression(2, true);
//        ///   
//        ///   // Now suppose we have some points
//        ///   double[][] inputs = 
//        ///   {
//        ///       new double[] { 1, 1 },
//        ///       new double[] { 0, 1 },
//        ///       new double[] { 1, 0 },
//        ///       new double[] { 0, 0 },
//        ///   };
//        ///   
//        ///   // located in the same Z (z = 1)
//        ///   double[] outputs = { 1, 1, 1, 1 };
//        ///   
//        ///   
//        ///   // Now we will try to fit a regression model
//        ///   double error = target.Regress(inputs, outputs);
//        ///   
//        ///   // As result, we will be given the following:
//        ///   double a = target.Coefficients[0]; // a = 0
//        ///   double b = target.Coefficients[1]; // b = 0
//        ///   double c = target.Coefficients[2]; // c = 1
//        ///   
//        ///   // Now, considering we were trying to find a plane, which could be
//        ///   // described by the equation ax + by + c = z, and we have found the
//        ///   // aforementioned coefficients, we can conclude the plane we were
//        ///   // trying to find is giving by the equation:
//        ///   //
//        ///   //   ax + by + c = z
//        ///   //     -> 0x + 0y + 1 = z
//        ///   //     -> 1 = z.
//        ///   //
//        ///   // The plane containing the aforementioned points is, in fact,
//        ///   // the plane given by z = 1.
//        ///   </code>
//        public async static Task<Vector3> FindBestFitPlane(IEnumerable<AxisSeparatedVector> inData, int inputIndex1 = 0, int inputIndex2 = 1, int outputIndex = 2)
//        {

//            // Create a multiple linear regression for two inputs, don't bother to calculate the intercept term.
//            MultipleLinearRegression target = new MultipleLinearRegression(2, false);

//            // Now use the X & Y axes of our accelerometer data
//            double[][] inputs = inData.Select(v => new double[] { v.Vector.Component(inputIndex1), v.Vector.Component(inputIndex2) }).ToArray();

//            // and their Z accelerations (note that since the Z ones are the most likely to be small, we may want to use (say) Y as an output of X & Z.
//            double[] outputs = inData.Select(v => (double)v.Vector.Component(outputIndex)).ToArray();

//            //await Task.Run(() =>
//            //{
//            //    // Now we will try to fit a regression model
//            //    double error = target.Regress(inputs, outputs);
//            //});
//            double error = target.Regress(inputs, outputs);

//            // As result, we will be given the following:
//            double a = target.Coefficients[0];
//            double b = target.Coefficients[1];

//            //var planeNormal = new Vector3((float)a, (float)b, -1).Normalize(); // Follows from the relationship between the normal vector and the equation of a plane.
//            var planeNormal = new Vector3();
//            planeNormal = planeNormal.SetComponent(inputIndex1, (float)a);
//            planeNormal = planeNormal.SetComponent(inputIndex2, (float)b);
//            planeNormal = planeNormal.SetComponent(outputIndex, -1);
//            planeNormal = planeNormal.Normalize();

//            Log.Debug("planeNormal", $"With {outputIndex} in terms of {inputIndex1} & {inputIndex2}, Vnormal = {planeNormal:f3}, error = {error:f3}");

//            var maxDot = new Vector3[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ }
//                    .OrderByDescending(v => Math.Abs(v.Dot(planeNormal)))
//                    .First();
//            if (maxDot.Dot(planeNormal) < 0) planeNormal = -planeNormal;
//            return planeNormal;
            
//        }

//        public async Task<Vector2[]> GetPlanarizedData(bool replanarize = false, bool forcePlaneVertical = true)
//        {
//            if (replanarize || PlaneNormal == Vector3.Zero)
//            {
//                PlaneNormal = await FindBestFitPlane(data, 0, 1, 2);
//                await FindBestFitPlane(data, 0, 2, 1);
//                await FindBestFitPlane(data, 1, 2, 0);
//            }

//            if (forcePlaneVertical) return data.Select(v => v.GetInPlaneZ(PlaneNormal)).ToArray();
//            else return data.Select(v => v.GetInPlane2(PlaneNormal)).ToArray();
//        }

//        public async Task<Vector2[]> GetRawXYData()
//        {
//            return data.Select(v => new Vector2(v.Vector.X, v.Vector.Y)).ToArray();
//        }

//        public struct AxisSeparatedVector
//        {
//            public Vector3 Vector;
//            public Vector3 Gravity;
//            public TimeSpan Interval;

//            // This version of the math expresses a vector as a component along the z-axis (world space)
//            // and along an axis defined by the primary x-y vector of the sequence.  In essence, it
//            // takes the gesture's plane and rotates it around an axis of constant Z until it is fully upright.
//            public Vector2 GetInPlaneZ(Vector3 Vnormal)
//            {
//                var Vz = (Vector3.Dot(Vector, Gravity) * Gravity / Gravity.LengthSquared());
//                var Vxy = Vector - Vz;
//                var Voop = (Vector3.Dot(Vxy, Vnormal) * Vnormal / Vnormal.LengthSquared());
//                var Vip = Vxy - Voop;
//                return new Vector2(Vip.Length(), Vz.Length());
//            }

//            // This version of the math expresses the vector using basis vectors which define the primary
//            // plane of the sequence
//            public Vector2 GetInPlane2(Vector3 Vnormal)
//            {
//                var Nz = (Vector3.Dot(Vnormal, Gravity) * Gravity / Gravity.LengthSquared());
//                var Vz_ip = -1 * (Vnormal - Nz).Normalize();
//                var Vperp_ip = Vector3.Cross(Vz_ip, Vnormal.Normalize());
//                return new Vector2(Vector3.Dot(Vector, Vz_ip), Vector3.Dot(Vector, Vperp_ip));
//            }
//        }

//        public struct AugVector2
//        {
//            public Vector2 Vector;
//            public TimeSpan Interval;

//            public AugVector2(float Vx, float Vz, TimeSpan t)
//            {
//                Vector = new Vector2(Vx, Vz);
//                Interval = t;
//            }
//            public AugVector2(Vector2 vector, TimeSpan t)
//            {
//                Vector = vector;
//                Interval = t;
//            }
//        }
//    }
//}