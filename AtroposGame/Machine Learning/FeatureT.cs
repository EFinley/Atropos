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
using Android.Graphics;

using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Math;
using Accord.Statistics.Kernels;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Numerics;

using Vector3 = System.Numerics.Vector3;
using Atropos.DataStructures;

namespace Atropos.Machine_Learning
{
    /// <summary>
    /// Feature vector: a wrapper around any value type to provide access for the 
    /// machine learning engine to use it as the underlying type.  This can be 
    /// either a built-in type like <see cref="Vector2"/>, or a user-defined struct
    /// like <see cref="Vector6"/>.
    /// <para>For this struct to wrap a particular type, a few delegate function
    /// slots need to be filled - see <see cref="toArray"/> & <see cref="crosslinkFunc"/>.  
    /// Also see <seealso cref="SerializableFeature{T}"/> if needed.</para> 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct Feature<T> where T : struct
    {
        public T Content { get; set; }
        public static int Dimensions { get; private set; }

        // Private delegate which all instances of a given Feature<T> class (such as, say, Feature<Vector2>) share, and MUST be assigned in static ctor.
        internal static Func<T, double[]> toArray;

        // Public accessor which retrieves the delegate's result.
        public double[] AsArray { get { return toArray?.Invoke(Content) ?? new double[Dimensions]; } }

        // A little more obscure (but will be needed elsewhere)... for this data type, once we've worked out the
        // *per-axis* standard deviations [aka scaling factors], how do we turn that into *crosslinked* standard deviations?
        // (Ex: In the original example, we scaled all X values per the X standard dev. and all Y values per the Y sigma.  But
        // this meant that a vertical line with a small X extent amplified any wiggle in X to be just as important as that in Y!)
        private static Func<double[], double[]> crosslinkFunc;
        public static double[] CrosslinkScalingFactors(double[] perAxisScalings)
        {
            return crosslinkFunc?.Invoke(perAxisScalings);
        }

        // Static constructor which *creates* the appropriate function and assigns it to the delegate.  All it must do is turn a T into an array of doubles, however works.
        static Feature()
        {
            if (typeof(T) == typeof(Vector2))
            {
                Dimensions = 2;
                toArray = (V2) => { var C = MiscUtil.Operator.Convert<T, Vector2>(V2); return new double[] { C.X, C.Y }; };
                crosslinkFunc = (axisMeans) => new double[] { axisMeans.Max(), axisMeans.Max() };
                return;
            }
            if (typeof(T) == typeof(Vector3))
            {
                Dimensions = 3;
                toArray = (V3) => { var C = MiscUtil.Operator.Convert<T, Vector3>(V3); return new double[] { C.X, C.Y, C.Z }; };
                crosslinkFunc = (axisMeans) => new double[] { axisMeans.Max(), axisMeans.Max(), axisMeans.Max() };
                return;
            }
            if (typeof(T) == typeof(Vector6))
            {
                Dimensions = 6;
                toArray = (V6) => 
                {
                    var C = MiscUtil.Operator.Convert<T, Vector6>(V6); return new double[] { C.V1.X, C.V1.Y, C.V1.Z, C.V2.X, C.V2.Y, C.V2.Z };
                };
                crosslinkFunc = (axisMeans) =>
                {
                    var accelScaling = new double[] { axisMeans[0], axisMeans[1], axisMeans[2] }.Max();
                    return new double[] { accelScaling, accelScaling, accelScaling, axisMeans[3], axisMeans[4], axisMeans[5] }; // Gravity is gravity!  No scaling ought be needed.
                };
                return;
            }
            // Add other clauses here, similarly. NOTE - for serialization, you will also need to add them to SerializableFeature<T>, below.
            else throw new TypeInitializationException($"Atropos.Feature<T>", new Exception($"Unable to look up proper arrayification form for a Feature<{typeof(T).Name}>, in static ctor."));
        }

        // And finally a way to get a Feature<T> out of a T.
        public static explicit operator Feature<T>(T source) => new Feature<T>() { Content = source };

        // Moreover, the first two entries have special significance... because we assume that they will form two axes (so we can draw pretty pictures with them).
        // Ditto the first three, if present.
        public static implicit operator Point(Feature<T> source)
        {
            return new Point((int)source.AsArray.ElementAtOrDefault(0), (int)source.AsArray.ElementAtOrDefault(1));
        }
        public static implicit operator Vector2(Feature<T> source)
        {
            return new Vector2((float)source.AsArray.ElementAtOrDefault(0), (float)source.AsArray.ElementAtOrDefault(1));
        }
        public static implicit operator System.Numerics.Vector3(Feature<T> source)
        {
            return new System.Numerics.Vector3((float)source.AsArray.ElementAtOrDefault(0), (float)source.AsArray.ElementAtOrDefault(1), (float)source.AsArray.ElementAtOrDefault(2));
        }

        // And, since we don't directly inherit the properties of Point or Vector2, let's make .X and .Y available directly here.
        public float X { get { return (float)this.AsArray.ElementAtOrDefault(0); } }
        public float Y { get { return (float)this.AsArray.ElementAtOrDefault(1); } }
        public float Z { get { return (float)this.AsArray.ElementAtOrDefault(2); } }
    }

    /// <summary>
    /// Since the various System.Numerics Vector classes are NOT marked as Serializable, we need a workaround.
    /// To use, explicitly cast into and out of this type before serialization or after deserialization.  You
    /// should never need to actually construct an instance of this type external to those two activities.
    /// </summary>
    /// <typeparam name="T">The type of feature - see <seealso cref="Feature{T}"/>.</typeparam>
    [Serializable]
    public struct SerializableFeature<T> where T : struct
    {
        public double[] Values;

        // Private delegate which all instances of a given Feature<T> class (such as, say, Feature<Vector2>) share, and MUST be assigned in static ctor.
        //[NonSerialized] private static Func<T, double[]> ToArray;
        [NonSerialized] private static Func<double[], T> FromArray;

        static SerializableFeature()
        {
            if (typeof(T) == typeof(Vector2))
            {
                FromArray = (vals) => { return MiscUtil.Operator.Convert<Vector2, T>(new Vector2((float)vals[0], (float)vals[1])); };
                return;
            }
            if (typeof(T) == typeof(Vector3))
            {
                FromArray = (vals) => { return MiscUtil.Operator.Convert<Vector3, T>(new Vector3((float)vals[0], (float)vals[1], (float)vals[2])); };
                return;
            }
            if (typeof(T) == typeof(Vector6))
            {
                FromArray = (vals) => 
                {
                    return MiscUtil.Operator.Convert<Vector6, T>(
                        new Vector6()
                        {
                            V1 = new Vector3((float)vals[0], (float)vals[1], (float)vals[2]),
                            V2 = new Vector3((float)vals[3], (float)vals[4], (float)vals[5])
                        });
                };
                return;
            }
            // Add other clauses here, similarly.
            else throw new TypeInitializationException($"Atropos.Feature<T>", new Exception($"Unable to look up proper arrayification form for a Feature<{typeof(T).Name}>, in static ctor."));

        }

        public static explicit operator Feature<T>(SerializableFeature<T> source)
            => new Feature<T>() { Content = FromArray(source.Values) };

        public static explicit operator SerializableFeature<T>(Feature<T> source)
            => new SerializableFeature<T>() { Values = source.AsArray };
    }
}