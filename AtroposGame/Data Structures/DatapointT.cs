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
using System.Runtime.Serialization;
using MiscUtil;
using Nullable = System.Nullable;

namespace Atropos.DataStructures
{

    /// <summary>
    /// Generic datapoint: a wrapper around any value type to provide access for the 
    /// machine learning engine to use it as the underlying type.  This can be 
    /// either a built-in type like <see cref="Vector2"/>, or a user-defined struct
    /// like <see cref="Vector6"/>.  Note that in machine learning literature this is
    /// more often called a "feature vector."
    /// <para>For this struct to wrap a particular type, a few delegate function
    /// slots need to be filled - see <see cref="_asArray"/> & <see cref="crosslinkFunc"/>
    /// - in the static ctor. Also see <seealso cref="SerializableFeature{T}"/> if needed.</para> 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public struct Datapoint<T> : IDatapoint, ISerializable // IDatapoint<T>
        where T : struct
    {
        [NonSerialized] private T? _value;
        //private Nullable<T> _value;
        public T Value
        {
            get
            {
                _value = _value ?? Datapoint.DefaultOrIdentity<T>(); // ?? operator cannot be overloaded
                //_value = (_value != null && _value.HasValue) ? _value.Value : Datapoint.DefaultOrIdentity<T>();
                return _value.Value;
            }
            set { _value = value; }
        }
        public static int Dimensions { get; private set; }
        int IDatapoint.Dimensions { get { return Dimensions; } }

        [NonSerialized] private static Func<T, float[]> _asArray;
        public float[] AsArray() { return _asArray?.Invoke(Value) ?? new float[Dimensions]; }

        [NonSerialized] private static Func<T, float> _magnitude;
        public float Magnitude() { return _magnitude?.Invoke(Value) ?? float.NaN; }

        [NonSerialized] private static Func<float[], IDatapoint> _fromArray;
        //IDatapoint IDatapoint.FromArray(float[] sourceArray) // Normally this wouldn't be an instance method, but it just works out SO much easier, elsewhere, if it is.
        //{
        //    return _fromArray(sourceArray);
        //}
        public IDatapoint FromArray(float[] sourceArray)
        {
            return (Datapoint<T>)_fromArray(sourceArray);
        }

        // Static constructor which *creates* the appropriate functions and assigns them to the delegate. 
        static Datapoint()
        //static void setFunctions()
        {
            Type typeT = typeof(T);
            if (typeT.Implements<IDatapoint>()) // In other words, it's a Datapoint<Datapoint<T>> - although why, I'm not sure.
            {
                //var subTypes = typeT.GetGenericArguments();
                //if (subTypes.Length == 1)
                //{
                //    var sT = subTypes[0];
                //    Dimensions = sT.GetStaticProperty<int>("Dimensions");

                //}
                Dimensions = (default(T) as IDatapoint).Dimensions;
                _magnitude = (val) => (val as IDatapoint).Magnitude();
                _asArray = (val) => (val as IDatapoint).AsArray();
                //_fromArray = (vals) => typeT.InvokeStaticMethod<Datapoint<T>>("FromArray", vals);
                //if (typeT.IsGenericType && typeT.GetGenericTypeDefinition() == typeof(Datapoint<,>)) 
                //{
                //    var dT1T2 = typeof(Datapoint<,>).MakeGenericType(typeT.GetGenericArguments());
                //    var instance = System.Activator.CreateInstance(dT1T2);
                //    var methInfo = dT1T2.GetMethod("FromArray");
                //    _fromArray = (vals) =>
                //    {
                //        //dynamic dyn = dT1T2.InvokeStaticMethod<T>("FromArray", vals);
                //        //return (IDatapoint)dyn;
                //        var parameters = new object[] { vals };//vals.Select(v => (object)v).ToList().ToArray();
                //        dynamic dyn = methInfo.Invoke(instance, parameters);
                //        return (IDatapoint)dyn;
                //    };
                //}
                //else 
                    _fromArray = (vals) => (default(T) as IDatapoint).FromArray(vals);
            }
            else if (typeT.IsOneOf(typeof(int), typeof(double), typeof(float))) // Hackish but whatever
            {
                Dimensions = 1;
                _magnitude = (f) => (float)Math.Abs(Operator.Convert<T, double>(f));
                _asArray = (f) => new float[] { Operator.Convert<T, float>(f) };
                _fromArray = (vals) =>
                {
                    //var result = (Datapoint<T>)Operator.Convert<float, T>(vals[0]);
                    //return result;
                    return new Datapoint<T>() { Value = Operator.Convert<float, T>(vals[0]) };
                };
            }
            else if (typeT == typeof(TimeSpan))
            {
                Dimensions = 1;
                _magnitude = (t) => (float)Operator.Convert<T, TimeSpan>(t).TotalMilliseconds;
                _asArray = (t) => new float[] { _magnitude(t) };
                _fromArray = (vals) => (Datapoint<T>)Operator.Convert<TimeSpan, T>(TimeSpan.FromMilliseconds(vals[0]));
            }
            else if (typeT == typeof(Vector2))
            {
                Dimensions = 2;
                _magnitude = (V2) => Operator.Convert<T, Vector2>(V2).Length();
                _asArray = (V2) => { var C = MiscUtil.Operator.Convert<T, Vector2>(V2); return new float[] { C.X, C.Y }; };
                _fromArray = (vals) => (Datapoint<T>)Operator.Convert<Vector2, T>(new Vector2(vals[0], vals[1]));
            }
            else if (typeT == typeof(Vector3))
            {
                Dimensions = 3;
                _magnitude = (dpt) => Operator.Convert<T, Vector3>(dpt).Length();
                _asArray = (V3) => { var C = MiscUtil.Operator.Convert<T, Vector3>(V3); return new float[] { C.X, C.Y, C.Z }; };
                _fromArray = (vals) => (Datapoint<T>)Operator.Convert<Vector3, T>(new Vector3(vals[0], vals[1], vals[2]));
            }
            else if (typeT == typeof(Quaternion))
            {
                Dimensions = 4;
                _magnitude = (dpt) => Operator.Convert<T, Quaternion>(dpt).Length();
                _asArray = (Q) => { var C = MiscUtil.Operator.Convert<T, Quaternion>(Q); return new float[] { C.X, C.Y, C.Z, C.W }; };
                _fromArray = (vals) => (Datapoint<T>)Operator.Convert<Quaternion, T>(new Quaternion(vals[0], vals[1], vals[2], vals[3]));
            }
            else if (typeT == typeof(Vector6))
            {
                Dimensions = 6;
                _magnitude = (dpt) =>
                {
                    var C = Operator.Convert<T, Vector6>(dpt);
                    return (float)Math.Sqrt(C.V1.LengthSquared() + C.V2.LengthSquared());
                };
                _asArray = (V6) =>
                {
                    var C = MiscUtil.Operator.Convert<T, Vector6>(V6);
                    return new float[] { C.V1.X, C.V1.Y, C.V1.Z, C.V2.X, C.V2.Y, C.V2.Z };
                };
                _fromArray = (vals) => (Datapoint<T>)Operator.Convert<Vector6, T>(
                    new Vector6()
                    {
                        V1 = new Vector3(vals[0], vals[1], vals[2]),
                        V2 = new Vector3(vals[3], vals[4], vals[5])
                    });
            }
            //else if (typeT.Implements<IVectorCluster>())
            //{
            //    Type[] subsumedTypes = typeT.GetGenericArguments();
            //    int[] dimensions = new int[subsumedTypes.Length];
            //    var subtypeIndices = Enumerable.Range(0, subsumedTypes.Length); // Prebuild the equivalent of Python's range function.

            //    foreach (var i in subtypeIndices)
            //    {
            //        subsumedTypes[i] = (typeof(Datapoint<>)
            //                    .MakeGenericType(subsumedTypes[i])); // Creates the Type "Datapoint<Tn>" (not just Feature<>) for each Tn
            //        dimensions[i] = (int)(subsumedTypes[i]
            //                    .GetProperty("Dimensions") // Look up the *property definition* for the property we call ".Dimensions"
            //                    .GetValue(null)); // Retrieve the actual value of that property (null means "from the class itself (i.e. it's static)")
            //    }
            //    Dimensions = dimensions.Sum();
            //    Android.Util.Log.Debug("FeatureT", $"For type {typeT.ToString()}, subsumedTypes is {subsumedTypes}, and Dimensions is {Dimensions}.");

            //    // ToArray is defined as part of the VectorCluster<T1, T2> structure.
            //    _toArray = (T src) =>
            //    {
            //        return (src as IVectorCluster).ToArray();
            //    };

            //    // Extracting the crosslink functions for each sub-component of a VectorCluster is a pain, but it can be done.
            //    int dimsYet = 0;
            //    List<Func<IEnumerable<double>, IEnumerable<double>>> extractSubtypeComponents
            //        = dimensions.Select<int, Func<IEnumerable<double>, IEnumerable<double>>>(
            //            (int d) =>
            //            {
            //                Func<IEnumerable<double>, IEnumerable<double>> result =
            //                    (allValues) => allValues.Skip(dimsYet).Take(d);
            //                dimsYet += d;
            //                return result;
            //            }
            //        ).ToList();
            //    return;
            //}
            // Add other clauses here, similarly. NOTE - for serialization, you will also need to add them to SerializableDatapoint<T>, below.
            else throw new TypeInitializationException($"Atropos.Datapoint<T>", new Exception($"Unable to look up proper arrayification form for a Datapoint<{typeT.Name}>, in static ctor."));
        }

        // And finally a way to get a Datapoint<T> out of a T.
        public static explicit operator Datapoint<T>(T source)
        {
            //if (typeof(T).IsGenericType
            //    && typeof(T).GetGenericTypeDefinition() == typeof(Datapoint<>))
            //{
            //    Type innerType = typeof(T).GetGenericArguments()[0];
            //    return new Datapoint<T>() { Value = ()}
            //}
            return new Datapoint<T>() { Value = source };
        }

        public static implicit operator T(Datapoint<T> source)
        {
            return source.Value;
        }

        #region Standard operators defined based on the underlying type
        // Note that I suspect none of these are necessary due to the implicit conversions to and from T... but for symmetry with Datapoint<T1,T2> it was easy to just add them explicitly to save on casts.
        public static Datapoint<T> operator +(Datapoint<T> first, Datapoint<T> second) { return (Datapoint<T>)Operator.Add(first.Value, second.Value); }
        public static Datapoint<T> operator -(Datapoint<T> first, Datapoint<T> second) { return (Datapoint<T>)Operator.Subtract(first.Value, second.Value); }
        public static Datapoint<T> operator -(Datapoint<T> self) { return (Datapoint<T>)Operator.Negate(self.Value); }

        // Multiplication operators get a little more complex because the "other woman" can have varying types, and AFAIK the operator overloading is picky about specifics there.
        // Here we just cover the most important three - double, float [because System.Numerics.VectorN plays nicely with it, unlike double], and int.
        public static Datapoint<T> operator *(Datapoint<T> vec, float scalar)
        {
            if (typeof(T) != typeof(Quaternion)) return (Datapoint<T>)Operator.MultiplyAlternative(vec.Value, scalar);
            else // When used as rotations, we can't just elementwise multiply - we need to SLERP them from the identity toward/past the target rotation.
            {
                var startQuat = Operator.Convert<T, Quaternion>(vec.Value);
                var slerpResult = Quaternion.Slerp(Quaternion.Identity, startQuat, scalar);
                return (Datapoint<T>)Datapoint.From<T>(Operator.Convert<Quaternion, T>(slerpResult));
            }
        }
        public static Datapoint<T> operator *(float scalar, Datapoint<T> vec) { return vec * scalar; }
        public static Datapoint<T> operator *(Datapoint<T> vec, double scalar) { return vec * (float)scalar; }
        public static Datapoint<T> operator *(double scalar, Datapoint<T> vec) { return vec * (float)scalar; }
        public static Datapoint<T> operator *(Datapoint<T> vec, int scalar) { return vec * (float)scalar; }
        public static Datapoint<T> operator *(int scalar, Datapoint<T> vec) { return vec * (float)scalar; }

        // Division we only do *by* a scalar, never *by* our vector types, so it's a bit easier.
        public static Datapoint<T> operator /(Datapoint<T> vec, float scalar) { return vec * (1.0f / scalar); }
        public static Datapoint<T> operator /(Datapoint<T> vec, double scalar) { return vec / (float)scalar; }
        public static Datapoint<T> operator /(Datapoint<T> vec, int scalar) { return vec / (float)scalar; }

        public static bool operator ==(Datapoint<T> first, Datapoint<T> second) { return Operator.Equals(first.Value, second.Value); }
        public static bool operator !=(Datapoint<T> first, Datapoint<T> second) { return Operator.NotEqual(first.Value, second.Value); }
        #endregion

        //#region Graphics Helper Functions - Mostly obsolete
        //// Moreover, the first two to three entries have special significance... because we assume that they will form two axes (so we can draw pretty pictures with them).
        //// Ditto the first three, if present.
        //public static implicit operator Point(Datapoint<T> source)
        //{
        //    return new Point((int)source.AsArray.ElementAtOrDefault(0), (int)source.AsArray.ElementAtOrDefault(1));
        //}
        //public static implicit operator Vector2(Datapoint<T> source)
        //{
        //    return new Vector2((float)source.AsArray.ElementAtOrDefault(0), (float)source.AsArray.ElementAtOrDefault(1));
        //}
        //public static implicit operator System.Numerics.Vector3(Datapoint<T> source)
        //{
        //    return new System.Numerics.Vector3((float)source.AsArray.ElementAtOrDefault(0), (float)source.AsArray.ElementAtOrDefault(1), (float)source.AsArray.ElementAtOrDefault(2));
        //}

        //// And, since we don't directly inherit the properties of Point or Vector2, let's make .X and .Y available directly here.
        //public float X { get { return (float)this.AsArray.ElementAtOrDefault(0); } }
        //public float Y { get { return (float)this.AsArray.ElementAtOrDefault(1); } }
        //public float Z { get { return (float)this.AsArray.ElementAtOrDefault(2); } }
        //#endregion

        public override string ToString()
        {
            return $"\u1445 {Value} \u1440";
        }

        // Serialization... implementing the ISerializable interface.

        // First, we need an explicit parameterless constructor, which is technically not allowed for a struct; one with an optional parameter is, though.
        public Datapoint(T val = default(T)) { _value = (Operator.NotEqual(val, default(T)) ? val : Datapoint.DefaultOrIdentity<T>()); }

        // Otherwise, the special ISerializable constructor would be our only constructor, which would render things... sticky.
        public Datapoint(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            var valueArray = (float[])info.GetValue("valueArray", typeof(float[]));
            var fArray = (Datapoint<T>)(new Datapoint<T>()).FromArray(valueArray);
            _value = fArray.Value;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");
            info.AddValue("valueArray", AsArray());
        }
    }

    /// <summary>
    /// Since the various System.Numerics Vector classes are NOT marked as Serializable, we need a workaround.
    /// To use, explicitly cast into and out of this type before serialization or after deserialization.  You
    /// should never need to actually construct an instance of this type external to those two activities.
    /// </summary>
    /// <typeparam name="T">The type of feature - see <seealso cref="Datapoint{T}"/>.</typeparam>
    [Serializable]
    public struct SerializableDatapoint<T> where T : struct
    {
        public float[] Values;

        public static explicit operator Datapoint<T>(SerializableDatapoint<T> source)
            => (Datapoint<T>)(new Datapoint<T>()).FromArray(source.Values);

        public static explicit operator SerializableDatapoint<T>(Datapoint<T> source)
            => new SerializableDatapoint<T>() { Values = source.AsArray() };
    }
}