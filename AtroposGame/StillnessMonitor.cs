#define DEBUG


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
using System.Threading.Tasks;
using System.Threading;
using System.Numerics;
using Nito.AsyncEx;
using Android.Util;
using Atropos.DataStructures;

namespace Atropos
{
    public class StillnessProvider : MultiSensorProvider<float>
    {
        #region Default Constants
        public const float defaultAccelBaseline = 0.375f; // Informed guesses, for now.
        public const float defaultGyroBaseline = 0.075f;
        protected const float EPSILON = (float)1e-6;
        #endregion

        #region Backing Fields
        protected float gyroBaseline, accelBaseline, gyroWt, pGain, iGain, dGain, maxRangeMult, exponentialSteepness;
        protected IVector3Provider GyroProvider, AccelProvider;
        private TimeSpan minimumRunTime = TimeSpan.FromSeconds(1.0);

        protected float pointsDecayRate;
        protected ScoreTracker scoreTracker = ScoreTracker.NullTracker;

        public DateTime LastScreenUpdate = DateTime.Now;
        protected TimeSpan UpdateInterval;
        protected float maxStillness = float.PositiveInfinity, minStillness = float.NegativeInfinity;
        protected IRelayMessages currentActivity;
        #endregion

        #region Properties
        public float GyroBaseline { get { return gyroBaseline; } set { gyroBaseline = value; } }
        public float AccelBaseline { get { return accelBaseline; } set { accelBaseline = value; } }
        public float GyroWeight { get { return gyroWt; } set { gyroWt = value; } }
        public float MaxRangeMultiplier { get { return maxRangeMult; } set { maxRangeMult = value; } }

        public float StillnessScore { get { return (RunTime < minimumRunTime) ? 0f : scoreTracker.OutValue; } }
        public float InstantaneousScore { get { return (RunTime < minimumRunTime) ? 0f : scoreTracker.InstantaneousValue; } }
        protected override float toImplicitType()
        {
            return StillnessScore;
        }
        public ScoreTracker Tracker { get { return scoreTracker; } }
        #endregion

        /// <summary>
        /// Fuses acceleration and gyroscope data into a single scalar value representing how "stationary" the player is holding
        /// their device.  The resulting value (obtainable as (float)myStillessProvider, myStillnessProvider.Data, or
        /// myStillnessProvider.StillnessScore, whichever makes more sense) ranges between -20 and +20, with the highest values
        /// requiring superhuman steadiness, and the lowest ones indicating waving the device around with no attempt at keeping
        /// it stationary at all.
        /// </summary>
        /// <param name="accelbaseline">>The total magnitude of an acceleration vector considered "still *enough*".  Optional, 
        /// defaults to 0.25 m/s^2. The ratio of measured value compared to this quantity, rather than the absolute measured 
        /// value, is used as our indicator - recommend calibrating!</param>
        /// <param name="gyrobaseline">The total magnitude of a gyro vector considered "still *enough*".  Optional, 
        /// defaults to 0.05 radians per sec. The ratio of measured value compared to this quantity, rather than the absolute 
        /// measured value, is used as our indicator - recommend calibrating!</param>
        /// <param name="gyroweight">(Optional, default 0.5) The weighting given to the gyro values as compared to those
        /// from the accelerometer, after normalizing each of them by its respective Baseline value.</param>
        /// <param name="maxrangemultiplier">(Optional, default 5.0) How big a range the sensor "gauge" should have,
        /// as a multiple of the baseline value for that quantity.  In a speed, if baseline were (say) 30kph, 
        /// the default 5x here would mean that speeds over 150kph would all be handled the same - just as they pretty much
        /// would by the passengers!</param>
        /// <param name="externalToken">A cancellation token to stop the whole shebang.  If canceled, will require
        /// recreating this object anew.  Calls Stop() but has no built-in way to trigger Start() again - so don't
        /// use it for that purpose, use Start()/Stop()!</param>
        /// 
        public StillnessProvider(float scoreDecayRate = 0.25f, float? accelbaseline = null, float? gyrobaseline = null, 
            float gyroweight = 0.667f, float maxrangemultiplier = 5.0f, CancellationToken? externalToken = null) 
                : base(externalToken ?? CancellationToken.None,
                  new CorrectedAccelerometerProvider(SensorType.LinearAcceleration),
                  //new Vector3Provider(SensorType.LinearAcceleration),
                  new Vector3Provider(SensorType.Gyroscope))
        {
            accelBaseline = accelbaseline ?? defaultAccelBaseline;
            gyroBaseline = gyrobaseline ?? defaultGyroBaseline;
            gyroWt = gyroweight;
            maxRangeMult = maxrangemultiplier;
            pointsDecayRate = scoreDecayRate;

            //AccelProvider = new CorrectedAccelerometerProvider(SensorType.LinearAcceleration);
            //GyroProvider = new Vector3Provider(SensorType.Gyroscope);
            AccelProvider = providers[0] as CorrectedAccelerometerProvider;
            //AccelProvider = providers[0] as Vector3Provider;
            GyroProvider = providers[1] as Vector3Provider;
            
            Activate();
        }

        public override void Activate(CancellationToken? token = null)
        {
            base.Activate(token);
            scoreTracker = (scoreTracker is ScoreTracker.NullTrackerClass) 
                ? new StillnessTracker(TimeSpan.FromSeconds(0.25), accelBaseline, gyroBaseline, gyroWt) : scoreTracker;
        }
        
        public override void Deactivate()
        {
            base.Deactivate();
            scoreTracker = ScoreTracker.NullTracker;
        }

        protected override void DoWhenAllDataIsReady()
        {
            //if (!(AccelProvider?.IsActive ?? false) || !(GyroProvider?.IsActive ?? false))
            //{
            //    //AccelProvider?.Start();
            //    //GyroProvider?.Start();
            //    return;
            //    //Log.Error("StillnessMonitor", "Uh-oh!");
            //}

            //monitorPID.Compute(AccelProvider.Vector, GyroProvider.Vector);
            scoreTracker.Compute(Datapoint.From(AccelProvider.Vector, GyroProvider.Vector));
        }

        public void Jostle(double amount)
        {
            if (amount <= 0) return;
            scoreTracker.Score.Update((scoreTracker.Score.Average - (float)amount).Clamp(-20,20));
        }

        // Note - if wanting just a quick "hmm, is it?" use OutValue instead.  This one forces an immediate recalculation so that the value used is truly current.
        // NOTE TWO - This /would/ let us implement IComparable<float> which in some ways might be convenient, but in other ways misleading; avoided due to side-effects.  
        public virtual int? CompareTo(float queryValue)
        {
            if (RunTime < minimumRunTime) return null;

            float currentValue = scoreTracker.Compute(Datapoint.From(AccelProvider.Vector, GyroProvider.Vector));

            if (currentValue < queryValue - EPSILON) return -1;
            else if (currentValue > queryValue + EPSILON) return 1;
            else return 0;
        }

        public bool ReadsLessThan(float q) { return (CompareTo(q) < 0); }
        public bool ReadsMoreThan(float q) { return (CompareTo(q) > 0); }
        public bool ReadsNearEqualTo(float q) { return (CompareTo(q) == 0); }
        public bool ReadsNotEqualTo(float q) { return (CompareTo(q) != 0); }
        public bool ReadsBetween(float q1, double q2) { return ( (CompareTo((float)Math.Min(q1,q2)) > 0) && (StillnessScore < Math.Max(q1,q2) - EPSILON) ); } // Avoiding double-tapping of side effects by calling CompareTo twice.

        #region Display functionality for development purposes
        public void StartDisplayLoop(IRelayMessages current, float millisecondsDelay = 250)
        {
            currentActivity = current;
            UpdateInterval = TimeSpan.FromMilliseconds(millisecondsDelay);
            LastScreenUpdate = DateTime.Now - UpdateInterval - UpdateInterval; // Makes sure it'll fire immediately.
        }
        public bool IsItDisplayUpdateTime()
        {
            maxStillness = Math.Min(maxStillness, StillnessScore);
            minStillness = Math.Max(minStillness, StillnessScore);
            return (UpdateInterval != null && DateTime.Now - LastScreenUpdate > UpdateInterval);
        }
        public void DoDisplayUpdate()
        {
            currentActivity.RelayMessage($"Current steadiness: {StillnessScore:f3}\nRanging from {minStillness:f3} to {maxStillness:f3}.");
            LastScreenUpdate = DateTime.Now;
            maxStillness = float.PositiveInfinity;
            minStillness = float.NegativeInfinity;
        } 
        #endregion

        public class StillnessTracker : ScoreTracker
        {
            protected float gyroBaseline;
            protected float accelBaseline;
            protected float gyroWeight;
            protected float accelWeight;

            public StillnessTracker(TimeSpan halfLife, float accelbaseline, float gyrobaseline, float gyroWt = 0.5f, float initialScore = -5f)
                : base(halfLife)
            {
                accelBaseline = accelbaseline;
                gyroBaseline = gyrobaseline;
                gyroWeight = gyroWt;
                accelWeight = 1f - gyroWeight;
            }

            protected override float MultiplesOfBaseline<T>(T data)
            {
                //if (!(data is Datapoint<Vector3, Vector3>)) throw new ArgumentException($"Cannot process a {data.GetType().Name} in ScoreTrackerAccelAndGyro!");

                var d = Datapoint.From<Vector3, Vector3>(data);
                return accelWeight * (d.Value1.Length() / accelBaseline) + gyroWeight * (d.Value2.Length() / gyroBaseline);
            }
        }
    }

    public class StillnessAndOrientationProvider : StillnessProvider, IOrientationProvider
    {
        protected IOrientationProvider OrientationProvider;

        public StillnessAndOrientationProvider(IOrientationProvider orientationprovider = null, float scoreDecayRate = 0.35f, float? accelbaseline = null, float? gyrobaseline = null,
            float gyroweight = 0.667f, float maxrangemultiplier = 5.0f, CancellationToken? externalToken = null)
            : base(scoreDecayRate, accelbaseline, gyrobaseline, gyroweight, maxrangemultiplier, externalToken)
        {
            OrientationProvider = orientationprovider ?? new OrientationSensorProvider(SensorType.RotationVector, StopToken);
            AddProvider(OrientationProvider);
        }

        public StillnessAndOrientationProvider(SensorType orientationSensorType = SensorType.RotationVector, float scoreDecayRate = 0.35f, float? accelbaseline = null, float? gyrobaseline = null,
            float gyroweight = 0.667f, float maxrangemultiplier = 5.0f, CancellationToken? externalToken = null)
            : this(new OrientationSensorProvider(orientationSensorType), scoreDecayRate, accelbaseline, gyrobaseline, gyroweight, maxrangemultiplier, externalToken)
        { }

        public virtual Quaternion Quaternion { get { return OrientationProvider.Quaternion; } }
        public static implicit operator Quaternion(StillnessAndOrientationProvider source)
        {
            return source.Quaternion;
        }
    }

    public class ShakingMonitor : ConditionStage<Vector3>
    {
        public Vector3 LastAccel = Vector3.Zero;

        private TimeSpan WithinTime, MinTime;
        public Queue<DateTime> ReversalTimestamps = new Queue<DateTime>();

        public int DebugCounter = 0;

        public ShakingMonitor(int numShakes = 5, TimeSpan withinTime = default(TimeSpan), TimeSpan minTime = default(TimeSpan)) : 
            base(new Vector3Provider(SensorType.LinearAcceleration) { Delay = SensorDelay.Fastest },
                (ths, v, t) =>
                {
                    var _this = ths as ShakingMonitor;

                    // Trim extraneous shakes (ones too old to count)
                    while (_this.ReversalTimestamps.Count > 0 && DateTime.Now - _this.ReversalTimestamps.First() > _this.WithinTime)
                        _this.ReversalTimestamps.Dequeue();

                    // Get the dot product of the previous read and the current one.
                    var dot = _this.LastAccel.Dot(v);
                    _this.LastAccel = v;

                    // Does this count as a "shake"?  If so, record it.
                    // Since normal use produces accels in the 1-4 m/s2 range, a dot product of -1 implies a fairly violent course correction within that 20ms.  Doable, but not trivial.
                    if (dot < -1) _this.ReversalTimestamps.Enqueue(DateTime.Now);

                    var count = _this.ReversalTimestamps.Count;
                    if (count == 0 && _this.DebugCounter > 0) { _this.DebugCounter = 0; Log.Debug("Shaking", "Resetting counter to zero."); }
                    else if (count > _this.DebugCounter) { _this.DebugCounter = count; Log.Debug("Shaking", $"One shake detected... tally is {count} over {(DateTime.Now - _this.ReversalTimestamps.First()).TotalMilliseconds:f0}ms."); }


                    // Okay, so... do we have enough shakes within the time allotted?
                    return _this.ReversalTimestamps.Count >= numShakes
                         && DateTime.Now - _this.ReversalTimestamps.First() > _this.MinTime;
                })
        {
            WithinTime = (withinTime == default(TimeSpan)) ? TimeSpan.FromMilliseconds(750) : withinTime;
            MinTime = (minTime == default(TimeSpan)) ? TimeSpan.FromMilliseconds(250) : minTime;
        }
    }

    //public class YawCorrectingStillnessAndOrientationProvider : StillnessAndOrientationProvider
    //{
    //    protected Quaternion yawCorrection;
    //    protected AdvancedRollingAverageFloat YawAngleCorrection = new AdvancedRollingAverageFloat();
    //    protected Quaternion GetYawCorrection(float angle)
    //    {
    //        angle *= QuaternionExtensions.degToRad;
    //        var ret = new Quaternion(Vector3.UnitZ * (float)Math.Sin(angle), (float)Math.Cos(angle));
    //        return ret;
    //    }

    //    protected Quaternion currentTarget;
    //    protected float[] angleScatter = new float[] { -5f, 5f, -2f, 2f, -1f, 1f, -0.75f, 0.75f, -0.4f, 0.4f, 0f };
    //    protected Tuple<float,Quaternion>[] precalcScatterRotations;

    //    protected float currentAngleToTarget = float.NaN;
    //    protected bool needsUpdating = true;

    //    public YawCorrectingStillnessAndOrientationProvider(SensorType orientationSensorType = SensorType.RotationVector, float? accelbaseline = null, float? gyrobaseline = null,
    //        float gyroweight = 0.667f, float maxrangemultiplier = 5.0f, CancellationToken? externalToken = null)
    //        : base(new FrameShiftedOrientationProvider(orientationSensorType, token: externalToken), accelbaseline, gyrobaseline, gyroweight, maxrangemultiplier, externalToken)
    //    {
    //        precalcScatterRotations = angleScatter.Select(a => new Tuple<float,Quaternion>(a, GetYawCorrection(a).Inverse())).ToArray();
    //    }

    //    protected override void DoWhenAllDataIsReady()
    //    {
    //        base.DoWhenAllDataIsReady();
    //        needsUpdating = true;
    //    }

    //    public Quaternion Target { get { return currentTarget; } set { currentTarget = value; needsUpdating = true; } }
    //    public float AngleToTarget
    //    {
    //        get
    //        {
    //            if (needsUpdating && StillnessScore > 5f) MinimizeAngleToTarget(); // Five means that we're at least slow-moving; no point in updating the correction factors else.
    //            if (float.IsNaN(currentAngleToTarget)) return base.Quaternion.AngleTo(Target); // Just use the "naive" method until it's been corrected.
    //            return currentAngleToTarget * QuaternionExtensions.radToDeg;
    //        }
    //    }
    //    public float GetAngleToTarget(Quaternion? hypotheticalFrameShift = null)
    //    {
    //        // What's uncertain here is what our "real" yaw value ought to be.  The various constant scatter-angle
    //        // quaternions we precalculated are possible reference frames in which we might read the sensor quaternion;
    //        // here we get the angle between our current orientation [in that frame] and the target.
    //        return currentTarget.AngleTo(OrientationProvider.Quaternion * (hypotheticalFrameShift ?? Quaternion.Identity));
    //    }
    //    public void MinimizeAngleToTarget()
    //    {
    //        var minimumAngle = float.PositiveInfinity;
    //        float shiftAngleForMinimumAngleToTarget = 0.0f;
    //        foreach (var tup in precalcScatterRotations)
    //        {
    //            currentAngleToTarget = GetAngleToTarget(tup.Item2);
    //            if (currentAngleToTarget < minimumAngle)
    //            {
    //                minimumAngle = currentAngleToTarget;
    //                shiftAngleForMinimumAngleToTarget = tup.Item1;
    //            }
    //        }

    //        YawAngleCorrection.Update(shiftAngleForMinimumAngleToTarget);
    //        yawCorrection = GetYawCorrection(YawAngleCorrection.Average);
    //        currentAngleToTarget = GetAngleToTarget(yawCorrection);

    //        needsUpdating = false;
    //    }

    //    public override Quaternion Quaternion
    //    {
    //        get
    //        {
    //            if (needsUpdating && StillnessScore > 5f) MinimizeAngleToTarget(); // Five means that we're at least slow-moving; no point in updating the correction factors else.
    //            return OrientationProvider.Quaternion * yawCorrection;
    //        }
    //    }
    //}
}