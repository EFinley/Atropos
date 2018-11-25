using MiscUtil;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using static System.Math;
using System.Collections;
using System.Linq;
using Atropos.DataStructures;

namespace Atropos
{
    public interface IAverage<T>
    {
        T Average { get; }
        int NumPoints { get; }
        void Update(T newValue);
    }

    public interface IAdvancedAverage<T> : IAverage<T>
    {
        float StdDev { get; }
        float Sigma { get; }
        float RelativeStdDev { get; }
        float RootMeanSquare { get; }
    }

    // First, the simplest kind - an IAverage<T> which simply keeps track of the normal add-all-together-and-divide-by-n average of
    // a series of quantities.  But does so using the interface we'll also use for fancier averages later.
    [Serializable]
    public class SimpleAverage<T> : IAverage<T> where T : struct
    {
        protected T currentAverage, lastAverage, defaultAverage;
        protected T currentValue, lastValue;

        protected virtual float Alpha { get { return 1.0f / (float)count; } } // Correct for the simple average case (it's actually a constant in the rolling/exponential-weighted case).

        protected int count;
        public int NumPoints { get { return count; } }
        protected bool WarningIssued = false;

        public virtual T Average { get { return currentAverage; } }

        public SimpleAverage(T? initialAverage = null)
        {
            count = 0;

            //setFunctions(); // Only necessary during debugging (because for some reason breakpoints inside a static ctor do not work).
            currentAverage = defaultAverage = initialAverage ?? DefaultAverage;
        }

        public virtual void Update(T newValue)
        {
            if (float.IsNaN(MagnitudeOf(newValue)))
            {
                // It's conceivable that a single NaN might get passed here during startup / stopping, so tolerate that (but warn)
                if (!WarningIssued)
                {
                    WarningIssued = true;
                    Android.Util.Log.Warn("Atropos|SimpleAverage<T>", $"SimpleAverage<{typeof(T).Name}> passed {newValue}, whose Magnitude is NaN.  Problem?");
                    return;
                }
                else throw new ArgumentException($"SimpleAverage<{typeof(T).Name}> passed {newValue}, whose Magnitude is NaN, more than once.  Time to investigate.");
            }

            count++;

            lastAverage = currentAverage;
            lastValue = currentValue;

            currentValue = newValue;
            var delta = Operator.Subtract(newValue, lastAverage);
            //currentAverage = Operator.Add(lastAverage, Operator.MultiplyAlternative(delta, Alpha));
            var v1 = Operator.MultiplyAlternative(delta, Alpha);
            var v2 = Operator.Add(lastAverage, v1);
            currentAverage = v2;
        }

        public static T DefaultAverage { get; protected set; }
        protected static Func<T, float> _magnitudeOf;
        public static float MagnitudeOf(T value)
        {
            if (_magnitudeOf == null) throw new Exception($"No MagnitudeOf function defined for {typeof(T).Name}.");
            return _magnitudeOf(value);
        }

        static SimpleAverage()
        {
            if (typeof(T) == typeof(float))
            {
                DefaultAverage = Operator.Convert<float, T>(0.0f);
                _magnitudeOf = (f) => Abs(Operator.Convert<T, float>(f));
            }
            else if (typeof(T) == typeof(double))
            {
                DefaultAverage = Operator.Convert<double, T>(0.0);
                _magnitudeOf = (f) => (float)Abs(Operator.Convert<T, double>(f));
            }
            else if (typeof(T) == typeof(Vector2))
            {
                DefaultAverage = Operator.Convert<Vector2, T>(Vector2.Zero);
                _magnitudeOf = (v2) => Operator.Convert<T, Vector2>(v2).Length();
            }
            else if (typeof(T) == typeof(Vector3))
            {
                DefaultAverage = Operator.Convert<Vector3, T>(Vector3.Zero);
                _magnitudeOf = (v3) => Operator.Convert<T, Vector3>(v3).Length();
            }
            else if (typeof(T) == typeof(Quaternion))
            {
                DefaultAverage = Operator.Convert<Quaternion, T>(Quaternion.Identity);
                _magnitudeOf = (q) => Operator.Convert<T, Quaternion>(q).Length();
            }
            else if (typeof(T).Implements<IDatapoint>())
            {
                DefaultAverage = Datapoint.DefaultOrIdentity<T>();
                _magnitudeOf = (vC) => (vC as IDatapoint).Magnitude();
            }
            else throw new NotImplementedException("Ack! Ptui!");
        }

        public static implicit operator T(SimpleAverage<T> source)
        {
            return source.Average;
        }
    }

    // NOTE - Logically, an "AdvancedSimpleAverage<T>" (sic) would exist here, implementing IAdvancedAverage (e.g. RMS,
    // standard deviation, etc).  But thus far I don't need one, so meh.

    [Serializable]
    public class RollingAverage<T> : SimpleAverage<T> where T : struct
    {
        protected float timeFrame;
        private float _alpha;
        protected override float Alpha { get { return _alpha; } }

        protected bool isValidYet; //{ get { return count >= readinessCount; } }
        protected int readinessCount;
        protected float unreadyPart { get { return (readinessCount - count) / (float)readinessCount; } }
        protected float readyPart { get { return count / (float)readinessCount; } }

        public RollingAverage(float timeFrameInPeriods = 30f, T? initialAverage = null, bool needsRampUp = true)
            : base(initialAverage)
        {
            timeFrame = timeFrameInPeriods;
            _alpha = 2.0f / (timeFrameInPeriods + 1);

            //count = 0;
            //readinessCount = (needsRampUp) ? (int)(1.5 * timeFrame) : 0; // The math tells us that the first [timeframe] account for 86%, so half again that many definitely makes us ready to assert that it's a legit average.
            readinessCount = (int)(1.5 * timeFrame); // The math tells us that the first [timeframe] account for 86%, so half again that many definitely makes us ready to assert that it's a legit average.
            //setFunctions(); // Only necessary during debugging (because for some reason breakpoints inside a static ctor do not work).
            //currentAverage = defaultAverage = initialAverage ?? DefaultAverage;
        }

        //public virtual void Update(T newValue)
        //{
        //    if (float.IsNaN(MagnitudeOf(newValue))) return;

        //    count++;
        //    isValidYet = (count > readinessCount);

        //    lastAverage = currentAverage;
        //    lastValue = currentValue;

        //    currentValue = newValue;
        //    var delta = Operator.Subtract(newValue, lastAverage);
        //    currentAverage = Operator.Add(lastAverage, Operator.MultiplyAlternative(delta, Alpha));
        //}

        public override void Update(T newValue)
        {
            base.Update(newValue);
            isValidYet = isValidYet || (count > readinessCount);
        }

        public static implicit operator T(RollingAverage<T> source)
        {
            return source.Average;
        }

        //public static T DefaultAverage { get; private set; }
        //protected static Func<T, float> _magnitudeOf;
        //public static float MagnitudeOf(T value)
        //{
        //    if (_magnitudeOf == null) throw new Exception($"No MagnitudeOf function defined for {typeof(T).Name}.");
        //    return _magnitudeOf(value);
        //}

        public override T Average
        {
            get
            {
                //if (_getAverageFunc == null) throw new Exception($"No Average function defined for {typeof(T).Name}.");
                return _getAverageFunc(this);
            }
        }

        protected static Func<RollingAverage<T>, T> _getAverageFunc;
        protected static T _defaultGetAverageFunc(RollingAverage<T> self)
        {
            return (self.isValidYet) 
                ? self.currentAverage 
                : Operator<T>.Add(Operator.MultiplyAlternative(self.currentAverage, self.readyPart), 
                                  Operator.MultiplyAlternative(self.defaultAverage, self.unreadyPart));
        }
        
        static RollingAverage()
        //void setFunctions()
        {
            _getAverageFunc = _defaultGetAverageFunc; // Will be true for all but Quaternions and other exotic types (like our Vector6)
            //if (typeof(T) == typeof(float))
            //{
            //    DefaultAverage = Operator.Convert<float, T>(0.0f);
            //    _magnitudeOf = (f) => Abs(Operator.Convert<T, float>(f));
            //}
            //else if (typeof(T) == typeof(Vector2))
            //{
            //    DefaultAverage = Operator.Convert<Vector2, T>(Vector2.Zero);
            //    _magnitudeOf = (v2) => Operator.Convert<T, Vector2>(v2).Length();
            //}
            //else if (typeof(T) == typeof(Vector3))
            //{
            //    DefaultAverage = Operator.Convert<Vector3, T>(Vector3.Zero);
            //    _magnitudeOf = (v3) => Operator.Convert<T, Vector3>(v3).Length();
            //}
            //else if (typeof(T) == typeof(Quaternion))
            if (typeof(T) == typeof(Quaternion))
            {
                //DefaultAverage = Operator.Convert<Quaternion, T>(Quaternion.Identity);
                //_magnitudeOf = (q) => Operator.Convert<T, Quaternion>(q).Length();
                _getAverageFunc = (RollingAverage<T> self) =>
                {
                    return (self.isValidYet)
                    ? self.currentAverage
                    : Operator.Convert<Quaternion, T>
                        (Quaternion.Slerp(Operator.Convert<T, Quaternion>(DefaultAverage),
                                          Operator.Convert<T, Quaternion>(self.currentAverage), 
                                          self.readyPart));
                };
            }
            //else if (typeof(T).Implements<IDatapoint>())
            //{
            //    DefaultAverage = Datapoint.DefaultOrIdentity<T>();
            //    _magnitudeOf = (vC) => (vC as IDatapoint).Magnitude();
            //}
            else if (_magnitudeOf == null) throw new NotImplementedException("Ack! Ptui! Some more."); // Denotes that it didn't have a valid SimpleAverage<T> underneath.
        }
    }

    [Serializable]
    public abstract class AdvancedRollingAverage<T> : RollingAverage<T>, IAdvancedAverage<T> where T: struct
    {
        protected float currentStdDev, lastStdDev, defaultStdDev;
        protected float currentRMS, lastRMS, defaultRMS;

        public virtual float StdDev { get { return (isValidYet) ? currentStdDev : currentStdDev * readyPart + defaultStdDev * unreadyPart; } }
        public virtual float Sigma { get { return StdDev; } } // Just an alias, plain and simple.  Compiler will take care of the "extra level" of nesting, as if it mattered anyway.
        public virtual float RelativeStdDev { get { return StdDev / MagnitudeOf(Average); } }  // Probably a hack, but whatever.
        public virtual float RootMeanSquare { get { return (isValidYet) ? currentRMS : currentRMS * readyPart + defaultRMS * unreadyPart; } }

        //public Func<T, T, float> CorrelationFunction;

        protected AdvancedRollingAverage(float timeFrameInPeriods = 30f, T? initialAverage = null, float initialRelativeStdDev = 1.0f)
            : base(timeFrameInPeriods, initialAverage)
        {
            SetInitialValues(initialAverage, initialRelativeStdDev);
            //CorrelationFunction = (v1, v2) => { return 1.0f; }; // Default to a null correlation function.
        }

        public virtual void SetInitialValues(T? defaultavg, float defaultrelativestddev)
        {
            currentStdDev = defaultStdDev = MagnitudeOf(defaultAverage) * defaultrelativestddev;
            currentRMS = defaultRMS = (float)Sqrt(SquareOf(defaultAverage) + defaultStdDev * defaultStdDev);
        }

        //TODO!?!? Finish implementing the CorrelationFunction (which skews how heavily it's weighted, based on some relationship to the existing average).
        protected static Action<T, AdvancedRollingAverage<T>> _updateFunc;
        protected static void _defaultUpdate(T newValue, AdvancedRollingAverage<T> self)
        {
            
        }

        public override void Update(T newValue)
        {
            base.Update(newValue);
            if (float.IsNaN(MagnitudeOf(newValue)))
                return;

            lastStdDev = currentStdDev;
            lastRMS = currentRMS;

            float modifiedAlpha = Alpha; // * 0.5f * (1.0f + CorrelationFunction(newValue, lastValue));

            var delta = Operator.Subtract(newValue, lastAverage);
            float diffSquares = SquareOf(delta) - lastStdDev * lastStdDev;
            currentStdDev = lastStdDev + modifiedAlpha * Sign(diffSquares) * (float)Sqrt(Abs(diffSquares));
            currentRMS = (float)Sqrt((1.0f - modifiedAlpha) * lastRMS * lastRMS + modifiedAlpha * SquareOf(newValue));
        }

        //public static implicit operator T(AdvancedRollingAverage<T> source)
        //{
        //    return source.Average;
        //}

        // Turns a Tfrom? into a Tto?, where both are nullable versions of value types.
        protected static Tto? ConvertOrNull<Tfrom, Tto>(Tfrom? source) where Tfrom : struct where Tto : struct
        {
            if (!source.HasValue) return null;
            else return Operator.Convert<Tfrom, Tto>(source.Value);
        }

        public abstract float SquareOf(T value);

        // And here's the real magic... delivering up a derived class as the appropriate parent type, based on the type arg alone.
        // Note that the Quaternion version is different enough, here, that I chose to go with this factory function version, even though the
        // simpler RollingAverage<Quaternion> gets by on simply the static-constructor trickery above.  Lets me use virtual/override, rather than
        // a whole raft of private delegate functions for everything that the bloody Quaternion version needs to override.
        public static AdvancedRollingAverage<Tnew> Create<Tnew>(float timeFrameInPeriods, Tnew? initialAverage = null, float initialRelativeStdDev = 1.0f) where Tnew : struct
        { 
            if (typeof(Tnew) == typeof(float))
            {
                return new AdvancedRollingAverageFloat(timeFrameInPeriods, ConvertOrNull<Tnew, float>(initialAverage), initialRelativeStdDev)
                    as AdvancedRollingAverage<Tnew>;
            }
            else if (typeof(T) == typeof(Vector2))
            {
                return new AdvancedRollingAverageVector2(timeFrameInPeriods, ConvertOrNull<Tnew, Vector2>(initialAverage), initialRelativeStdDev)
                    as AdvancedRollingAverage<Tnew>;
            }
            else if (typeof(T) == typeof(Vector3))
            {
                return new AdvancedRollingAverageVector3(timeFrameInPeriods, ConvertOrNull<Tnew, Vector3>(initialAverage), initialRelativeStdDev)
                    as AdvancedRollingAverage<Tnew>;
            }
            else if (typeof(T) == typeof(Quaternion))
            {
                return new AdvancedRollingAverageQuat(timeFrameInPeriods, ConvertOrNull<Tnew, Quaternion>(initialAverage), initialRelativeStdDev)
                    as AdvancedRollingAverage<Tnew>;
            }
            else throw new NotImplementedException("Ack! Ptui!");
        }
    }

    [Serializable]
    public class AdvancedRollingAverageFloat : AdvancedRollingAverage<float>
    {
        public AdvancedRollingAverageFloat(float timeFrameInPeriods = 30f, float? initialAverage = null, float initialRelativeStdDev = 1.0f)
            : base(timeFrameInPeriods, initialAverage, initialRelativeStdDev) { }
        public override float SquareOf(float value) { return value * value; }
    }

    [Serializable]
    public class AdvancedRollingAverageVector3 : AdvancedRollingAverage<Vector3>
    {
        public AdvancedRollingAverageVector3(float timeFrameInPeriods = 30f, Vector3? initialAverage = null, float initialRelativeStdDev = 1.0f)
            : base(timeFrameInPeriods, initialAverage, initialRelativeStdDev) { }
        //public override float MagnitudeOf(Vector3 value) { return value.Length(); }
        public override float SquareOf(Vector3 value) { return value.LengthSquared(); }
        //public override Vector3 DefaultAverage { get { return Vector3.Zero; } }
    }

    [Serializable]
    public class AdvancedRollingAverageVector2 : AdvancedRollingAverage<Vector2>
    {
        public AdvancedRollingAverageVector2(float timeFrameInPeriods = 30f, Vector2? initialAverage = null, float initialRelativeStdDev = 1.0f)
            : base(timeFrameInPeriods, initialAverage, initialRelativeStdDev) { }
        //public override float MagnitudeOf(Vector2 value) { return value.Length(); }
        public override float SquareOf(Vector2 value) { return value.LengthSquared(); }
        //public override Vector2 DefaultAverage { get { return Vector2.Zero; } }
    }

    [Serializable]
    public class AdvancedRollingAverageQuat : AdvancedRollingAverage<Quaternion>
    {
        protected float oneMinusAlpha;
        public AdvancedRollingAverageQuat(float timeFrameInPeriods = 30f, Quaternion? initialAverage = null, float initialRelativeStdDev = 1.0f)
            : base(timeFrameInPeriods, initialAverage, initialRelativeStdDev)
        {
            oneMinusAlpha = 1.0f - Alpha;
        }
        //public override float MagnitudeOf(Quaternion value) { return value.Length(); }
        public override float SquareOf(Quaternion value) { return value.LengthSquared(); }
        //public override Quaternion DefaultAverage { get { return Quaternion.Identity; } }

        public override Quaternion Average { get { return (isValidYet) ? currentAverage : Quaternion.Slerp(DefaultAverage, currentAverage, readyPart); } }


        // Special!  For the quaternion version only, the "relative" sigma is simply the sigma compared to a fixed 30-degree baseline,
        // since the usual definition of relative s.d. compares it to the magnitude which, for rotations, doesn't make much sense at all.
        // This also affects the initial values, since (for various reasons I won't get into in too much depth) when we create any of these, 
        // we use the relative std.dev. instead of the std. dev. itself.
        private const float baselineAngle = 30.0f;
        public override float RelativeStdDev { get { return StdDev / baselineAngle; } }
        public override void SetInitialValues(Quaternion? defaultavg, float defaultrelativestddev)
        {
            currentAverage = defaultAverage = defaultavg ?? DefaultAverage;
            currentStdDev = defaultStdDev = baselineAngle * defaultrelativestddev;
            currentRMS = defaultRMS = (float)Sqrt(SquareOf(defaultAverage) + defaultStdDev * defaultStdDev);
        }

        public override void Update(Quaternion newValue)
        {
            count++;
            //isValidYet = (count > readinessCount);

            lastAverage = currentAverage;
            lastStdDev = currentStdDev;
            lastRMS = currentRMS;
            lastValue = currentValue;

            float modifiedAlpha = Alpha; // * 0.5f * (1.0f + CorrelationFunction(newValue, lastValue));

            currentValue = newValue;

            //var delta = (newValue - lastAverage);
            //currentAverage = lastAverage + delta * modifiedAlpha;
            currentAverage = Quaternion.Slerp(lastAverage, newValue, modifiedAlpha);

            var angleToMean = currentAverage.AngleTo(newValue);
            //var currentDotMean = Quaternion.Dot(newValue, currentAverage);
            currentStdDev = lastStdDev + modifiedAlpha * (float)((angleToMean > lastStdDev)
                                                            ? Sqrt(angleToMean * angleToMean - lastStdDev * lastStdDev)
                                                            : -Sqrt(lastStdDev * lastStdDev - angleToMean * angleToMean));
            var angleToIdentity = Quaternion.Identity.AngleTo(newValue);
            //var currentDotIdentity = Quaternion.Dot(newValue, Quaternion.Identity);
            currentRMS = (float)Sqrt(oneMinusAlpha * lastRMS * lastRMS + modifiedAlpha * angleToIdentity * angleToIdentity);
        }
        
        public Vector3 ToEulerAngles() { return Average.ToEulerAngles(); }
        public float AngleTo(Quaternion other) { return Average.AngleTo(other); }
    }

    /// <summary>
    /// Utility option #1 - a list which is intrinsically smoothed using the appropriate averager (not advanced averager) from above.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class SmoothedList<T> : IList<T> where T : struct
    {
        public RollingAverage<T> Average;
        private float timeFrame;
        private T? initialAvg;
        private List<T> List = new List<T>();
        public SmoothedList(float timeFrameInPeriods = 10f, T? initialAverage = null) : base()
        {
            Average = new RollingAverage<T>(timeFrameInPeriods, initialAverage);
            timeFrame = timeFrameInPeriods;
            initialAvg = initialAverage;
        }
        public SmoothedList(List<T> sourceList, bool SmoothNow = false, float timeFrameInPeriods = 10f)
            : this(timeFrameInPeriods)
        {
            if (!SmoothNow)
            {
                Average = new RollingAverage<T>(timeFrameInPeriods, sourceList.LastOrDefault(), false);
                List = sourceList;
            }
            else
            {
                Average = new RollingAverage<T>(timeFrameInPeriods, sourceList.FirstOrDefault(), false);
                List = new List<T>() { sourceList.FirstOrDefault() };
                foreach (var pt in sourceList.Skip(1)) Add(pt);
            }
        }

        #region Implementations *not* simply passed on to the encapsulated list.
        public void Add(T newItem)
        {
            Average.Update(newItem);
            List.Add(Average.Average);
        }

        public void Clear()
        {
            ((IList<T>)List).Clear();
            Average = new RollingAverage<T>(timeFrame, initialAvg);
        }
        #endregion

        #region Implementations which are straight pass-throughs to the wrapped list.
        public T this[int index]
        {
            get
            {
                return ((IList<T>)List)[index];
            }

            set
            {
                ((IList<T>)List)[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return ((IList<T>)List).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IList<T>)List).IsReadOnly;
            }
        }



        public bool Contains(T item)
        {
            return ((IList<T>)List).Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            ((IList<T>)List).CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IList<T>)List).GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return ((IList<T>)List).IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            ((IList<T>)List).Insert(index, item);
        }

        public bool Remove(T item)
        {
            return ((IList<T>)List).Remove(item);
        }

        public void RemoveAt(int index)
        {
            ((IList<T>)List).RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<T>)List).GetEnumerator();
        }
        #endregion
    }

    /// <summary>
    /// Utility option #2 - post-facto map it through such an averaging engine, receive the results, and throw away the averaging object when done.
    /// </summary>
    //[Serializable]
    public static class SmoothedList
    {
        public static List<T> Smooth<T>(this IEnumerable<T> source, float timeFrameInPeriods = 10, T? initialAverage = null) where T : struct
        {
            var Average = new RollingAverage<T>(timeFrameInPeriods, initialAverage ?? default(T), false);
            //foreach (int i in Enumerable.Range(0, (int)(timeFrameInPeriods * 1.6))) Average.Update(source.First()); // Create a "solid base" which lets it know that the first datapoint IS the initial average, and IsValidYet should be true always.
            //return source.Select(val => { Average.Update(val); return Average.Average; }).ToList();
            var result = new List<T>();
            foreach (T elem in source)
            {
                Average.Update(elem);
                result.Add(Average.Average);
            }
            return result;
        }
    }
}