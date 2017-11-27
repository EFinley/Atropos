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
using Android.Hardware;
using System.Threading;
using com.Atropos.Machine_Learning;

namespace com.Atropos.DataStructures
{
    public struct Datapoint<T1, T2> : IDatapoint<T1,T2>
        where T1 : struct
        where T2 : struct
    {
        private T1? _v1;
        //private Nullable<T1> _v1;
        public T1 Value1
        {
            get
            {
                _v1 = (_v1 != null && _v1.HasValue) ? _v1.Value : Datapoint.DefaultOrIdentity<T1>();
                return _v1.Value;
            }
            set { _v1 = value; }
        }
        private T2? _v2;
        //private Nullable<T2> _v2;
        public T2 Value2
        {
            get
            {
                _v2 = (_v2 != null && _v2.HasValue) ? _v2.Value : Datapoint.DefaultOrIdentity<T2>();
                return _v2.Value;
            }
            set { _v2 = value; }
        }

        public static int Dimensions { get { return dimensions.Sum(); } }
        private static int[] dimensions { get; set; }
        int IDatapoint.Dimensions { get { return Dimensions; } }

        public float[] AsArray()
        {
            var a1 = Datapoint.From(Value1).AsArray();
            var a2 = Datapoint.From(Value2).AsArray();
            var result = a1.Concat(a2).ToArray();
            return result;
        }

        public float Magnitude()
        {
            var m1 = Datapoint.From(Value1).Magnitude();
            var m2 = Datapoint.From(Value2).Magnitude();
            return (float)Math.Sqrt(m1 * m1 + m2 * m2);
        }

        //public Datapoint<T1,T2> FromArray(float[] sourceArray)
        //{
        //    var l1 = new float[dimensions[0]];
        //    var l2 = new float[dimensions[1]];
        //    foreach (int i in Enumerable.Range(0, dimensions[0]))
        //        l1[i] = sourceArray[i];
        //    foreach (int i in Enumerable.Range(0, dimensions[1]))
        //        l2[i] = sourceArray[i + dimensions[0]];

        //    Value1 = typeof(Datapoint<,>)
        //            .MakeGenericType(typeof(T1))
        //            .InvokeStaticMethod<Datapoint<T1>>("FromArray", l1)
        //            .Value;
        //    Value2 = typeof(Datapoint<,>)
        //            .MakeGenericType(typeof(T2))
        //            .InvokeStaticMethod<Datapoint<T2>>("FromArray", l2)
        //            .Value;
        //    return this;
        //}

        IDatapoint IDatapoint.FromArray(float[] sourceArray)
        {
            var sublistA = sourceArray.Take(dimensions[0]);
            var sublistB = sourceArray.Skip(dimensions[0])
                                      .Take(dimensions[1]);
            return new Datapoint<T1, T2>()
            {
                Value1 = (T1)new Datapoint<T1>().FromArray(sublistA.ToArray()),
                Value2 = (T2)new Datapoint<T2>().FromArray(sublistB.ToArray())
            };
        }
        public Datapoint<T1,T2> FromArray(float[] sourceArray)
        {
            return (Datapoint<T1, T2>)((this as IDatapoint).FromArray(sourceArray));
        }

        #region Proto-functions designed to simplify applying the various operators

        // For binary operators...
        private static Func<Datapoint<T1, T2>, Datapoint<T1, T2>, Datapoint<T1, T2>> Apply(
            Func<T1, T1, T1> Func1,
            Func<T2, T2, T2> Func2)
        {
            return (Datapoint<T1, T2> first, Datapoint<T1, T2> second) =>
                new Datapoint<T1, T2>()
                {
                    Value1 = Func1(first.Value1, second.Value1),
                    Value2 = Func2(first.Value2, second.Value2)
                };
        }

        // For unary operators...
        private static Func<Datapoint<T1, T2>, Datapoint<T1, T2>> Apply(
            Func<T1, T1> Func1,
            Func<T2, T2> Func2)
        {
            return (Datapoint < T1, T2 > self) => 
                new Datapoint<T1, T2>()
                {
                    Value1 = Func1(self.Value1),
                    Value2 = Func2(self.Value2)
                };
        }

        // And for mixed-with-another-type operators...
        private static Func<Datapoint<T1, T2>, Tother, Datapoint<T1, T2>> Apply<Tother>(
            Func<T1, Tother, T1> Func1,
            Func<T2, Tother, T2> Func2)
        {
            return (Datapoint<T1, T2> cluster, Tother other) =>
                new Datapoint<T1, T2>()
                {
                    Value1 = Func1(cluster.Value1, other),
                    Value2 = Func2(cluster.Value2, other)
                };
        }
        #endregion

        #region Standard operators defined as componentwise versions of their usual selves on each of T1 & T2
        private static Func<Datapoint<T1, T2>, Datapoint<T1, T2>, Datapoint<T1, T2>> _add;
        public static Datapoint<T1, T2> operator +(Datapoint<T1, T2> first, Datapoint<T1, T2> second) { return _add(first, second); }

        private static Func<Datapoint<T1, T2>, Datapoint<T1, T2>, Datapoint<T1, T2>> _subtract;
        public static Datapoint<T1, T2> operator -(Datapoint<T1, T2> first, Datapoint<T1, T2> second) { return _subtract(first, second); }

        // Unary negation is also fine to do componentwise.
        private static Func<Datapoint<T1, T2>, Datapoint<T1, T2>> _negate;
        public static Datapoint<T1, T2> operator -(Datapoint<T1, T2> self) { return _negate(self); }

        // Multiplication operators get a little more complex because the "other woman" can have varying types, and AFAIK the operator overloading is picky about specifics there.
        // Here we just cover the most important three - double, float [because System.Numerics.VectorN plays nicely with it, unlike double], and int.
        private static Func<Datapoint<T1, T2>, float, Datapoint<T1, T2>> _multiplyByScalar;
        public static Datapoint<T1, T2> operator *(Datapoint<T1, T2> vec, double scalar) { return _multiplyByScalar(vec, (float)scalar); }
        public static Datapoint<T1, T2> operator *(double scalar, Datapoint<T1, T2> vec) { return _multiplyByScalar(vec, (float)scalar); }
        public static Datapoint<T1, T2> operator *(Datapoint<T1, T2> vec, float scalar) { return _multiplyByScalar(vec, scalar); }
        public static Datapoint<T1, T2> operator *(float scalar, Datapoint<T1, T2> vec) { return _multiplyByScalar(vec, scalar); }
        public static Datapoint<T1, T2> operator *(Datapoint<T1, T2> vec, int scalar) { return _multiplyByScalar(vec, scalar); }
        public static Datapoint<T1, T2> operator *(int scalar, Datapoint<T1, T2> vec) { return _multiplyByScalar(vec, scalar); }

        // Division we only do *by* a scalar, never *by* our vector types, so it's a bit easier.
        private static Func<Datapoint<T1, T2>, float, Datapoint<T1, T2>> _divideByScalar;
        public static Datapoint<T1, T2> operator /(Datapoint<T1, T2> vec, double scalar) { return _divideByScalar(vec, (float)scalar); }
        public static Datapoint<T1, T2> operator /(Datapoint<T1, T2> vec, float scalar) { return _divideByScalar(vec, scalar); }
        public static Datapoint<T1, T2> operator /(Datapoint<T1, T2> vec, int scalar) { return _divideByScalar(vec, scalar); }
        #endregion

        static Datapoint()
        {
            _add = Apply(Operator.Add<T1>, Operator.Add<T2>);
            _subtract = Apply(Operator.Subtract<T1>, Operator.Subtract<T2>);
            _negate = Apply(Operator.Negate<T1>, Operator.Negate<T2>);
            _multiplyByScalar = Apply<float>(Operator.MultiplyAlternative<T1, float>, Operator.MultiplyAlternative<T2, float>);
            _divideByScalar = Apply<float>(Operator.DivideAlternative<T1, float>, Operator.DivideAlternative<T2, float>);

            //dimensions = new Type[] { typeof(T1), typeof(T2) }
            //        .Select(t => (default(t) as IDatapoint).Dimensions)
            //        .ToArray();
            dimensions = new int[]
            {
                (default(Datapoint<T1>) as IDatapoint).Dimensions,
                (default(Datapoint<T2>) as IDatapoint).Dimensions
            };
        }

        public override string ToString()
        {
            return $"\u1445{Value1}|{Value2}\u1440";
        }
    }

    //// And, for bonus points, here's how to extend the concept into arbitrarily large feature vectors...
    //public struct Datapoint<T1, T2, T3> : IDatapoint
    //    where T1 : struct
    //    where T2 : struct
    //    where T3 : struct
    //{
    //    public T1 Value1;
    //    public T2 Value2;
    //    public T3 Value3;
    //    private static int[] dimensions { get; set; }
    //    public static int Dimensions
    //    {
    //        get { return dimensions.Sum(); }
    //    }
    //    int IDatapoint.Dimensions { get { return Dimensions; } }

    //    public float[] AsArray()
    //    {
    //        return ((IDatapoint)Value1).AsArray()
    //                .Concat(((IDatapoint)Value2).AsArray())
    //                .Concat(((IDatapoint)Value3).AsArray())
    //                .ToArray();
    //    }
    //    public float Magnitude()
    //    {
    //        return ((Datapoint<Datapoint<T1, T2>, T3>)this).Magnitude();
    //    }

    //    public IDatapoint FromArray(float[] sourceArray)
    //    {
    //        var sublistA = sourceArray.Take(dimensions[0] + dimensions[1]);
    //        var sublistB = sourceArray.Skip(dimensions[0] + dimensions[1])
    //                                  .Take(dimensions[2]);
    //        return (Datapoint<T1,T2,T3>)new Datapoint<Datapoint<T1, T2>, T3>()
    //        {
    //            Value1 = (Datapoint<T1,T2>)new Datapoint<T1, T2>().FromArray(sublistA.ToArray()),
    //            Value2 = (T3)new Datapoint<T3>().FromArray(sublistB.ToArray())
    //        };
    //    }

    //    static Datapoint()
    //    {
    //        //dimensions = new Type[] { typeof(T1), typeof(T2), typeof(T3) }
    //        //    .Select(t => (t as IDatapoint).Dimensions)
    //        //    .ToArray();
    //        dimensions = new int[]
    //        {
    //            (default(Datapoint<T1>) as IDatapoint).Dimensions,
    //            (default(Datapoint<T2>) as IDatapoint).Dimensions,
    //            (default(Datapoint<T3>) as IDatapoint).Dimensions
    //        };
    //    }

    //    // We can convert to (or from) a more conventional Datapoint<Tone,Ttwo> type by grouping T1 & T2.
    //    // This should allow for as much expansion as necessary, chaining these together.
    //    public static implicit operator Datapoint<Datapoint<T1,T2>,T3>(Datapoint<T1,T2,T3> original)
    //    {
    //        return new Datapoint<Datapoint<T1, T2>, T3>()
    //        {
    //            Value1 = new Datapoint<T1, T2>()
    //            {
    //                Value1 = original.Value1,
    //                Value2 = original.Value2
    //            },
    //            Value2 = original.Value3
    //        };
    //    }
    //    public static implicit operator Datapoint<T1,T2,T3>(Datapoint<Datapoint<T1,T2>,T3> original)
    //    {
    //        return new Datapoint<T1, T2, T3>()
    //        {
    //            Value1 = original.Value1.Value1,
    //            Value2 = original.Value1.Value2,
    //            Value3 = original.Value2
    //        };
    //    }
    //}
}