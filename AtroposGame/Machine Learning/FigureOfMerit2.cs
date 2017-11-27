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
using System.Runtime.CompilerServices;
using System.Collections;

namespace com.Atropos.Machine_Learning
{
    #region Interfaces and abstract base classes
    public interface IAssess<Tdata>
    {
        string Name { get; set; }
        double Score(IEnumerable<Tdata> data);
    }

    public interface IAssessor<T, Tdata> : IAssess<Tdata>
    {
        T Assess(IEnumerable<Tdata> data);
        double zScore(T assessment);
        double ResultScore(double zscore);
    }

    public interface IAssessor<T> : IAssessor<T, T> { }

    [Serializable]
    public abstract class AssessorBase<T, Tdata> : IAssessor<T, Tdata> where T : struct where Tdata : struct
    {
        public abstract T Assess(IEnumerable<Tdata> data);

        public T Target;
        public T Sigma;
        public virtual double zScore(T assessment)
        {
            var numerator = Datapoint.From(Operator.Subtract(assessment, Target)).Magnitude();
            var denominator = Datapoint.From(Sigma).Magnitude();
            return ((double)numerator / denominator);
        }

        public virtual double ResultScore(double zscore)
        {
            return Math.Exp(-zscore);
        }

        public virtual double Score(IEnumerable<Tdata> data)
        {
            return ResultScore(zScore(Assess(data)));
        }

        public string Name { get; set; }
    }

    public abstract class AssessorBase<T> : AssessorBase<T, T>, IAssessor<T> where T : struct
    {
        // Nothing - just collecting the type args is enough.
    }

    [Serializable]
    public class AssessByAggregator<T, Tdata> : AssessorBase<T, Tdata> where T : struct where Tdata : struct
    {
        public AssessByAggregator(Func<T, Tdata, T> aggregator, [CallerMemberName] string name = "")
        {
            Startvalue = Datapoint.DefaultOrIdentity<T>();
            aggregatorFunc = aggregator;
            Name = name;
        }
        public virtual T Startvalue { get; set; } //= Datapoint.DefaultOrIdentity<T>();
        public Func<T, Tdata, T> aggregatorFunc;
        public override T Assess(IEnumerable<Tdata> data)
        {
            return data.Aggregate<Tdata, T>(Startvalue, aggregatorFunc);
        }
    }
    [Serializable]
    public class AssessByAggregator<T> : AssessByAggregator<T, T>, IAssessor<T> where T : struct
    {
        // Again, nothing - just collapsing the type args.
        public AssessByAggregator(Func<T, T, T> aggregator, [CallerMemberName] string name = "") : base(aggregator, name) { }
    }
    #endregion

    public static class Assessors<T, Tdata> where T : struct where Tdata : struct
    {
        #region Shortcut factory functions
        public static AssessByAggregator<T, Tdata> AssessBy(Func<T, Tdata, T> aggregator, [CallerMemberName] string callerName = null)
        {
            return new AssessByAggregator<T, Tdata>(aggregator, callerName); // { Name = callerName, aggregatorFunc = aggregator };
        }
        public static AssessByAggregator<T> AssessBy(Func<T, T, T> aggregator, [CallerMemberName] string callerName = null)
        {
            return new AssessByAggregator<T>(aggregator, callerName); //{ Name = callerName, aggregatorFunc = aggregator };
        }
        #endregion

        #region Static premade (and pre-named) assessors

        public static IAssessor<T> Sum { get { return AssessBy(Operator.Add<T>); } }
        public static IAssessor<T> Max { get { return AssessBy((current, newval) => { return (Operator.GreaterThan<T>(newval, current)) ? newval : current; }); } }
        public static IAssessor<T> Min { get { return AssessBy((current, newval) => { return (Operator.LessThan<T>(newval, current)) ? newval : current; }); } }
        public static IAssessor<TimeSpan, T> RunTime { get { return new RunTimeAssessor<T>(); } }
        #endregion
    }

    [Serializable]
    public class AssessorSet<Tdata> : IAssess<Tdata>, IList<IAssess<Tdata>>
    {
        public string Name { get; set; } = "AssessorSet"; // Has to exist for the interface, but is used mainly *inside* this set to differentiate assessor components.
        private List<IAssess<Tdata>> Assessors = new List<IAssess<Tdata>>();
        private List<double> coefficients = new List<double>();
        public IDictionary<IAssess<Tdata>, double> Coefficients
        {
            get
            {
                return new DictionaryFacade<IAssess<Tdata>, double>(Assessors, coefficients);
            }
        }

        public double Score(IEnumerable<Tdata> data)
        {
            var TotalWeight = coefficients.Sum();
            return Assessors
                .Select((a, i) =>
                {
                    return a.Score(data) * coefficients[i] / TotalWeight;
                })
                .Sum();
        }

        #region Dictionary-like access
        public IAssess<Tdata> this[string name]
        {
            get
            {
                return Assessors.FirstOrDefault((a) => a.Name == name);
            }
        }

        public bool Contains(string name) { return Assessors.Any(a => a.Name == name); }

        private void UniquifyName(IAssess<Tdata> item)
        {
            int highestIndex = -1;
            foreach (var a in Assessors)
            {
                int index;
                if (a.Name == item.Name) index = 1;
                else
                {
                    if (!a.Name.StartsWith(item.Name)) continue;
                    if (!int.TryParse(a.Name.Substring(item.Name.Length), out index)) continue;
                }
                highestIndex = Math.Max(index, highestIndex);
            }
            if (highestIndex >= 1) item.Name += (highestIndex + 1).ToString();
        }
        #endregion

        #region IList members *not* just passed through to the underlying list
        public void Add(IAssess<Tdata> item)
        {
            UniquifyName(item);
            Coefficients.Add(item, 1.0);
            ((IList<IAssess<Tdata>>)Assessors).Add(item);
        }
        public void Insert(int index, IAssess<Tdata> item)
        {
            UniquifyName(item);
            ((IList<IAssess<Tdata>>)Assessors).Insert(index, item);
            coefficients.Insert(index, 1.0);
        }

        public bool Remove(IAssess<Tdata> item)
        {
            return Coefficients.Remove(item);
            //return ((IList<IAssess<Tdata>>)Assessors).Remove(item);
        }

        public void Clear()
        {
            Coefficients.Clear();
            //((IList<IAssess<Tdata>>)Assessors).Clear();
        }

        public IAssess<Tdata> this[int index]
        {
            get
            {
                return ((IList<IAssess<Tdata>>)Assessors)[index];
            }

            set
            {
                Assessors[index] = null;
                UniquifyName(value);
                ((IList<IAssess<Tdata>>)Assessors)[index] = value;
            }
        }
        #endregion

        #region IList members simply passed through
        public int Count
        {
            get
            {
                return ((IList<IAssess<Tdata>>)Assessors).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IList<IAssess<Tdata>>)Assessors).IsReadOnly;
            }
        }

        public int IndexOf(IAssess<Tdata> item)
        {
            return ((IList<IAssess<Tdata>>)Assessors).IndexOf(item);
        }

        public bool Contains(IAssess<Tdata> item)
        {
            return ((IList<IAssess<Tdata>>)Assessors).Contains(item);
        }

        public void CopyTo(IAssess<Tdata>[] array, int arrayIndex)
        {
            ((IList<IAssess<Tdata>>)Assessors).CopyTo(array, arrayIndex);
        }

        public void RemoveAt(int index)
        {
            var item = Assessors[index];
            Remove(item);
        }

        public IEnumerator<IAssess<Tdata>> GetEnumerator()
        {
            return ((IList<IAssess<Tdata>>)Assessors).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<IAssess<Tdata>>)Assessors).GetEnumerator();
        }
        #endregion
    }

    #region Dependency Seeker - marker interface and service locator for anything that needs a "what is X right now?" of some kind.
    public static class DependencySeeker
    {
        public interface INeedSomething
        {
            DepType NeedThis { get; }
        }
        public enum DepType
        {
            Activity,
            GestureStage,
            IProvider,
            ILoggingProvider,
            Dataset,
            Sequence,
            GestureClass
        }

        public static object Seek(DepType needThis)
        {
            try
            {
                var act = BaseActivity.CurrentActivity;
                if (needThis == DepType.Activity) return act;

                var stage = BaseActivity.CurrentStage;
                if (needThis == DepType.GestureStage) return stage;

                var prov = (stage as IGestureRecognizerStage).DataProvider;
                if (needThis == DepType.IProvider) return prov;

                if (needThis == DepType.ILoggingProvider && prov is ILoggingProvider)
                    return prov;
                
                if (needThis == DepType.Dataset && Machine_Learning.DataSet.Current != null)
                    return Machine_Learning.DataSet.Current;

                var actML = act as Machine_Learning.MachineLearningActivity.IMachineLearningActivity;
                if (needThis == DepType.GestureClass
                    && actML != null
                    && actML.SelectedGestureClass != null)
                        return actML.SelectedGestureClass;

                if (needThis == DepType.Sequence
                    && actML != null
                    && actML.MostRecentSample != null)
                    return actML.MostRecentSample;
            }
            catch (NullReferenceException)
            {
                return null;
            }

            return null;
        }
    }

    // One example of a useful type which needed the DependencySeeker interface (among other reasons, for post-deserialiation)
    [Serializable]
    public class RunTimeAssessor<Tdata> 
        : AssessorBase<TimeSpan, Tdata>, IAssessor<TimeSpan, Tdata>, DependencySeeker.INeedSomething
        where Tdata : struct
    {
        public DependencySeeker.DepType NeedThis { get; } = DependencySeeker.DepType.ILoggingProvider;
        private ILoggingProvider<Tdata> Provider
        {
            get
            {
                return (ILoggingProvider<Tdata>)DependencySeeker.Seek(NeedThis);
            }
        }

        public RunTimeAssessor([CallerMemberName] string name = "")
        {
            Startvalue = TimeSpan.Zero;
            Name = name;
        }
        public virtual TimeSpan Startvalue { get; set; } 
        public override TimeSpan Assess(IEnumerable<Tdata> data)
        {
            var p = Provider;
            var iStart = p.LoggedData.IndexOf(data.First());
            var iEnd = p.LoggedData.IndexOf(data.Last());
            return p.Intervals.Skip(iStart).Take(iEnd - iStart).Sum();
        }
    }
    #endregion

    //#region Typical "library" of FoM components used to calculate the FoM for a particular gesture class
    //[Serializable]
    //public class GestureClassFoMHandler<T> : IFoM<T> where T : struct
    //{
    //    public BindingList<IFoMComponent> Components = new BindingList<IFoMComponent>();
    //    private string className;
    //    public GestureClassFoMHandler(GestureClass owningClass, params IFoMComponent[] components)
    //    {
    //        className = owningClass.className;
    //        Components.RaiseListChangedEvents = true;
    //        Components.ListChanged += (o, e) =>
    //        {
    //            Renormalize();
    //            if (e.ListChangedType == ListChangedType.ItemAdded) Components[e.NewIndex].CoefficientChanged += (ob, ev) => Renormalize();
    //        };
    //        foreach (var comp in components) Components.Add(comp);
    //    }

    //    protected void Renormalize()
    //    {
    //        var TotalRawCoeff = Components.Sum((c) => c.Coefficient);
    //        foreach (int i in Enumerable.Range(0, Components.Count))
    //        {
    //            Components[i].Weight = 1.0 / TotalRawCoeff;
    //        }
    //    }

    //    public void Add(T newVal)
    //    {
    //        foreach (var Comp in Components)
    //        {
    //            (Comp as IFoM<T>).Add(newVal);
    //        }
    //    }

    //    public double FoMcontribution
    //    {
    //        get
    //        {
    //            return Components
    //                    .Select(c => (c as IFoM<T>).FoMcontribution)
    //                    .Sum();
    //        }
    //    }

    //    public void Reset()
    //    {
    //        foreach (var c in Components) (c as IFoM<T>).Reset();
    //    }

    //    #region Utilities for finding the Nth instance of a particular *type* of FoMComponent (like the RunTime handler or whatever)
    //    protected int IndexOf<Tcomponent>(int instanceNumber = 1) where Tcomponent : IFoMComponent
    //    {
    //        int i = -1;
    //        foreach (int j in Enumerable.Range(0, instanceNumber))
    //        {
    //            i = Components.ToList().FindIndex(i + 1, (comp) => comp is Tcomponent); // So each time we find one, make the one after it the start point of the next pass.
    //        }
    //        if (i < 0) throw new ArgumentOutOfRangeException($"Cannot find {typeof(Tcomponent).Name} instance #{instanceNumber} in components list.");
    //        return i;
    //    }

    //    public double GetCoefficient<Tcomponent>(int instanceNumber = 1) where Tcomponent : IFoMComponent
    //    {
    //        return Components.ElementAtOrDefault(IndexOf<Tcomponent>(instanceNumber)).Coefficient;
    //    }

    //    public void SetCoefficient<Tcomponent>(double newCoefficient) where Tcomponent : IFoMComponent
    //    {
    //        SetCoefficient<Tcomponent>(1, newCoefficient);
    //    }
    //    public void SetCoefficient<Tcomponent>(int instanceNumber, double newCoefficient) where Tcomponent : IFoMComponent
    //    {
    //        var i = IndexOf<Tcomponent>(instanceNumber);
    //        Components[i].Coefficient = newCoefficient;
    //        Renormalize();
    //    }
    //    #endregion

    //    //#region ISerializable functionality
    //    //[Serializable]
    //    //private class ComponentRepresentation<Tcomponent, T1, Tdata>
    //    //{
    //    //    public 
    //    //}
    //    //protected GestureClassFoMHandler(SerializationInfo info, StreamingContext context) : this()
    //    //{
    //    //    if (info == null) throw new System.ArgumentNullException("info");
    //    //}
    //    //#endregion
    //}
    //#endregion

    //#region Specific templates for expected quantities - run time, total magnitude, dot products, etc.
    //[Serializable]
    //public class RuntimeAssessor : AssessByAggregator<TimeSpan>
    //{
    //    [NonSerialized]
    //    private IList<TimeSpan> Intervals;
    //    public FoM_RunTime(IList<TimeSpan> intervalsList)
    //    {
    //        Intervals = intervalsList;
    //    }

    //    public override void Add(T newVal)
    //    {
    //        Total += Intervals.LastOrDefault();
    //    }
    //    public override double zScore { get { return (Total - Target).TotalMilliseconds / Sigma.TotalMilliseconds; } }
    //}

    //[Serializable]
    //public class FoM_TotalMagnitude<T> : FoMComponent<T, float> where T : struct
    //{
    //    public override void Add(T newVal)
    //    {
    //        Total += Datapoint.From<T>(newVal).Magnitude();
    //    }
    //}

    //// Can also do an AverageMagnitude, but it'll be a little trickier - best will be to use our SimpleAverage<T> internally.
    //// Don't need it right now anyway, so TODO if at all.

    //[Serializable]
    //public class FoM_MaxMagnitude<T> : FoMComponent<T, float> where T : struct
    //{
    //    public override void Add(T newVal)
    //    {
    //        Total = Math.Max(Total, Datapoint.From<T>(newVal).Magnitude());
    //    }
    //}

    //[Serializable]
    //public class FoM_MinMagnitude<T> : FoMComponent<T, float> where T : struct
    //{
    //    public override void Add(T newVal)
    //    {
    //        Total = Math.Min(Total, Datapoint.From<T>(newVal).Magnitude());
    //    }
    //}

    //[Serializable]
    //public class FoM_DotProduct : FoMComponent<Vector3, float>
    //{
    //    [NonSerialized]
    //    protected Vector3 oldVal = Vector3.Zero;
    //    public override void Add(Vector3 newVal)
    //    {
    //        Total += oldVal.Dot(newVal);
    //        oldVal = newVal;
    //    }
    //}

    //[Serializable]
    //public class FoM_NormaliedDotProduct : FoM_DotProduct
    //{
    //    public override void Add(Vector3 newVal)
    //    {
    //        base.Add(newVal.Normalize());
    //    }
    //}

    //[Serializable]
    //public class FoM_AngleChange : FoMComponent<Vector3, double>
    //{
    //    [NonSerialized]
    //    protected RollingAverage<Vector3> previousVector
    //        = new RollingAverage<Vector3>(3, Vector3.Zero); // Uses a lightly smoothed rolling average instead of just the previous vector because I expect this to be pretty swingy if there's a lot of noise.
    //    public override void Add(Vector3 newVal)
    //    {
    //        newVal = newVal.Normalize();
    //        Total += Math.Acos(previousVector.Average.Dot(newVal));
    //        previousVector.Update(newVal);
    //    }
    //}

    //// Versions which want to look at the whole sequence can just receive an IList<T> in the constructor,
    //// which is filled by the appropriate List<T> internal to the class (LoggedData, SmoothedData, etc).
    //#endregion
}
