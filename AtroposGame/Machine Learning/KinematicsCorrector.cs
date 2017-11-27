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
using MiscUtil;

namespace com.Atropos.Machine_Learning
{
    public struct VectorAndTime<T> where T : struct
    {
        public T V;
        public TimeSpan t;

        private static Type[] legitTypes = new Type[] { typeof(Vector3), typeof(Vector2) };
        public VectorAndTime(T? V = null, TimeSpan? t = null)
        {
            if (!legitTypes.Contains(typeof(T))) throw new TypeInitializationException("Atropos.VectorAndTime<T>", new Exception($"Cannot construct a VectorAndTime<{typeof(T).Name}>... T must be either Vector3 or Vector2."));
            this.V = V ?? default(T);
            this.t = t ?? TimeSpan.Zero;
        }
        public static implicit operator T(VectorAndTime<T> vAt)
        {
            return vAt.V;
        }
    }

    class KinematicsCorrector<T> where T:struct // T is actually going to be limited to Vector2 and Vector3, but there's no way to get into that.
    {
        private static Type[] legitTypes = new Type[] { typeof(Vector3), typeof(Vector2) };
        static KinematicsCorrector()
        {
            if (!legitTypes.Contains(typeof(T))) throw new TypeInitializationException("Atropos.KinematicsCorrector<T>", new Exception($"Cannot construct a KinematicsCorrector<{typeof(T).Name}>... T must be either Vector3 or Vector2."));
        }

        private List<VectorAndTime<T>> rawAs = new List<VectorAndTime<T>>();
        private List<VectorAndTime<T>> rawVs = new List<VectorAndTime<T>>();
        private List<VectorAndTime<T>> rawDs = new List<VectorAndTime<T>>();

        public KinematicsCorrector()
        {

        }

        public KinematicsCorrector(IEnumerable<VectorAndTime<T>> inputAccels) : this()
        {
            foreach (VectorAndTime<T> accel in inputAccels) Add(accel);
        }

        public KinematicsCorrector(IEnumerable<T> inputAccels, IEnumerable<TimeSpan> intervals)
            : this( inputAccels.Zip(intervals, (a, t) => new VectorAndTime<T>(a, t)) ) { }

        //public Func<T, T> MappingFunction;
        //public double? AccelSmoothingWindow, VelocitySmoothingWindow, DisplacementSmoothingWindow;

        public bool IsCorrected { get; set; } = false;
        private List<VectorAndTime<T>> correctedAs = new List<VectorAndTime<T>>();
        private List<VectorAndTime<T>> correctedVs = new List<VectorAndTime<T>>();
        private List<VectorAndTime<T>> correctedDs = new List<VectorAndTime<T>>();

        public List<T> Accelerations { get { return (IsCorrected) ? correctedAs.ConvertAll(v => v.V) : rawAs.ConvertAll(v => v.V); } }
        public List<T> Velocities { get { return (IsCorrected) ? correctedVs.ConvertAll(v => v.V) : rawVs.ConvertAll(v => v.V); } }
        public List<T> Displacements { get { return (IsCorrected) ? correctedDs.ConvertAll(v => v.V) : rawDs.ConvertAll(v => v.V); } }

        private TimeSpan runTimeTotal = TimeSpan.Zero;
        private List<TimeSpan> runTimes = new List<TimeSpan>();
        public List<TimeSpan> RunTimes { get { return runTimes; } }
        public List<TimeSpan> Intervals { get { return rawAs.ConvertAll(v => v.t); } }

        public void Add(T acceleration, TimeSpan interval)
        {
            Add(new VectorAndTime<T>(acceleration, interval));
        }
        public void Add(VectorAndTime<T> accel)
        {
            // rawAs.Add(new VectorAndTime<T>(MappingFunction?.Invoke(acceleration) ?? acceleration, interval));
            rawAs.Add(accel);
            runTimeTotal += accel.t;
            runTimes.Add(runTimeTotal);

            //if (IsCalculated)
            //{

            //}
        }

        private T initialVelocity = default(T);
        private List<VectorAndTime<T>> velocityHints;
        private double? totalDistanceHint;
        private VectorAndTime<T>? totalDisplacementHint;
        private List<VectorAndTime<T>> displacementHints;
        private double hintToleranceFactor = 0.5;
        private double? nudgeForceStrength;

        private T nudge (VectorLike<T> inputVal, VectorLike<T> targetVal, double? sigma = null, double strength = 1.0)
        {
            VectorLike<T> nudgeVector = targetVal - inputVal;
            var Sigma = sigma ?? (Math.Max(targetVal.Length, inputVal.Length) * hintToleranceFactor);
            var zScore = nudgeVector.Length / Sigma;
            zScore = (zScore < -hintToleranceFactor) ? zScore + hintToleranceFactor
                : (zScore > hintToleranceFactor) ? zScore - hintToleranceFactor
                : 0.0;

            var amount = strength * Math.Min(1.0, zScore);
            return (T)(inputVal + amount * nudgeVector);
        }

        public void CalculateRaw()
        {
            VectorLike<T> V = (VectorLike<T>)initialVelocity;
            VectorLike<T> D = default(VectorLike<T>);
            //TimeSpan T = TimeSpan.Zero;
            foreach (VectorAndTime<T> A in rawAs.Skip(1))
            {
                V += (VectorLike<T>)(A.V) * (float)A.t.TotalSeconds;
                rawVs.Add(new VectorAndTime<T>((T)V, A.t));
                D += V * (float)A.t.TotalSeconds;
                rawDs.Add(new VectorAndTime<T>((T)D, A.t));
                //T += A.t;
            }
        }
    }
}