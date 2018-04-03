using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MiscUtil;
using System.Reflection;

namespace Atropos.DataStructures
{
    public interface IDatapoint
    {
        int Dimensions { get; }
        float[] AsArray();
        float Magnitude();
        IDatapoint FromArray(float[] sourceArray);
    }

    public interface IDatapoint<T> : IDatapoint
        where T : struct
    {
        T Value { get; set; }
        //new IDatapoint<T> FromArray(float[] sourceArray);
    }

    public interface IDatapoint<T1, T2> : IDatapoint 
        where T1 : struct 
        where T2 : struct
    {
        T1 Value1 { get; set; }
        T2 Value2 { get; set; }
        //new IDatapoint<T1, T2> FromArray(float[] sourceArray);
    }

    public static class Datapoint
    {
        // Since we mostly use Quaternions as *rotations*, the zero Quaternion is a STUPID default value.
        // The identity Quat makes much more sense in that role.  This function is used to get the *right* default(T)
        // for our purposes.
        public static T DefaultOrIdentity<T>()
        {
            if (typeof(T) == typeof(Quaternion))
                return Operator.Convert<Quaternion, T>(Quaternion.Identity);
            else return default(T);
        }

        // Used when you want to guarantee that the result is a Datapoint type, but the incoming
        // value might or might not be one, and we'd rather wrap it only if we have to.
        public static IDatapoint From<T>(T val)
            where T : struct
        {
            if (typeof(T).Implements("IDatapoint")) return (IDatapoint)val;
            else return new Datapoint<T>() { Value = val };
        }

        public static float[] AsArrayFrom<T>(T val) where T : struct
        {
            return From(val).AsArray();
        }

        public static double[] AsDblArrayFrom<T>(T val) where T : struct
        {
            return AsArrayFrom(val).Cast<float, double>().ToArray();
        }

        public static Datapoint<T1, T2> From<T1, T2>(T1 val1, T2 val2)
            where T1 : struct
            where T2 : struct
        {
            return new Datapoint<T1, T2>()
            {
                Value1 = val1,
                Value2 = val2
            };
        }

        public static Datapoint<T1, T2> From<T1, T2>(IDatapoint val) 
            where T1 : struct 
            where T2 : struct
        {
            if (!(val is Datapoint<T1, T2>)) throw new ArgumentException($"Cannot create a Datapoint<{typeof(T1).Name},{typeof(T2).Name}> from the provided {val.GetType().Name}!");

            return Operator.Convert<IDatapoint, Datapoint<T1, T2>>(val);
        }
    }

    //public static class VectorOperator<T>
    //{
    //    public static Func<T, float> LengthOf;
    //    public static Func<T, float> LengthSquaredOf;
    //    public static Func<T, T, float> Dot;
    //    static VectorOperator()
    //    {
    //        var anyT = default(T);
    //        if (anyT.CanConvert<T, Vector2>())
    //        {
    //            LengthOf = (t) => Operator.Convert<T, Vector2>(t).Length();
    //            LengthSquaredOf = (t) => Operator.Convert<T, Vector2>(t).LengthSquared();
    //            Dot = (t, u) => Vector2.Dot(Operator.Convert<T, Vector2>(t), Operator.Convert<T, Vector2>(u));
    //        }
    //        else if (anyT.CanConvert<T, Vector3>())
    //        {
    //            LengthOf = (t) => Operator.Convert<T, Vector3>(t).Length();
    //            LengthSquaredOf = (t) => Operator.Convert<T, Vector3>(t).LengthSquared();
    //            Dot = (t, u) => Vector3.Dot(Operator.Convert<T, Vector3>(t), Operator.Convert<T, Vector3>(u));
    //        }
    //        else if (anyT.CanConvert<T, Vector4>())
    //        {
    //            LengthOf = (t) => Operator.Convert<T, Vector4>(t).Length();
    //            LengthSquaredOf = (t) => Operator.Convert<T, Vector4>(t).LengthSquared();
    //            Dot = (t, u) => Vector4.Dot(Operator.Convert<T, Vector4>(t), Operator.Convert<T, Vector4>(u));
    //        }
    //        else if (anyT.CanConvert<T, Quaternion>())
    //        {
    //            LengthOf = (t) => Operator.Convert<T, Quaternion>(t).Length();
    //            LengthSquaredOf = (t) => Operator.Convert<T, Quaternion>(t).LengthSquared();
    //            Dot = (t, u) => Quaternion.Dot(Operator.Convert<T, Quaternion>(t), Operator.Convert<T, Quaternion>(u));
    //        }
    //        else if (anyT.CanConvert<T, float>())
    //        {
    //            LengthOf = (t) => Math.Abs(Operator.Convert<T, float>(t));
    //            LengthSquaredOf = (t) => Operator.Convert<T, float>(Operator.Multiply(t, t));
    //            Dot = (t, u) => { throw new InvalidOperationException($"Cannot take dot product of two {typeof(T).Name}s!"); };
    //        }
    //        //else if (typeof(T).Implements<IVectorCluster>())
    //        //{
    //        //    LengthOf = (t) => (t as IVectorCluster).Length();
    //        //    LengthSquaredOf = (t) => (t as IVectorCluster).LengthSquared();
    //        //    Dot = (t, u) => { throw new InvalidOperationException($"Cannot inherently take dot product of two {typeof(T).Name}s!"); };
    //        //}
    //        else
    //        {
    //            LengthOf = (t) => { throw new InvalidOperationException($"Cannot take Length of {typeof(T).Name}!"); };
    //            LengthSquaredOf = (t) => { throw new InvalidOperationException($"Cannot take LengthSquared of {typeof(T).Name}!"); };
    //            Dot = (t, u) => { throw new InvalidOperationException($"Cannot take dot product of two {typeof(T).Name}s!"); };
    //        }
    //    }
    //}
    //public static class VectorOperator
    //{
    //    public static float Length<T>(T target)
    //    {
    //        return VectorOperator<T>.LengthOf(target);
    //    }
    //    public static float LengthSquared<T>(T target)
    //    {
    //        return VectorOperator<T>.LengthSquaredOf(target);
    //    }
    //    public static float Dot<T>(T first, T second)
    //    {
    //        return VectorOperator<T>.Dot(first, second);
    //    }
    //}
}