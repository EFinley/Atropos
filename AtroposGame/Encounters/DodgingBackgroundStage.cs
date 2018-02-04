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
using MiscUtil;

namespace Atropos.Encounters
{
    public class GravAxisAccelerometerProvider : MultiSensorProvider<Vector3>, IVector3Provider
    {
        protected IVector3Provider linearAccelProvider, gravProvider;
        public Vector3 Vector { get { return (linearAccelProvider.Vector.Dot(gravityDirection) * gravityDirection); } }
        public Vector3 gravityDirection { get { return gravProvider.Vector.Normalize(); } }

        public GravAxisAccelerometerProvider() : base(new Vector3Provider(SensorType.LinearAcceleration), new Vector3Provider(SensorType.Gravity))
        {
            linearAccelProvider = (IVector3Provider)providers[0];
            gravProvider = (IVector3Provider)providers[1];
        }

        protected override Vector3 toImplicitType()
        {
            return Vector;
        }
    }

    public class IncomingRangedAttack
    {
        public TimeSpan AcquisitionTime;
        public double UnmodifiedHitChance, BaseZScore;
        public double DodgeCompensationBonus;
        public IEffect AttackSFX, HitSFX, MissSFX;
        public string AttackSpeech, HitSpeech, MissSpeech;
        public event EventHandler<EventArgs<double>> OnIncomingHit;
        public event EventHandler<EventArgs<double>> OnIncomingMiss;

        public IncomingRangedAttack()
        {
            // Set a bunch of default values; override in object initializer (or elsewhere) if desired.
            AttackSFX = new Effect("Incoming.Gunshot", Resource.Raw.gunshot_4);
            HitSFX = new Effect("Incoming.Hit", Resource.Raw._44430_vocalized_ow);
            MissSFX = new Effect("Incoming.Miss", Resource.Raw._96632_ricochet_metal4);
            AttackSpeech = "";
            HitSpeech = "Fall down.  You have been shot and injured.";
            MissSpeech = "";

            AcquisitionTime = TimeSpan.FromSeconds(1.5).MultipliedBy(Res.GetRandomCoefficient(1.0, 0.25));
            UnmodifiedHitChance = 0.75; // Defined as "the chance of hitting if you don't dodge at all within the time allowed."
            BaseZScore = Accord.Math.Special.Ierf(UnmodifiedHitChance);
            DodgeCompensationBonus = 0.0; // Percentage above/below 100%, defined as "the factor by which, compared to a normal shooter, he requires a superior dodge to avoid."  Exactly what this means varies by EvasionMode.
        }

        public virtual async Task IncomingAttackHitResults(double? AimScore = null)
        {
            await HitSFX.PlayToCompletion();
            OnIncomingHit?.Invoke(this, new EventArgs<double>(AimScore ?? 0.0));
            await Task.Delay(300);
            await Speech.SayAllOf(HitSpeech);
        }

        public virtual async Task IncomingAttackMissResults(double? AimScore = null)
        {
            await MissSFX.PlayToCompletion();
            OnIncomingMiss?.Invoke(this, new EventArgs<double>(AimScore ?? 0.0));
            await Task.Delay(300);
            await Speech.SayAllOf(MissSpeech);
        }
    }

    public static class EvasionMode
    {
        //public virtual double GetAimScore(IncomingRangedAttack incoming, SmoothLoggingProvider<float> relevantFactorTrajectory)
        //{
        //    var i = relevantFactorTrajectory.Timestamps.FindIndex(t => t >= incoming.AcquisitionTime);
        //    return AssessShot(relevantFactorTrajectory.LoggedData, incoming);
        //}

        

        //#region Static factory functions used for accessing the modes set up thus far (allowing you to refer to them as EvasionMode.Duck etc. without actually seeing the definition Duck etc.)
        //public static EvasionMode<Vector3> Duck() { return new Duck(); }
        //public static EvasionMode<Vector3> Dodge() { return new Dodge(); }
        //public static EvasionMode<float> TakeCover() { return new TakeCover(); }
        //#endregion

        #region Definition of Dodge subclass
        public class Dodge : EvasionMode<Vector3>
        {
            public override string Prompt { get; } = "Dodge!";

            public virtual double DistanceForOneSigma { get; } = 0.35; // In meters.
            public virtual double LimitAccelForCue { get; } = 0.5; // In m/s^2 (the units of the accelerometer itself)

            private RollingAverage<Vector3> smoothedAccel = new RollingAverage<Vector3>(3);
            private Vector3 currentVelocity = Vector3.Zero;
            private Vector3 currentDisplacement = Vector3.Zero;

            public override double AssessShot(SmoothedList<float> distancesDodged, List<TimeSpan> timestamps, IncomingRangedAttack incoming)
            {
                var effectiveDodge = distancesDodged.Last() / DistanceForOneSigma / (1.0 + 0.01 * incoming.DodgeCompensationBonus);
                var bellCurveDieRoll = Accord.Statistics.Distributions.Univariate.NormalDistribution.Random();
                var resultScore = incoming.BaseZScore - effectiveDodge - bellCurveDieRoll;
                Log.Debug("Evasion|AssessShot", $"Resolved an evasion attempt with a {((resultScore > 0) ? "hit" : "miss")} ({resultScore:f2}) based on an EffectiveDodge of {effectiveDodge:f2} and a random dodge of {bellCurveDieRoll:f2}.");
                return resultScore;
            }

            public override float ProcessData(IProvider<Vector3> provider)
            {
                smoothedAccel.Update(provider.Data);
                currentVelocity += smoothedAccel.Average * (float)provider.Interval.TotalSeconds;
                currentDisplacement += currentVelocity * (float)provider.Interval.TotalSeconds;
                return currentDisplacement.Length();
            }
            public override IProvider<Vector3> CreateProvider(params object[] args)
            {
                return new Vector3Provider(SensorType.LinearAcceleration);
            }

            public override bool ReadyForCue(IProvider<Vector3> provider)
            {
                return provider.Data.Length() < LimitAccelForCue;
            }
        }
        #endregion

        #region Definition of Duck subclass
        public class Duck : Dodge
        {
            public override string Prompt { get; } = "Duck!";
            public override double DistanceForOneSigma { get; } = 0.3;
            public override IProvider<Vector3> CreateProvider(params object[] args)
            {
                return new GravAxisAccelerometerProvider();
            }
        }
        #endregion

        #region Definition of TakeCover subclass - TODO - NOT IMPLEMENTED YET
        private class TakeCover : EvasionMode<float>
        {
            public override string Prompt { get; } = "Take cover!";
            public override double AssessShot(SmoothedList<float> relevantFactors, List<TimeSpan> timestamps, IncomingRangedAttack incoming)
            {
                throw new NotImplementedException();
            }

            public override float ProcessData(IProvider<float> provider)
            {
                throw new NotImplementedException();
            }
            public override IProvider<float> CreateProvider(params object[] args) { return new StillnessProvider(); }

            public override bool ReadyForCue(IProvider<float> provider)
            {
                throw new NotImplementedException();
            }
        }
        #endregion
    }

    public abstract class EvasionMode<T>
    {
        public abstract string Prompt { get; }

        /// <summary>
        /// Aim Score definition: If greater than zero, indicates a hit, with magnitude indicating
        /// margin of success (under 1.0 is mediocre, over 2.0 is excellent).  If less than zero,
        /// indicates a miss, with magnitude indicating how badly they missed (under -2.0 is miserable).
        /// </summary>
        /// <param name="relevantFactors">A stored list of derived scalar "dodge values" - typically we only use the last one, but it may vary.</param>
        /// <param name="incoming">The characteristics of the incoming attack being evaded.</param>
        /// <returns>The aim score calculated for the shot.</returns>
        public abstract double AssessShot(SmoothedList<float> relevantFactors, List<TimeSpan> timestamps, IncomingRangedAttack incoming);

        public abstract IProvider<T> CreateProvider(params object[] args);
        public abstract float ProcessData(IProvider<T> provider);
        public abstract bool ReadyForCue(IProvider<T> provider);
    }

    public class IncomingAttackPrepStage<T> : GestureRecognizerStage
    {
        protected BaseActivity ParentActivity;
        protected IncomingRangedAttack Incoming;
        protected EvasionMode<T> Evasion;
        protected IProvider<T> Provider;

        public IncomingAttackPrepStage(BaseActivity parent, IncomingRangedAttack incoming, EvasionMode<T> evasionMode, params object[] args) : base("IncomingAttackPrep")
        {
            ParentActivity = parent;
            Incoming = incoming;
            Evasion = evasionMode;
            Provider = Evasion.CreateProvider(args);
            SetUpProvider(Provider, true);
            BaseActivity.BackgroundStages.Add(this); // Put into startAction() or even Activate() if the timing on this (not yet defined and all) is an issue.
        }
        protected override void startAction()
        {
            while (Evasion.ProcessData(Provider) < 1e-6) // Meaning: it's not up and running yet!
                Provider.WhenDataReady().Wait(StopToken);
        }

        protected override bool nextStageCriterion()
        {
            return Evasion.ReadyForCue(Provider);
        }

        protected override void nextStageAction()
        {
            //var nextStage = new IncomingAttackReactionStage<T>(ParentActivity, Incoming, Evasion, Provider);
            var nextStage = new IncomingAttackReactionStage<T>(ParentActivity, Incoming, Evasion, null);
            BaseActivity.BackgroundStages.Add(nextStage);
            nextStage.Activate();
            BaseActivity.BackgroundStages.Remove(this);
        }
    }

    public class IncomingAttackReactionStage<T> : GestureRecognizerStage
    {
        protected BaseActivity ParentActivity;
        protected IncomingRangedAttack Incoming;
        protected EvasionMode<T> Evasion;
        protected IProvider<T> Provider;

        protected SmoothedList<float> ProcessedData = new SmoothedList<float>(3);
        protected List<TimeSpan> Timestamps = new List<TimeSpan>();
        protected System.Diagnostics.Stopwatch Stopwatch;

        public IncomingAttackReactionStage(BaseActivity parent, IncomingRangedAttack incoming, EvasionMode<T> evasionMode, IProvider<T> provider) : base("IncomingAttackReaction")
        {
            ParentActivity = parent;
            Incoming = incoming;
            Evasion = evasionMode;
            Provider = provider ?? Evasion.CreateProvider();

            SetUpProvider(Provider);
            Stopwatch = new System.Diagnostics.Stopwatch();
        }

        protected override void startAction()
        {
            Speech.Say(Evasion.Prompt);
            Stopwatch.Start();
            Task.Delay(Incoming.AcquisitionTime.DividedBy(3))
                .ContinueWith(_ =>
                {
                    Incoming.AttackSFX.Play();
                    Speech.Say(Incoming.AttackSpeech);
                });
        }

        protected override bool nextStageCriterion()
        {
            ProcessedData.Add(Evasion.ProcessData(Provider));
            Timestamps.Add(Timestamps.LastOrDefault() + Provider.Interval);
            return Stopwatch.Elapsed >= Incoming.AcquisitionTime;
        }

        protected override void nextStageAction()
        {
            var AimScore = Evasion.AssessShot(ProcessedData, Timestamps, Incoming);

            if (AimScore >= 0)
                Incoming.IncomingAttackHitResults(AimScore).LaunchAsOrphan();
            else
                Incoming.IncomingAttackMissResults(AimScore).LaunchAsOrphan();

            BaseActivity.BackgroundStages.Remove(this);
        }
    }
}