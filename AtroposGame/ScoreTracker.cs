//
using System;
using System.Threading;
using System.Threading.Tasks;
using MiscUtil;
using Android.Util;
using Nito.AsyncEx;
using System.Numerics;
using Atropos.DataStructures;

namespace Atropos
{
    public abstract class ScoreTracker
    {
        protected TimeSpan halfLife;
        protected float depreciationCoefficient;
        protected TimeSpan expectedInterval, expectedIntervalMin, expectedIntervalMax;
        public bool CheckIntervals = false;

        // Scores are all expressed in "points" in some defined range of values.
        public RollingAverage<float> Score { get; protected set; }
        protected float currentPoints, minimumScore, maximumScore;

        private System.Diagnostics.Stopwatch stopWatch;

        //public static ScoreTrackerStillness NullTracker = new ScoreTracker(TimeSpan.MaxValue, 1e6f, 1e6f);

        public ScoreTracker(TimeSpan halfLife, float initialScore = -5f, float minScore = -20f, float maxScore = 20f)
        {
            this.halfLife = halfLife;
            minimumScore = minScore;
            maximumScore = maxScore;

            SetUpTiming(TimeSpan.FromMilliseconds(20), initialScore); // 20 ms - equivalent to SensorDelay.Game
            currentPoints = 0.0f;
        }
        public void SetUpTiming(TimeSpan interval, float initialAverage, bool isAdjustmentAfterStart = false)
        {
            expectedInterval = interval;
            expectedIntervalMin = TimeSpan.FromSeconds(interval.TotalSeconds * 0.85);
            expectedIntervalMax = TimeSpan.FromSeconds(interval.TotalSeconds * 1.15);
            Score = new RollingAverage<float>((float)(halfLife.TotalSeconds / interval.TotalSeconds), initialAverage, !isAdjustmentAfterStart);
            depreciationCoefficient = (float)Math.Pow(0.5, interval.TotalSeconds / halfLife.TotalSeconds);
        }

        public float OutValue { get { return Score.Average; } }
        public float InstantaneousValue { get { return (currentPoints * 10f).Clamp(-20f, 20f); } }
        public float OutRating { get { return (Score.Average - minimumScore) / (maximumScore - minimumScore); } } // Maps it into (0,1)
        public float Compute() { return OutValue; }

        protected abstract float MultiplesOfBaseline<T>(T data) where T : IDatapoint;

        protected virtual float PointsValue(float factorSum) // Range of input: zero to around 5.  Should return values between about +2 (though usually more like +1 for realistic scenarios) and -2.
        {
            // Note - various functions I've played around with.
            //return 1.0f - factorSum;
            return 0.1f - (float)Math.Log(factorSum + 0.1);
            //return -1.3f * (float)Math.Log(0.65*factorSum + 0.1 + 0.1/(x + 0.2));
            //return -(float)Math.Tanh(factorSum - 1.25);
        }

        public virtual float Compute<T>(T data) where T : IDatapoint
        {
            // Step one: as a weighted average, how large are those vectors (as multiples of their respective baseline vector lengths)?
            var factorSum = MultiplesOfBaseline<T>(data);

            // Step 1.5: Make it a little easier to get back out of the depths of a bad overall score.
            if (Score.Average < 0) factorSum *= (float)Math.Exp(-Score.Average / minimumScore); // So at a current score of -20, your shakiness is effectively cut to a third of its value.

            // Step two: Translate those vector magnitudes into a score modifier ("this round" points)
            currentPoints = PointsValue(factorSum);

            // Step three: Erode the existing score at our designated rate, then add this round's points to that.
            var insideTerm = (currentPoints + depreciationCoefficient * Score.Average).Clamp(minimumScore, maximumScore);
            Score.Update(insideTerm);

            #region If the intervals are very different from expected, try to compensate
            if (CheckIntervals)
            {
                if (stopWatch == null)
                {
                    stopWatch = new System.Diagnostics.Stopwatch();
                    stopWatch.Start();
                }
                else
                {
                    var elapsed = stopWatch.Elapsed;
                    if (elapsed < expectedIntervalMin || elapsed > expectedIntervalMax) // Outside our comfort zone - readjust!
                    {
                        SetUpTiming(elapsed, Score.Average, true);
                    }
                    stopWatch.Restart();
                } 
            }
            #endregion

            //if (accelVector == Vector3.UnitX) return float.NaN; // Is the initial value of the vector provider, but I don't think we need to check for it anymore.
            return Score.Average;
        }
        public class NullTrackerClass : ScoreTracker
        {
            public NullTrackerClass() : base(TimeSpan.FromSeconds(1), 0f) { }
            override protected float MultiplesOfBaseline<T>(T data) { return 0f; }
            public override float Compute<T>(T data) { return 0f; }
        }
        public static ScoreTracker NullTracker = new NullTrackerClass();
    }
}
