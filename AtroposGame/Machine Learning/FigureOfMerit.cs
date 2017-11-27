using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using System.Drawing;
using Android.Graphics;
using PerpetualEngine.Storage;
using System.Threading.Tasks;
using com.Atropos.DataStructures;
using MiscUtil;
using System.Numerics;
using System.Runtime.Serialization;
using Android.App;
using com.Atropos.Machine_Learning;

namespace com.Atropos.Machine_Learning_old
{
    #region Interfaces and abstract base classes
    public interface IFoM<T>
    {
        void Reset();
        void Add(T newVal);
        double FoMcontribution { get; }
    }

    public interface IFoMComponent
    {
        double zScore { get; }
        double Coefficient { get; set; }
        double Weight { get; set; }
        event EventHandler CoefficientChanged;
    }

    public interface IFoMwithDependency
    {
        void SetupDependency();
    }

    [Serializable]
    public abstract class FoMComponentBase<T> : IFoMComponent, IFoM<T> where T : struct
    {
        public abstract void Reset();
        public abstract void Add(T newVal);
        public abstract double zScore { get; }

        protected double _coeff = 1.0;
        public virtual double Coefficient { get { return _coeff; } set { _coeff = value; CoefficientChanged?.Invoke(this, new EventArgs()); } }
        public event EventHandler CoefficientChanged;
        public virtual double Weight { get; set; } = 1.0;
        //public virtual double FoMcontribution { get { return Coefficient * zScore; } }
        public virtual double FoMcontribution { get { return Weight * Coefficient * Math.Exp(-zScore); } }

        //public void Add(object newVal)
        //{
        //    Add((T)newVal);
        //}
    }

    [Serializable]
    public abstract class FoMComponent<T, T2> : FoMComponentBase<T> where T : struct where T2 : struct
    {
        public T2 Total { get; set; }
        public T2 Target;
        public T2 Sigma;
        public override double zScore { get { return _getZscore?.Invoke(Total, Target, Sigma) ?? double.NaN; } }

        public override void Reset()
        {
            Total = Datapoint.DefaultOrIdentity<T2>();
        }

        [NonSerialized]
        protected static Func<T2, T2, T2, double> _getZscore;
        //// Methodically search through valid operations to get a way to do (Total - Target) / Sigma => double.
        //private static void ThrowIfNotLegit(IDatapoint D)
        //{
        //    try
        //    {
        //        var dMag = D.Magnitude();
        //        if (float.IsNaN(dMag) || float.IsInfinity(dMag)) throw new InvalidOperationException();
        //    }
        //    catch { throw new InvalidOperationException(); }
        //}
        //private static void ThrowIfNotLegit(T2 D, bool thisIsLessPreferable = true) // Tricksy! Disambiguates in favour of the other one, for the compiler, even though we never ever actually use it.
        //{
        //    ThrowIfNotLegit(Datapoint.From(D));
        //}

        static FoMComponent()
        {
            //bool CanCreateNonzeroExample = false,
            //     CanCastToDatapoint = false,
            //     CanConvertToDatapoint = false,
            //     CanSubtract = false,
            //     CanSubtractAfterCasting = false,
            //     CanDivideAndGetDouble = false,
            //     CanDivideAndTakeMagnitude = false,
            //     CanDivideMagnitudes = false;
            //T2 defaultExample, nonzeroExample;

            //defaultExample = Datapoint.DefaultOrIdentity<T2>(); // This *ought* to be always safe, since it only differs from default(T2) in specific case(s).

            //try
            //{
            //    var nonzero = Datapoint
            //                        .From(default(T2)) // *Should* yield an example Datapoint<T2> (exact contents are irrelevant here, it's like a template)
            //                        .FromArray(Enumerable.Repeat(1.0f, Datapoint<T2>.Dimensions).ToArray()); // This will presumably always yield a nonzero value - it might not make sense but it's a good start.
            //    ThrowIfNotLegit(nonzero);
            //    nonzeroExample = (T2)nonzero;
            //    CanCreateNonzeroExample = true;
            //}
            //catch (InvalidOperationException) { }

            //try
            //{

            //    var verifyCast = (Datapoint<T2>)defaultExample;
            //    ThrowIfNotLegit(verifyCast);
            //    CanCastToDatapoint = true;
            //}
            //catch (InvalidCastException) { }
            //catch (InvalidOperationException) { }

            //if (!CanCastToDatapoint)
            //    try
            //    {
            //        var verifyConvert = Operator.Convert<T2, Datapoint<T2>>(Datapoint.DefaultOrIdentity<T2>());
            //        ThrowIfNotLegit(verifyConvert);
            //        CanConvertToDatapoint = true;
            //    }
            //    catch (InvalidCastException) { }
            //    catch (InvalidOperationException) { }

            //try
            //{
            //    var verifySubtraction = Operator.Subtract(default(T2), default(T2));
            //    ThrowIfNotLegit(verifySubtraction);
            //    CanSubtract = true;
            //}
            //catch (InvalidOperationException) { }

            // Etc., etc., etc.

            //if (CanSubtract && CanDivideMagnitudes)
            //{
                _getZscore = (Total, Target, Sigma) =>
                {
                    var numerator = Datapoint.From(Operator.Subtract(Total, Target)).Magnitude();
                    var denominator = Datapoint.From(Sigma).Magnitude();
                    return ((double)numerator / denominator);
                };
            //}
        }

        //public override string ToString()
        //{
        //    return $"FoMComponent|{this.GetType().Name}|{typeof(T).Name}|{typeof(T2).Name}|{Coefficient}|{Target}|{Sigma}";
        //}
    }

    // For the case where you literally just want to add each submission to the last and track their (vector etc) sum.
    [Serializable]
    public abstract class FoMComponent<T> : FoMComponent<T,T> where T : struct
    {
        public override void Add(T newVal)
        {
            Total = Operator.Add(Total, newVal);
        }
    }

    public static class FoMutils
    {
        public static GestureRecognizerStage GetCurrentStage()
        {
            return BaseActivity.CurrentStage as GestureRecognizerStage;
        }

        public static IProvider GetCurrentProvider()
        {
            return GetCurrentStage().DataProvider;
        }
    }

    #endregion

    #region Typical "library" of FoM components used to calculate the FoM for a particular gesture class
    [Serializable]
    public class GestureClassFoMHandler<T> : IFoM<T> where T : struct
    {
        public BindingList<IFoMComponent> Components = new BindingList<IFoMComponent>();
        private string className;
        public GestureClassFoMHandler(GestureClass owningClass, params IFoMComponent[] components)
        {
            className = owningClass.className;
            Components.RaiseListChangedEvents = true;
            Components.ListChanged += (o, e) =>
            {
                Renormalize();
                if (e.ListChangedType == ListChangedType.ItemAdded) Components[e.NewIndex].CoefficientChanged += (ob, ev) => Renormalize();
            };
            foreach (var comp in components) Components.Add(comp);
        }

        protected void Renormalize()
        {
            var TotalRawCoeff = Components.Sum((c) => c.Coefficient);
            foreach (int i in Enumerable.Range(0, Components.Count))
            {
                Components[i].Weight = 1.0 / TotalRawCoeff;
            }
        }

        public void Add(T newVal)
        {
            foreach (var Comp in Components)
            {
                (Comp as IFoM<T>).Add(newVal);
            }
        }

        public double FoMcontribution
        {
            get
            {
                return Components
                        .Select(c => (c as IFoM<T>).FoMcontribution)
                        .Sum();
            }
        }

        public void Reset()
        {
            foreach (var c in Components) (c as IFoM<T>).Reset();
        }

        #region Utilities for finding the Nth instance of a particular *type* of FoMComponent (like the RunTime handler or whatever)
        protected int IndexOf<Tcomponent>(int instanceNumber = 1) where Tcomponent : IFoMComponent
        {
            int i = -1;
            foreach (int j in Enumerable.Range(0, instanceNumber))
            {
                i = Components.ToList().FindIndex(i + 1, (comp) => comp is Tcomponent); // So each time we find one, make the one after it the start point of the next pass.
            }
            if (i < 0) throw new ArgumentOutOfRangeException($"Cannot find {typeof(Tcomponent).Name} instance #{instanceNumber} in components list.");
            return i;
        }

        public double GetCoefficient<Tcomponent>(int instanceNumber = 1) where Tcomponent : IFoMComponent
        {
            return Components.ElementAtOrDefault(IndexOf<Tcomponent>(instanceNumber)).Coefficient;
        }
        
        public void SetCoefficient<Tcomponent>(double newCoefficient) where Tcomponent : IFoMComponent
        {
            SetCoefficient<Tcomponent>(1, newCoefficient);
        }
        public void SetCoefficient<Tcomponent>(int instanceNumber, double newCoefficient) where Tcomponent : IFoMComponent
        {
            var i = IndexOf<Tcomponent>(instanceNumber);
            Components[i].Coefficient = newCoefficient;
            Renormalize();
        }
        #endregion

        //#region ISerializable functionality
        //[Serializable]
        //private class ComponentRepresentation<Tcomponent, T1, T2>
        //{
        //    public 
        //}
        //protected GestureClassFoMHandler(SerializationInfo info, StreamingContext context) : this()
        //{
        //    if (info == null) throw new System.ArgumentNullException("info");
        //}
        //#endregion
    }
    #endregion

    #region Specific templates for expected quantities - run time, total magnitude, dot products, etc.
    [Serializable]
    public class FoM_RunTime<T> : FoMComponent<T, TimeSpan> where T : struct
    {
        [NonSerialized]
        private IList<TimeSpan> Intervals;
        public FoM_RunTime(IList<TimeSpan> intervalsList)
        {
            Intervals = intervalsList;
        }

        public override void Add(T newVal)
        {
            Total += Intervals.LastOrDefault();
        }
        public override double zScore { get { return (Total - Target).TotalMilliseconds / Sigma.TotalMilliseconds; } }
    }

    [Serializable]
    public class FoM_TotalMagnitude<T> : FoMComponent<T, float> where T : struct
    {
        public override void Add(T newVal)
        {
            Total += Datapoint.From<T>(newVal).Magnitude();
        }
    }

    // Can also do an AverageMagnitude, but it'll be a little trickier - best will be to use our SimpleAverage<T> internally.
    // Don't need it right now anyway, so TODO if at all.

    [Serializable]
    public class FoM_MaxMagnitude<T> : FoMComponent<T, float> where T : struct
    {
        public override void Add(T newVal)
        {
            Total = Math.Max(Total, Datapoint.From<T>(newVal).Magnitude());
        }
    }

    [Serializable]
    public class FoM_MinMagnitude<T> : FoMComponent<T, float> where T : struct
    {
        public override void Add(T newVal)
        {
            Total = Math.Min(Total, Datapoint.From<T>(newVal).Magnitude());
        }
    }

    [Serializable]
    public class FoM_DotProduct : FoMComponent<Vector3, float>
    {
        [NonSerialized]
        protected Vector3 oldVal = Vector3.Zero;
        public override void Add(Vector3 newVal)
        {
            Total += oldVal.Dot(newVal);
            oldVal = newVal;
        }
    }

    [Serializable]
    public class FoM_NormaliedDotProduct : FoM_DotProduct
    {
        public override void Add(Vector3 newVal)
        {
            base.Add(newVal.Normalize());
        }
    }

    [Serializable]
    public class FoM_AngleChange : FoMComponent<Vector3, double>
    {
        [NonSerialized]
        protected RollingAverage<Vector3> previousVector
            = new RollingAverage<Vector3>(3, Vector3.Zero); // Uses a lightly smoothed rolling average instead of just the previous vector because I expect this to be pretty swingy if there's a lot of noise.
        public override void Add(Vector3 newVal)
        {
            newVal = newVal.Normalize();
            Total += Math.Acos(previousVector.Average.Dot(newVal));
            previousVector.Update(newVal);
        }
    }

    // Versions which want to look at the whole sequence can just receive an IList<T> in the constructor,
    // which is filled by the appropriate List<T> internal to the class (LoggedData, SmoothedData, etc).
    #endregion
}
