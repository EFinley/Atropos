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

using MiscUtil;

namespace com.Atropos.KinematicsExperiment
{
    public interface IVectorLike<T> { }

    public struct VectorLike<T> : IVectorLike<T> where T:struct
    {
        public T Value;
        public static explicit operator VectorLike<T>(T inputVal)
        {
            return new VectorLike<T> { Value = inputVal };
        }
        public static implicit operator T(VectorLike<T> inputVal)
        {
            return inputVal.Value;
        }

        public static VectorLike<T> operator +(VectorLike<T> val1, VectorLike<T> val2)
        {
            return (VectorLike<T>)Operator<T>.Add(val1, val2);
        }

        public static VectorLike<T> operator -(VectorLike<T> val1, VectorLike<T> val2)
        {
            return (VectorLike<T>)Operator<T>.Subtract(val1, val2);
        }

        public static VectorLike<T> operator *(VectorLike<T> val1, double val2)
        {
            return (VectorLike<T>)Operator.MultiplyAlternative<T, double>(val1, val2);
        }

        public static VectorLike<T> operator *(double val1, VectorLike<T> val2)
        {
            return (VectorLike<T>)Operator.MultiplyAlternative<T, double>(val2, val1);
        }

        public static VectorLike<T> operator /(VectorLike<T> val1, double val2)
        {
            return Operator.DivideAlternative(val1, val2);
        }

        private static Func<T, float> lengthFunc;
        public float Length
        {
            get
            {
                if (lengthFunc == null) throw new ArgumentException($"Cannot request Length() from type VectorLike<{typeof(T).Name}>.");
                return lengthFunc(Value);
            }
        }
        
        static VectorLike()
        {
            Type t = typeof(T);
            System.Reflection.PropertyInfo propInfo = t.GetProperty("Length");
            if (propInfo != null)
            {
                lengthFunc = (instance) => (float)propInfo.GetValue(instance);
            }
            System.Reflection.MethodInfo methInfo = t.GetMethod("Length");
            if (methInfo != null && methInfo.GetParameters().Count() == 0)
            {
                lengthFunc = (instance) => (float)methInfo.Invoke(instance, null);
            }
        }
    }
}