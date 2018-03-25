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
    }
    
}