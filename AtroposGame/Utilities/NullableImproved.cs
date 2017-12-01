using System;
using System.Collections.Generic;
using System.Linq;

namespace Atropos
{
    public class Nullable<T> : INullable<T>
    {
        private static INullable<T> defaultInner;
        private INullable<T> inner;
        public Nullable()
        {
            inner = defaultInner;
        }
        public Nullable(T value) : this()
        {
            (inner as ICanSetValue<T>).Value = value;
        }
        static Nullable()
        //void setDefaults()
        {
            var typeT = typeof(T);
            if (typeT.IsValueType)
                defaultInner = (INullable<T>)System.Activator.CreateInstance(
                    typeof(NullableS<>).MakeGenericType(typeT));
            else defaultInner = (INullable<T>)System.Activator.CreateInstance(
                    typeof(NullableC<>).MakeGenericType(typeT));
        }
        public bool HasValue { get { return inner.HasValue; } }
        public T Value
        {
            get
            {
                return inner.Value;
            }
            private set
            {
                inner = defaultInner;
                (inner as ICanSetValue<T>).Value = value;
            }
        }

        #region Many different forms of equality!
        public override bool Equals(object other)
        {
            if (inner == null || !inner.HasValue)
            {
                if (other == null) return true;
                var o = (other as INullable<T>);
                if (o != null && !o.HasValue) return true;
                else return false;
            }
            else return inner.Equals(other);
        }
        public static bool operator == (Nullable<T> self, object other)
        {
            return Nullable<T>.Equals(self, other);
        }
        public static bool operator !=(Nullable<T> self, object other)
        {
            return !Nullable<T>.Equals(self, other);
        }
        //public static bool operator ==(object other, Nullable<T> self)
        //{
        //    return self.Equals(other);
        //}
        //public static bool operator !=(object other, Nullable<T> self)
        //{
        //    return !self.Equals(other);
        //}
        #endregion

        public override int GetHashCode() { return inner.GetHashCode(); }

        public T GetValueOrDefault() { return inner.GetValueOrDefault(); }
        public T GetValueOrDefault(T defaultValue) { return inner.GetValueOrDefault(defaultValue); }
        public override string ToString() { return inner.ToString(); }

        public static implicit operator T(Nullable<T> value) { return value.inner.Value; }
        public static implicit operator Nullable<T>(T value) { return new Nullable<T>(value); }

        #region Interface and classes which are theoretically exposed to outside use but really only matter here.
        

        public interface ICanSetValue<Tv> // Setter is "privatier than private" - only accessible via an *explicit* cast to this interface.
        {
            Tv Value { set; }
        }
        #endregion
    }

    public interface INullable<Ti>
    {
        bool HasValue { get; }
        Ti Value { get; }
        bool Equals(object other);
        int GetHashCode();
        Ti GetValueOrDefault();
        Ti GetValueOrDefault(Ti defaultValue);
    }

    public class NullableS<Ts> : INullable<Ts>, Nullable<Ts>.ICanSetValue<Ts> where Ts : struct
    {
        private Ts? inner;
        public NullableS() { }
        public NullableS(Ts? value)
        {
            inner = value;
        }
        public bool HasValue { get { return inner.HasValue; } }
        public Ts Value
        {
            get
            {
                return inner.Value;
            }
            set
            {
                inner = value;
            }
        }

        public override bool Equals(object other) { return inner.Equals(other); }
        public override int GetHashCode() { return inner.GetHashCode(); }

        public Ts GetValueOrDefault() { return inner.GetValueOrDefault(); }
        public Ts GetValueOrDefault(Ts defaultValue) { return inner.GetValueOrDefault(defaultValue); }
        public override string ToString() { return inner.ToString(); }

        public static implicit operator Ts? (NullableS<Ts> value) { return value.inner; }
        public static implicit operator Ts(NullableS<Ts> value) { return value.inner.Value; }
        public static implicit operator NullableS<Ts>(Ts? value) { return new NullableS<Ts>(value); }
        public static implicit operator NullableS<Ts>(Ts value) { return new NullableS<Ts>(value); }
    }

    public class NullableC<Tc> : INullable<Tc>, Nullable<Tc>.ICanSetValue<Tc> where Tc : class
    {
        private Tc inner;
        public NullableC() { }
        public NullableC(Tc value)
        {
            inner = value;
        }
        public bool HasValue { get { return inner != null; } }
        public Tc Value
        {
            get
            {
                return inner;
            }
            set
            {
                inner = value;
            }
        }

        public override bool Equals(object other) { return inner.Equals(other); }
        public override int GetHashCode() { return inner.GetHashCode(); }

        public Tc GetValueOrDefault() { return inner ?? default(Tc); }
        public Tc GetValueOrDefault(Tc defaultValue) { return inner ?? defaultValue; }
        public override string ToString() { return inner.ToString(); }

        public static implicit operator Tc(NullableC<Tc> value) { return value.inner; }
        public static implicit operator NullableC<Tc>(Tc value) { return new NullableC<Tc>(value); }
    }
}