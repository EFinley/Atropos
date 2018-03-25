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
using Atropos.Machine_Learning;
using System.Runtime.Serialization;

namespace Atropos.DataStructures
{
    [Serializable]
    public struct Datapoint<T1, T2> : IDatapoint, ISerializable // IDatapoint<T1,T2>
        where T1 : struct
        where T2 : struct
    {
        [NonSerialized] private T1? _v1;
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
        [NonSerialized] private T2? _v2;
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

        private Datapoint<T1,T2> _fromArray(float[] sourceArray)
        {
            var sublistA = sourceArray.Take(dimensions[0]);
            var sublistB = sourceArray.Skip(dimensions[0])
                                      .Take(dimensions[1]);
            return new Datapoint<T1, T2>()
            {
                Value1 = (T1)(Datapoint<T1>)(new Datapoint<T1>()).FromArray(sublistA.ToArray()),
                Value2 = (T2)(Datapoint<T2>)(new Datapoint<T2>()).FromArray(sublistB.ToArray())
            };
        }
        //IDatapoint IDatapoint.FromArray(float[] sourceArray)
        //{
        //    return _fromArray(sourceArray);
        //}
        public IDatapoint FromArray(float[] sourceArray)
        {
            return _fromArray(sourceArray);
        }
        //public Datapoint<T1,T2> FromArray(float[] sourceArray)
        //{
        //    return (Datapoint<T1, T2>)((this as IDatapoint).FromArray(sourceArray));
        //}

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

        public static bool operator ==(Datapoint<T1, T2> first, Datapoint<T1, T2> second)
        { return Operator.Equal(first.Value1, second.Value1) && Operator.Equal(first.Value2, second.Value2); }
        public static bool operator !=(Datapoint<T1, T2> first, Datapoint<T1, T2> second)
        { return Operator.NotEqual(first.Value1, second.Value1) || Operator.NotEqual(first.Value2, second.Value2); }
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
            return $"\u1445 {Value1}|{Value2} \u1440";
        }

        // Serialization... Implementing ISerializable

        // First, we need an explicit parameterless constructor, which is technically not allowed for a struct; one with an optional parameter is, though.
        public Datapoint(T1 val1 = default(T1), T2 val2 = default(T2))
        {
            _v1 = (Operator.NotEqual(val1, default(T1)) ? val1 : Datapoint.DefaultOrIdentity<T1>());
            _v2 = (Operator.NotEqual(val2, default(T2)) ? val2 : Datapoint.DefaultOrIdentity<T2>());
        }

        // Otherwise, the special ISerializable constructor would be our only constructor, which would render things... sticky.
        public Datapoint(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            var valueArray = (float[])info.GetValue("valueArray", typeof(float[]));
            var fArray = (Datapoint<T1, T2>)((IDatapoint)new Datapoint<T1, T2>()).FromArray(valueArray);
            _v1 = fArray.Value1;
            _v2 = fArray.Value2;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            info.AddValue("valueArray", AsArray());
        }
    }

    // And, for bonus points, here's how to extend the concept into arbitrarily large feature vectors...
    [Serializable]
    public struct Datapoint<T1, T2, T3> : IDatapoint, ISerializable
        where T1 : struct
        where T2 : struct
        where T3 : struct
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
        //private Nullable<T1> _v1;
        public T2 Value2
        {
            get
            {
                _v2 = (_v2 != null && _v2.HasValue) ? _v2.Value : Datapoint.DefaultOrIdentity<T2>();
                return _v2.Value;
            }
            set { _v2 = value; }
        }
        private T3? _v3;
        //private Nullable<T3> _v3;
        public T3 Value3
        {
            get
            {
                _v3 = (_v3 != null && _v3.HasValue) ? _v3.Value : Datapoint.DefaultOrIdentity<T3>();
                return _v3.Value;
            }
            set { _v3 = value; }
        }
        private static int[] dimensions { get; set; }
        public static int Dimensions
        {
            get { return dimensions.Sum(); }
        }
        int IDatapoint.Dimensions { get { return Dimensions; } }

        public float[] AsArray()
        {
            var a1 = Datapoint.From(Value1).AsArray();
            var a2 = Datapoint.From(Value2).AsArray();
            var a3 = Datapoint.From(Value3).AsArray();
            var result = a1.Concat(a2).Concat(a3).ToArray();
            return result;
        }

        public float Magnitude()
        {
            return ((Datapoint<Datapoint<T1, T2>, T3>)this).Magnitude();
        }

        public IDatapoint FromArray(float[] sourceArray)
        {
            //var sublistA = sourceArray.Take(dimensions[0] + dimensions[1]);
            //var sublistB = sourceArray.Skip(dimensions[0] + dimensions[1])
            //                          .Take(dimensions[2]);
            //return (Datapoint<T1, T2, T3>)new Datapoint<Datapoint<T1, T2>, T3>()
            //{
            //    Value1 = new Datapoint<T1, T2>().FromArray(sublistA.ToArray()),
            //    Value2 = (T3)new Datapoint<T3>().FromArray(sublistB.ToArray())
            //};
            var sublistA = sourceArray.Take(dimensions[0]);
            var sublistB = sourceArray.Skip(dimensions[0]).Take(dimensions[1]);
            var sublistC = sourceArray.Skip(dimensions[0] + dimensions[1]).Take(dimensions[2]);
            return new Datapoint<T1, T2, T3>()
            {
                Value1 = (T1)(Datapoint<T1>)(new Datapoint<T1>().FromArray(sublistA.ToArray())),
                Value2 = (T2)(Datapoint<T2>)(new Datapoint<T2>().FromArray(sublistB.ToArray())),
                Value3 = (T3)(Datapoint<T3>)(new Datapoint<T3>().FromArray(sublistC.ToArray()))
            };
        }

        static Datapoint()
        {
            //dimensions = new Type[] { typeof(T1), typeof(T2), typeof(T3) }
            //    .Select(t => (t as IDatapoint).Dimensions)
            //    .ToArray();
            dimensions = new int[]
            {
                (default(Datapoint<T1>) as IDatapoint).Dimensions,
                (default(Datapoint<T2>) as IDatapoint).Dimensions,
                (default(Datapoint<T3>) as IDatapoint).Dimensions
            };
        }

        // We can convert to (or from) a more conventional Datapoint<Tone,Ttwo> type by grouping T1 & T2.
        // This should allow for as much expansion as necessary, chaining these together.
        public static implicit operator Datapoint<Datapoint<T1, T2>, T3>(Datapoint<T1, T2, T3> original)
        {
            return new Datapoint<Datapoint<T1, T2>, T3>()
            {
                Value1 = new Datapoint<T1, T2>()
                {
                    Value1 = original.Value1,
                    Value2 = original.Value2
                },
                Value2 = original.Value3
            };
        }
        public static implicit operator Datapoint<T1, T2, T3>(Datapoint<Datapoint<T1, T2>, T3> original)
        {
            return new Datapoint<T1, T2, T3>()
            {
                Value1 = original.Value1.Value1,
                Value2 = original.Value1.Value2,
                Value3 = original.Value2
            };
        }

        public static Datapoint<T1, T2, T3> operator-(Datapoint<T1,T2,T3> source) => (Datapoint<T1, T2, T3>)Operator.Negate((Datapoint<Datapoint<T1, T2>, T3>)source);
        public static Datapoint<T1, T2, T3> operator+(Datapoint<T1,T2,T3> first, Datapoint<T1,T2,T3> second) 
            => (Datapoint<T1, T2, T3>)Operator.Add((Datapoint<Datapoint<T1, T2>, T3>)first, (Datapoint<Datapoint<T1, T2>, T3>)second);
        public static Datapoint<T1, T2, T3> operator-(Datapoint<T1, T2, T3> first, Datapoint<T1, T2, T3> second)
            => (Datapoint<T1, T2, T3>)Operator.Subtract((Datapoint<Datapoint<T1, T2>, T3>)first, (Datapoint<Datapoint<T1, T2>, T3>)second);
        public static Datapoint<T1, T2, T3> operator*(Datapoint<T1, T2, T3> first, double second)
            => (Datapoint<T1, T2, T3>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, (float)second);
        public static Datapoint<T1, T2, T3> operator*(double second, Datapoint<T1, T2, T3> first)
            => (Datapoint<T1, T2, T3>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, (float)second);
        public static Datapoint<T1, T2, T3> operator *(Datapoint<T1, T2, T3> first, float second)
            => (Datapoint<T1, T2, T3>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, second);
        public static Datapoint<T1, T2, T3> operator *(float second, Datapoint<T1, T2, T3> first)
            => (Datapoint<T1, T2, T3>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, second);
        public static Datapoint<T1, T2, T3> operator *(Datapoint<T1, T2, T3> first, int second)
            => (Datapoint<T1, T2, T3>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, (float)second);
        public static Datapoint<T1, T2, T3> operator *(int second, Datapoint<T1, T2, T3> first)
            => (Datapoint<T1, T2, T3>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, (float)second);
        public static Datapoint<T1, T2, T3> operator /(Datapoint<T1, T2, T3> first, float second)
            => (Datapoint<T1, T2, T3>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, second);
        public static Datapoint<T1, T2, T3> operator/(Datapoint<T1, T2, T3> first, double second)
            => (Datapoint<T1, T2, T3>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, (float)second);
        public static Datapoint<T1, T2, T3> operator /(Datapoint<T1, T2, T3> first, int second)
            => (Datapoint<T1, T2, T3>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, T3>)first, (float)second);

        public static bool operator ==(Datapoint<T1, T2, T3> first, Datapoint<T1, T2, T3> second)
            => Operator.Equals(first.Value1, second.Value1) && Operator.Equals(first.Value2, second.Value2) && Operator.Equals(first.Value3, second.Value3);
        public static bool operator !=(Datapoint<T1, T2, T3> first, Datapoint<T1, T2, T3> second)
            => Operator.NotEqual(first.Value1, second.Value1) || Operator.NotEqual(first.Value2, second.Value2) || Operator.NotEqual(first.Value3, second.Value3);

        public override string ToString()
        {
            return $"\u1445 {Value1}|{Value2}|{Value3} \u1440";
        }

        // Serialization... Implementing ISerializable

        // First, we need an explicit parameterless constructor, which is technically not allowed for a struct; one with an optional parameter is, though.
        public Datapoint(T1 val1 = default(T1), T2 val2 = default(T2), T3 val3 = default(T3))
        {
            _v1 = (Operator.NotEqual(val1, default(T1)) ? val1 : Datapoint.DefaultOrIdentity<T1>());
            _v2 = (Operator.NotEqual(val2, default(T2)) ? val2 : Datapoint.DefaultOrIdentity<T2>());
            _v3 = (Operator.NotEqual(val3, default(T3)) ? val3 : Datapoint.DefaultOrIdentity<T3>());
        }

        // Otherwise, the special ISerializable constructor would be our only constructor, which would render things... sticky.
        public Datapoint(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            var valueArray = (float[])info.GetValue("valueArray", typeof(float[]));
            var fArray = (Datapoint<T1, T2, T3>)(new Datapoint<T1, T2, T3>()).FromArray(valueArray);
            _v1 = fArray.Value1;
            _v2 = fArray.Value2;
            _v3 = fArray.Value3;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            info.AddValue("valueArray", AsArray());
        }
    }

    [Serializable]
    public struct Datapoint<T1, T2, T3, T4> : IDatapoint, ISerializable
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
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
        //private Nullable<T1> _v1;
        public T2 Value2
        {
            get
            {
                _v2 = (_v2 != null && _v2.HasValue) ? _v2.Value : Datapoint.DefaultOrIdentity<T2>();
                return _v2.Value;
            }
            set { _v2 = value; }
        }
        private T3? _v3;
        //private Nullable<T3> _v3;
        public T3 Value3
        {
            get
            {
                _v3 = (_v3 != null && _v3.HasValue) ? _v3.Value : Datapoint.DefaultOrIdentity<T3>();
                return _v3.Value;
            }
            set { _v3 = value; }
        }
        private T4? _v4;
        //private Nullable<T3> _v3;
        public T4 Value4
        {
            get
            {
                _v4 = (_v4 != null && _v4.HasValue) ? _v4.Value : Datapoint.DefaultOrIdentity<T4>();
                return _v4.Value;
            }
            set { _v4 = value; }
        }
        private static int[] dimensions { get; set; }
        public static int Dimensions
        {
            get { return dimensions.Sum(); }
        }
        int IDatapoint.Dimensions { get { return Dimensions; } }

        public float[] AsArray()
        {
            var a1 = Datapoint.From(Value1).AsArray();
            var a2 = Datapoint.From(Value2).AsArray();
            var a3 = Datapoint.From(Value3).AsArray();
            var a4 = Datapoint.From(Value4).AsArray();
            var result = a1.Concat(a2).Concat(a3).Concat(a4).ToArray();
            return result;
        }

        public float Magnitude()
        {
            return ((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)this).Magnitude();
        }

        public IDatapoint FromArray(float[] sourceArray)
        {
            //var sublistA = sourceArray.Take(dimensions[0] + dimensions[1]);
            //var sublistB = sourceArray.Skip(dimensions[0] + dimensions[1])
            //                          .Take(dimensions[2]);
            //return (Datapoint<T1, T2, T3>)new Datapoint<Datapoint<T1, T2>, T3>()
            //{
            //    Value1 = new Datapoint<T1, T2>().FromArray(sublistA.ToArray()),
            //    Value2 = (T3)new Datapoint<T3>().FromArray(sublistB.ToArray())
            //};
            var sublistA = sourceArray.Take(dimensions[0]);
            var sublistB = sourceArray.Skip(dimensions[0]).Take(dimensions[1]);
            var sublistC = sourceArray.Skip(dimensions[0] + dimensions[1]).Take(dimensions[2]);
            var sublistD = sourceArray.Skip(dimensions[0] + dimensions[1] + dimensions[2]).Take(dimensions[3]);
            return new Datapoint<T1, T2, T3, T4>()
            {
                Value1 = (T1)(Datapoint<T1>)(new Datapoint<T1>().FromArray(sublistA.ToArray())),
                Value2 = (T2)(Datapoint<T2>)(new Datapoint<T2>().FromArray(sublistB.ToArray())),
                Value3 = (T3)(Datapoint<T3>)(new Datapoint<T3>().FromArray(sublistC.ToArray())),
                Value4 = (T4)(Datapoint<T4>)(new Datapoint<T4>().FromArray(sublistD.ToArray()))
            };
        }

        static Datapoint()
        {
            //dimensions = new Type[] { typeof(T1), typeof(T2), typeof(T3) }
            //    .Select(t => (t as IDatapoint).Dimensions)
            //    .ToArray();
            dimensions = new int[]
            {
                (default(Datapoint<T1>) as IDatapoint).Dimensions,
                (default(Datapoint<T2>) as IDatapoint).Dimensions,
                (default(Datapoint<T3>) as IDatapoint).Dimensions,
                (default(Datapoint<T4>) as IDatapoint).Dimensions
            };
        }

        // We can convert to (or from) a more conventional Datapoint<Tone,Ttwo> type by grouping T1 & T2.
        // This should allow for as much expansion as necessary, chaining these together.
        public static implicit operator Datapoint<Datapoint<T1, T2>, Datapoint<T3,T4>>(Datapoint<T1, T2, T3, T4> original)
        {
            return new Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>()
            {
                Value1 = new Datapoint<T1, T2>()
                {
                    Value1 = original.Value1,
                    Value2 = original.Value2
                },
                Value2 = new Datapoint<T3, T4>()
                {
                    Value1 = original.Value3,
                    Value2 = original.Value4
                }
            };
        }
        public static implicit operator Datapoint<T1, T2, T3, T4>(Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>> original)
        {
            return new Datapoint<T1, T2, T3, T4>()
            {
                Value1 = original.Value1.Value1,
                Value2 = original.Value1.Value2,
                Value3 = original.Value2.Value1,
                Value4 = original.Value2.Value2
            };
        }

        public static Datapoint<T1, T2, T3, T4> operator -(Datapoint<T1, T2, T3, T4> source) => (Datapoint<T1, T2, T3, T4>)Operator.Negate((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)source);
        public static Datapoint<T1, T2, T3, T4> operator +(Datapoint<T1, T2, T3, T4> first, Datapoint<T1, T2, T3, T4> second)
            => (Datapoint<T1, T2, T3, T4>)Operator.Add((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)second);
        public static Datapoint<T1, T2, T3, T4> operator -(Datapoint<T1, T2, T3, T4> first, Datapoint<T1, T2, T3, T4> second)
            => (Datapoint<T1, T2, T3, T4>)Operator.Subtract((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)second);
        public static Datapoint<T1, T2, T3, T4> operator *(Datapoint<T1, T2, T3, T4> first, double second)
            => (Datapoint<T1, T2, T3, T4>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4> operator *(double second, Datapoint<T1, T2, T3, T4> first)
            => (Datapoint<T1, T2, T3, T4>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4> operator *(Datapoint<T1, T2, T3, T4> first, float second)
            => (Datapoint<T1, T2, T3, T4>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, second);
        public static Datapoint<T1, T2, T3, T4> operator *(float second, Datapoint<T1, T2, T3, T4> first)
            => (Datapoint<T1, T2, T3, T4>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, second);
        public static Datapoint<T1, T2, T3, T4> operator *(Datapoint<T1, T2, T3, T4> first, int second)
            => (Datapoint<T1, T2, T3, T4>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4> operator *(int second, Datapoint<T1, T2, T3, T4> first)
            => (Datapoint<T1, T2, T3, T4>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4> operator /(Datapoint<T1, T2, T3, T4> first, float second)
            => (Datapoint<T1, T2, T3, T4>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, second);
        public static Datapoint<T1, T2, T3, T4> operator /(Datapoint<T1, T2, T3, T4> first, double second)
            => (Datapoint<T1, T2, T3, T4>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4> operator /(Datapoint<T1, T2, T3, T4> first, int second)
            => (Datapoint<T1, T2, T3, T4>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4>>)first, (float)second);

        public static bool operator ==(Datapoint<T1, T2, T3, T4> first, Datapoint<T1, T2, T3, T4> second)
            => Operator.Equals(first.Value1, second.Value1) && Operator.Equals(first.Value2, second.Value2) && Operator.Equals(first.Value3, second.Value3) && Operator.Equals(first.Value4, second.Value4);
        public static bool operator !=(Datapoint<T1, T2, T3, T4> first, Datapoint<T1, T2, T3, T4> second)
            => Operator.NotEqual(first.Value1, second.Value1) || Operator.NotEqual(first.Value2, second.Value2) || Operator.NotEqual(first.Value3, second.Value3) || Operator.NotEqual(first.Value4, second.Value4);

        public override string ToString()
        {
            return $"\u1445 {Value1}|{Value2}|{Value3}|{Value4} \u1440";
        }

        // Serialization... Implementing ISerializable

        // First, we need an explicit parameterless constructor, which is technically not allowed for a struct; one with an optional parameter is, though.
        public Datapoint(T1 val1 = default(T1), T2 val2 = default(T2), T3 val3 = default(T3), T4 val4 = default(T4))
        {
            _v1 = (Operator.NotEqual(val1, default(T1)) ? val1 : Datapoint.DefaultOrIdentity<T1>());
            _v2 = (Operator.NotEqual(val2, default(T2)) ? val2 : Datapoint.DefaultOrIdentity<T2>());
            _v3 = (Operator.NotEqual(val3, default(T3)) ? val3 : Datapoint.DefaultOrIdentity<T3>());
            _v4 = (Operator.NotEqual(val4, default(T4)) ? val4 : Datapoint.DefaultOrIdentity<T4>());
        }

        // Otherwise, the special ISerializable constructor would be our only constructor, which would render things... sticky.
        public Datapoint(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            var valueArray = (float[])info.GetValue("valueArray", typeof(float[]));
            var fArray = (Datapoint<T1, T2, T3, T4>)(new Datapoint<T1, T2, T3, T4>()).FromArray(valueArray);
            _v1 = fArray.Value1;
            _v2 = fArray.Value2;
            _v3 = fArray.Value3;
            _v4 = fArray.Value4;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            info.AddValue("valueArray", AsArray());
        }
    }

    // Seems crazy, but yes, we actually do want a T5 version, for linear accel + gravity + gyroscope + orientation + time. Yoicks!
    [Serializable]
    public struct Datapoint<T1, T2, T3, T4, T5> : IDatapoint, ISerializable
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
    {
        private T1? _v1;
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
        public T2 Value2
        {
            get
            {
                _v2 = (_v2 != null && _v2.HasValue) ? _v2.Value : Datapoint.DefaultOrIdentity<T2>();
                return _v2.Value;
            }
            set { _v2 = value; }
        }
        private T3? _v3;
        public T3 Value3
        {
            get
            {
                _v3 = (_v3 != null && _v3.HasValue) ? _v3.Value : Datapoint.DefaultOrIdentity<T3>();
                return _v3.Value;
            }
            set { _v3 = value; }
        }
        private T4? _v4;
        public T4 Value4
        {
            get
            {
                _v4 = (_v4 != null && _v4.HasValue) ? _v4.Value : Datapoint.DefaultOrIdentity<T4>();
                return _v4.Value;
            }
            set { _v4 = value; }
        }
        private T5? _v5;
        public T5 Value5
        {
            get
            {
                _v5 = (_v5 != null && _v5.HasValue) ? _v5.Value : Datapoint.DefaultOrIdentity<T5>();
                return _v5.Value;
            }
            set { _v5 = value; }
        }
        private static int[] dimensions { get; set; }
        public static int Dimensions
        {
            get { return dimensions.Sum(); }
        }
        int IDatapoint.Dimensions { get { return Dimensions; } }

        public float[] AsArray()
        {
            var a1 = Datapoint.From(Value1).AsArray();
            var a2 = Datapoint.From(Value2).AsArray();
            var a3 = Datapoint.From(Value3).AsArray();
            var a4 = Datapoint.From(Value4).AsArray();
            var a5 = Datapoint.From(Value5).AsArray();
            var result = a1.Concat(a2).Concat(a3).Concat(a4).Concat(a5).ToArray();
            return result;
        }

        public float Magnitude()
        {
            return ((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)this).Magnitude();
        }

        public IDatapoint FromArray(float[] sourceArray)
        {
            //var sublistA = sourceArray.Take(dimensions[0] + dimensions[1]);
            //var sublistB = sourceArray.Skip(dimensions[0] + dimensions[1])
            //                          .Take(dimensions[2]);
            //return (Datapoint<T1, T2, T3>)new Datapoint<Datapoint<T1, T2>, T3>()
            //{
            //    Value1 = new Datapoint<T1, T2>().FromArray(sublistA.ToArray()),
            //    Value2 = (T3)new Datapoint<T3>().FromArray(sublistB.ToArray())
            //};
            var sublistA = sourceArray.Take(dimensions[0]);
            var sublistB = sourceArray.Skip(dimensions[0]).Take(dimensions[1]);
            var sublistC = sourceArray.Skip(dimensions[0] + dimensions[1]).Take(dimensions[2]);
            var sublistD = sourceArray.Skip(dimensions[0] + dimensions[1] + dimensions[2]).Take(dimensions[3]);
            var sublistE = sourceArray.Skip(dimensions[0] + dimensions[1] + dimensions[2] + dimensions[3]).Take(dimensions[4]);
            return new Datapoint<T1, T2, T3, T4, T5>()
            {
                Value1 = (T1)(Datapoint<T1>)(new Datapoint<T1>().FromArray(sublistA.ToArray())),
                Value2 = (T2)(Datapoint<T2>)(new Datapoint<T2>().FromArray(sublistB.ToArray())),
                Value3 = (T3)(Datapoint<T3>)(new Datapoint<T3>().FromArray(sublistC.ToArray())),
                Value4 = (T4)(Datapoint<T4>)(new Datapoint<T4>().FromArray(sublistD.ToArray())),
                Value5 = (T5)(Datapoint<T5>)(new Datapoint<T5>().FromArray(sublistE.ToArray()))
            };
        }

        static Datapoint()
        {
            //dimensions = new Type[] { typeof(T1), typeof(T2), typeof(T3) }
            //    .Select(t => (t as IDatapoint).Dimensions)
            //    .ToArray();
            dimensions = new int[]
            {
                (default(Datapoint<T1>) as IDatapoint).Dimensions,
                (default(Datapoint<T2>) as IDatapoint).Dimensions,
                (default(Datapoint<T3>) as IDatapoint).Dimensions,
                (default(Datapoint<T4>) as IDatapoint).Dimensions,
                (default(Datapoint<T5>) as IDatapoint).Dimensions
            };
        }

        // We can convert to (or from) a more conventional Datapoint<Tone,Ttwo> type by grouping T1 & T2.
        // This should allow for as much expansion as necessary, chaining these together.
        public static implicit operator Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>(Datapoint<T1, T2, T3, T4, T5> original)
        {
            return new Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>()
            {
                Value1 = new Datapoint<T1, T2>()
                {
                    Value1 = original.Value1,
                    Value2 = original.Value2
                },
                Value2 = new Datapoint<T3, T4, T5>()
                {
                    Value1 = original.Value3,
                    Value2 = original.Value4,
                    Value3 = original.Value5
                }
            };
        }
        public static implicit operator Datapoint<T1, T2, T3, T4, T5>(Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>> original)
        {
            return new Datapoint<T1, T2, T3, T4, T5>()
            {
                Value1 = original.Value1.Value1,
                Value2 = original.Value1.Value2,
                Value3 = original.Value2.Value1,
                Value4 = original.Value2.Value2,
                Value5 = original.Value2.Value3,
            };
        }

        public static Datapoint<T1, T2, T3, T4, T5> operator -(Datapoint<T1, T2, T3, T4, T5> source) => (Datapoint<T1, T2, T3, T4, T5>)Operator.Negate((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)source);
        public static Datapoint<T1, T2, T3, T4, T5> operator +(Datapoint<T1, T2, T3, T4, T5> first, Datapoint<T1, T2, T3, T4, T5> second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.Add((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator -(Datapoint<T1, T2, T3, T4, T5> first, Datapoint<T1, T2, T3, T4, T5> second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.Subtract((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator *(Datapoint<T1, T2, T3, T4, T5> first, double second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator *(double second, Datapoint<T1, T2, T3, T4, T5> first)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator *(Datapoint<T1, T2, T3, T4, T5> first, float second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, second);
        public static Datapoint<T1, T2, T3, T4, T5> operator *(float second, Datapoint<T1, T2, T3, T4, T5> first)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, second);
        public static Datapoint<T1, T2, T3, T4, T5> operator *(Datapoint<T1, T2, T3, T4, T5> first, int second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator *(int second, Datapoint<T1, T2, T3, T4, T5> first)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.MultiplyAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator /(Datapoint<T1, T2, T3, T4, T5> first, float second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, second);
        public static Datapoint<T1, T2, T3, T4, T5> operator /(Datapoint<T1, T2, T3, T4, T5> first, double second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (float)second);
        public static Datapoint<T1, T2, T3, T4, T5> operator /(Datapoint<T1, T2, T3, T4, T5> first, int second)
            => (Datapoint<T1, T2, T3, T4, T5>)Operator.DivideAlternative((Datapoint<Datapoint<T1, T2>, Datapoint<T3, T4, T5>>)first, (float)second);

        public static bool operator ==(Datapoint<T1, T2, T3, T4, T5> first, Datapoint<T1, T2, T3, T4, T5> second)
            => Operator.Equals(first.Value1, second.Value1) && Operator.Equals(first.Value2, second.Value2) && Operator.Equals(first.Value3, second.Value3) && Operator.Equals(first.Value4, second.Value4) && Operator.Equals(first.Value5, second.Value5);
        public static bool operator !=(Datapoint<T1, T2, T3, T4, T5> first, Datapoint<T1, T2, T3, T4, T5> second)
            => Operator.NotEqual(first.Value1, second.Value1) || Operator.NotEqual(first.Value2, second.Value2) || Operator.NotEqual(first.Value3, second.Value3) || Operator.NotEqual(first.Value4, second.Value4) || Operator.NotEqual(first.Value5, second.Value5);

        public override string ToString()
        {
            return $"\u1445 {Value1}|{Value2}|{Value3}|{Value4}|{Value5} \u1440";
        }

        // Serialization... Implementing ISerializable

        // First, we need an explicit parameterless constructor, which is technically not allowed for a struct; one with an optional parameter is, though.
        public Datapoint(T1 val1 = default(T1), T2 val2 = default(T2), T3 val3 = default(T3), T4 val4 = default(T4), T5 val5 = default(T5))
        {
            _v1 = (Operator.NotEqual(val1, default(T1)) ? val1 : Datapoint.DefaultOrIdentity<T1>());
            _v2 = (Operator.NotEqual(val2, default(T2)) ? val2 : Datapoint.DefaultOrIdentity<T2>());
            _v3 = (Operator.NotEqual(val3, default(T3)) ? val3 : Datapoint.DefaultOrIdentity<T3>());
            _v4 = (Operator.NotEqual(val4, default(T4)) ? val4 : Datapoint.DefaultOrIdentity<T4>());
            _v5 = (Operator.NotEqual(val5, default(T5)) ? val5 : Datapoint.DefaultOrIdentity<T5>());
        }

        // Otherwise, the special ISerializable constructor would be our only constructor, which would render things... sticky.
        public Datapoint(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            var valueArray = (float[])info.GetValue("valueArray", typeof(float[]));
            var fArray = (Datapoint<T1, T2, T3, T4, T5>)(new Datapoint<T1, T2, T3, T4, T5>()).FromArray(valueArray);
            _v1 = fArray.Value1;
            _v2 = fArray.Value2;
            _v3 = fArray.Value3;
            _v4 = fArray.Value4;
            _v5 = fArray.Value5;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            info.AddValue("valueArray", AsArray());
        }
    }
}