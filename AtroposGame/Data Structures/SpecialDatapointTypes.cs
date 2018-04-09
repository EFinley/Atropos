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
using Android.Hardware;
using System.Threading;
using MiscUtil;

namespace Atropos.DataStructures
{
    /// <summary>
    /// A utility object intended to hold two vector quantities, such as acceleration and gyroscope data, or linear
    /// acceleration & gravity, tightly coupled.  Used particularly in things like the Machine Learning namespace,
    /// see for example <see cref="Machine_Learning.Feature{T}"/>. 
    /// </summary>
    public struct Vector6
    {
        public Vector3 V1;
        public Vector3 V2;
    }

    public class DatapointSpecialVariants
    {
        ///// <summary>
        ///// Special-purpose type... distinct from a <see cref="Datapoint{T1, T2}"/> where each of T1 and T2 is a <see cref="Datapoint{float,float}"/>,
        ///// because... reasons.  Which I think don't actually obtain here. Dammit.
        ///// </summary>
        //public struct Datapoint4f : IDatapoint<Datapoint<Datapoint<float>, Datapoint<float>>, Datapoint<Datapoint<float>, Datapoint<float>>>
        //{
        //    public Datapoint<Datapoint<float>, Datapoint<float>> Value1
        //    {
        //        get; set;
        //    }
        //    public Datapoint<Datapoint<float>, Datapoint<float>> Value2
        //    {
        //        get; set;
        //    }

        //    public static int Dimensions = 4;
        //    int IDatapoint.Dimensions => 4;

        //    float[] IDatapoint.AsArray()
        //    {
        //        return new float[] { Value1.Value1, Value1.Value2, Value2.Value1, Value2.Value1 };
        //    }

        //    IDatapoint IDatapoint.FromArray(float[] sourceArray)
        //    {
        //        return new Datapoint<Datapoint<Datapoint<float>, Datapoint<float>>, Datapoint<Datapoint<float>, Datapoint<float>>>()
        //        {
        //            Value1 = new Datapoint<Datapoint<float>, Datapoint<float>>()
        //            {
        //                Value1 = new Datapoint<float>() { Value = sourceArray[0] },
        //                Value2 = new Datapoint<float>() { Value = sourceArray[1] }
        //            },
        //            Value2 = new Datapoint<Datapoint<float>, Datapoint<float>>()
        //            {
        //                Value1 = new Datapoint<float>() { Value = sourceArray[2] },
        //                Value2 = new Datapoint<float>() { Value = sourceArray[3] }
        //            }
        //        };
        //    }

        //    float IDatapoint.Magnitude()
        //    {
        //        return (float)Math.Sqrt(Value1.Magnitude() * Value1.Magnitude() + Value2.Magnitude() * Value2.Magnitude());
        //    }
        //}

        public interface IInterval
        {
            double Interval { get; }
        }

        public interface IProxyType
        {
            Type ProxyType { get; }
        }

        [Serializable]
        public struct DatapointKitchenSink : IDatapoint, IInterval, IProxyType
        {
            public Vector3 LinAccel { get => Values.Value1; }
            public Vector3 Gravity { get => Values.Value2; }
            public Vector3 Gyro { get => Values.Value3; }
            public Quaternion Orientation { get => Values.Value4; }
            public double Interval { get => Values.Value5; }

            public Datapoint<Vector3, Vector3, Vector3, Quaternion, double> Values;
            public Type ProxyType { get => typeof(Datapoint<Vector3, Vector3, Vector3, Quaternion, double>); }

            public static int Dimensions => Datapoint<Vector3, Vector3, Vector3, Quaternion, double>.Dimensions; // Equals fourteen, incidentally.
            int IDatapoint.Dimensions => Dimensions;

            public float[] AsArray()
            {
                return Values.AsArray();
            }

            public IDatapoint FromArray(float[] sourceArray)
            {
                return new DatapointKitchenSink()
                {
                    Values = (Datapoint <Vector3, Vector3, Vector3, Quaternion, double>)new Datapoint<Vector3, Vector3, Vector3, Quaternion, double>().FromArray(sourceArray)
                };
            }

            public float Magnitude()
            {
                return Values.Magnitude();
            }

            public static implicit operator DatapointKitchenSink(Datapoint<Vector3, Vector3, Vector3, Quaternion, double> dpoint)
            {
                return new DatapointKitchenSink { Values = dpoint };
            }

            public static implicit operator Datapoint<Vector3, Vector3, Vector3, Quaternion, double>(DatapointKitchenSink dsink)
            {
                return dsink.Values;
            }

            public static DatapointKitchenSink operator -(DatapointKitchenSink source) => (DatapointKitchenSink)Operator.Negate((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)source);
            public static DatapointKitchenSink operator +(DatapointKitchenSink first, DatapointKitchenSink second)
                => (DatapointKitchenSink)Operator.Add((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)second);
            public static DatapointKitchenSink operator -(DatapointKitchenSink first, DatapointKitchenSink second)
                => (DatapointKitchenSink)Operator.Subtract((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)second);
            public static DatapointKitchenSink operator *(DatapointKitchenSink first, double second)
                => (DatapointKitchenSink)Operator.MultiplyAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (float)second);
            public static DatapointKitchenSink operator *(double second, DatapointKitchenSink first)
                => (DatapointKitchenSink)Operator.MultiplyAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (float)second);
            public static DatapointKitchenSink operator *(DatapointKitchenSink first, float second)
                => (DatapointKitchenSink)Operator.MultiplyAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, second);
            public static DatapointKitchenSink operator *(float second, DatapointKitchenSink first)
                => (DatapointKitchenSink)Operator.MultiplyAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, second);
            public static DatapointKitchenSink operator *(DatapointKitchenSink first, int second)
                => (DatapointKitchenSink)Operator.MultiplyAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (float)second);
            public static DatapointKitchenSink operator *(int second, DatapointKitchenSink first)
                => (DatapointKitchenSink)Operator.MultiplyAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (float)second);
            public static DatapointKitchenSink operator /(DatapointKitchenSink first, float second)
                => (DatapointKitchenSink)Operator.DivideAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, second);
            public static DatapointKitchenSink operator /(DatapointKitchenSink first, double second)
                => (DatapointKitchenSink)Operator.DivideAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (float)second);
            public static DatapointKitchenSink operator /(DatapointKitchenSink first, int second)
                => (DatapointKitchenSink)Operator.DivideAlternative((Datapoint<Vector3, Vector3, Vector3, Quaternion, double>)first, (float)second);

            public static bool operator ==(DatapointKitchenSink first, DatapointKitchenSink second)
                => Operator.Equals(first.LinAccel, second.LinAccel) && Operator.Equals(first.Gravity, second.Gravity) && Operator.Equals(first.Gyro, second.Gyro) && Operator.Equals(first.Orientation, second.Orientation) && Operator.Equals(first.Interval, second.Interval);
            public static bool operator !=(DatapointKitchenSink first, DatapointKitchenSink second)
                => Operator.NotEqual(first.LinAccel, second.LinAccel) || Operator.Equals(first.Gravity, second.Gravity) || Operator.Equals(first.Gyro, second.Gyro) || Operator.Equals(first.Orientation, second.Orientation) || Operator.Equals(first.Interval, second.Interval);

        }
    }
    
}