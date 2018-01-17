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
using System.Threading.Tasks;
using Android.Util;
using System.Numerics;
using System.Threading;
using Nito.AsyncEx;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;

namespace Atropos
{
    public static class Extensions
    {
        public static string Join<T>(this IEnumerable<T> source, string separator = ", ", string brackets = "()")
        {
            if (source == null || source.Count() == 0) return brackets;
            string openingBracket = brackets.Substring(0, brackets.Length / 2);
            string closingBracket = brackets.Substring(brackets.Length / 2);
            return openingBracket + String.Join(separator, source) + closingBracket;
        }

        public static string JoinNested<T>(this IEnumerable<IEnumerable<T>> source, string sep1 = ", ", string brack1 = "{}", string sep2 = ", ", string brack2 = "()")
        {
            IEnumerable<string> sublists = source.Select(subEnum => subEnum.Join(sep2, brack2));
            return sublists.Join(sep1, brack1);
        }

        public static T Clamp<T> (this T input, T min, T max)
        {
            if (Operator<T>.GreaterThan(min, input)) return min;
            if (Operator<T>.LessThan(max, input)) return max;
            return input;
        }

        //public static T ScaleFrom<T>(this T input, T inputMin, T inputMax)
        //{
        //    return Operator.Divide(Operator.Subtract(input, inputMin), Operator.Subtract(inputMax, inputMin));
        //}
        //public static T ScaleBetween<T>(this T input, T inputMin, T inputMax)
        //{
        //    return ScaleFrom(input.Clamp(inputMin, inputMax), inputMin, inputMax);
        //}
        //public static T ScaleBetween<T, T2>(this T input, T2 inputMin, T2 inputMax)
        //{
        //    throw new NotImplementedException(); // This one just exists so the compiler is "certain" that it will find such a function even if the type args are bizarre.
        //}
        //public static Quaternion ScaleBetween(this Quaternion input, Quaternion inputMin, Quaternion inputMax)
        //{
        //    var distToMin = Quaternion.Dot(input, inputMin);
        //    var distToMax = Quaternion.Dot(input, inputMax);
        //    return Quaternion.Slerp(inputMin, inputMax, distToMax / (distToMax + distToMin));
        //}
        //public static Vector3 ScaleBetween(this Vector3 input, Vector3 inputMin, Vector3 inputMax)
        //{
        //    var distToMin = (input - inputMin).Length();
        //    var distToMax = (input - inputMax).Length();
        //    return (distToMax * inputMin + distToMin * inputMax) / (distToMin + distToMax);
        //}
        //public static Quaternion ScaleBetween(this Quaternion input, float lengthMin, float lengthMax)
        //{
        //    return input * (input.Length().Clamp(lengthMin, lengthMax) / input.Length());
        //}
        //public static Vector3 ScaleBetween(this Vector3 input, float lengthMin, float lengthMax)
        //{
        //    return input * (input.Length().Clamp(lengthMin, lengthMax) / input.Length());
        //}

        public static T IdentityFunction<T> (T inp) { return (T)inp; }
        public static T IfNonNull<T>(T input) where T : class { return input ?? default(T); } 
        
        public static T Sum<T>(this IEnumerable<T> source) where T : struct
        {
            if (source.Count() == 0) return default(T);
            var sum = source.First();
            foreach (T elem in source.Skip(1))
            {
                sum = Operator.Add(sum, elem);
            }
            return sum;
        }
        public static T Average<T>(this IEnumerable<T> source) where T : struct
        {
            try
            {
                return Operator.DivideAlternative(source.Sum(), (float)source.Count()); // We don't want any funny divide-by-int shenanigans here.
            }
            catch (System.InvalidOperationException)
            {
                return Operator.MultiplyAlternative(source.Sum(), 1.0f / source.Count()); 
                // Quaternion doesn't define divide-by-scalar but it does have multiply. Go figure.
                // TODO - Look up the reputed Quaternion averaging math c/o NASA (??)
            }
        }

        //// More sophisticated metrics...
        //public static float ErrorSquared(this Vector3 orig, Vector3 mean) { return (orig - mean).LengthSquared(); }
        //public static float ErrorSquared(this Vector2 orig, Vector2 mean) { return (orig - mean).LengthSquared(); }
        //public static float ErrorSquared(this Vector4 orig, Vector4 mean) { return (orig - mean).LengthSquared(); }
        //public static float ErrorSquared(this Quaternion orig, Quaternion mean) { float v = orig.AngleTo(mean); return v * v; } // Not the technical definition, but it'll do for us here.
        ////public static float ErrorSquared(this Quaternion orig, Quaternion mean) { float v = Quaternion.Dot(orig, mean); return v * v; } // Not the technical definition, but it'll do for us here.
        //public static float ErrorSquared<T>(this T orig, T mean) where T : struct
        //{ T sq = Operator.Subtract(orig, mean); return Operator.Convert<T, float>(Operator.Multiply(sq, sq)); }

        //public static float SumErrorSquared<T>(this IEnumerable<T> source) where T : struct
        //{
        //    return source.SumDifferenceSquared(source.Average());
        //}
        //public static float SumDifferenceSquared<T>(this IEnumerable<T> source, T mean) where T : struct
        //{
        //    return source.Select((s) => s.ErrorSquared(mean)).Sum();
        //}
        //public static float StandardDeviation<T>(this IEnumerable<T> source) where T : struct
        //{
        //    return (float)Math.Sqrt(source.SumErrorSquared());
        //}

        //public static float RMSAverage<T>(this IEnumerable<T> source) where T : struct
        //{
        //    return (float)Math.Sqrt(source.SumDifferenceSquared(default(T)));
        //}
        ////public static float RMSAverage(this IEnumerable<Quaternion> source)
        ////{
        ////    return (float)Math.Sqrt(source.Select((q) => q.LengthSquared()).Sum()); // Because our slightly odd definition of ErrorSquared above won't work for Quats; the dot product would always be zero.
        ////}

        public static List<T> Sums<T>(this IEnumerable<IEnumerable<T>> source) where T : struct
        {
            if (source.Count() == 0) return new List<T>();
            if (source.Count() == 1) return source.First().ToList();
            return source.Select(sublist => { return sublist.Sum(); }).ToList();
        }
        public static List<T> Averages<T>(this IEnumerable<IEnumerable<T>> source) where T : struct
        {
            if (source.Count() == 0) return new List<T>();
            if (source.Count() == 1) return source.First().ToList();
            return source.Select(sublist => { return sublist.Average(); }).ToList();
        }

        // Kudos to http://stackoverflow.com/a/17267323 for this one.
        public static String bytesToHex(this byte[] bytes)
        {
            char[] hexArray = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            char[] hexChars = new char[bytes.Length * 2];
            int v;
            for (int j = 0; j < bytes.Length; j++)
            {
                v = bytes[j] & 0xFF;
                hexChars[j * 2] = hexArray[v >> 4];
                hexChars[j * 2 + 1] = hexArray[v & 0x0F];
            }
            return new String(hexChars);
        }

        public static string tagID(this Android.Nfc.Tag tag)
        {
            return tag.GetId().bytesToHex();
        }

        //public static double MeanSquare<T>(this T source) where T : IEnumerable<double>
        //{
        //    return source.Select(d => d * d).Average();
        //}

        //public static double RootMeanSquare<T>(this T source) where T : IEnumerable<double>
        //{
        //    //return Math.Sqrt(source.Select(d => d * d).Average());
        //    var A = source.Select(d => d * d);
        //    var B = A.Average();
        //    var C = Math.Sqrt(B);
        //    return C;
        //}

        //public static Vector3 Sum(this Vector3[] source) // Fuckin' hell.  Really?  Not defined?  OpenTK, you suck.
        //{
        //    var result = new Vector3(0);
        //    foreach (Vector3 vec in source) result += vec;
        //    return result;
        //}
        //public static Quaternion Sum(this Quaternion[] source)
        //{
        //    var result = new Quaternion();
        //    foreach (Quaternion quat in source) result += quat;
        //    return Quaternion.Normalize(result);
        //}

        //public static Vector3 Average(this Vector3[] source)
        //{
        //    return source.Sum() / source.Length;
        //}
        //public static Quaternion Average(this Quaternion[] source)
        //{
        //    return Quaternion.Multiply(source.Sum(), 1.0f / source.Length); // Divide is only defined on quat / quat, but Multiply is overloaded.  'Kay.
        //}

        public static void LaunchAsOrphan(this Task taskToLaunch, string label = null)
        {
            taskToLaunch.LaunchAsOrphan(CancellationToken.None, label);
        }
        public static void LaunchAsOrphan(this Task taskToLaunch, CancellationToken token, string label = null)
        {
            taskToLaunch.ContinueWith(t => Log.Error("OrphanedTask", $"Task [{label}] threw:" + t.Exception.Flatten().ToString()), token, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        // When using CancellationTokens to cause "Stop" signals, inside of Task constructions, we do run into TaskCancelledException issues
        public static async Task SwallowCancellations(this Task task)
        {
            try
            {
                await task;
            }
            catch (AggregateException ae)
            {
                //Log.Info("Test", "Gamma");
                ae.Handle(x => x is TaskCanceledException);  // Unpacking... return true (it's been handled) iff all of the subexceptions are this.
            }
            catch (TaskCanceledException)
            {
                //Log.Info("Test", "Delta");
            }
        }
        public static void SwallowCancellations(this Action act)
        {
            try
            {
                act?.Invoke();
            }
            catch (AggregateException ae)
            {
                //Log.Info("Test", "Epsilon");
                ae.Handle(x => x is TaskCanceledException);  // Unpacking... return true (it's been handled) iff all of the subexceptions are this.
            }
            catch (TaskCanceledException)
            {
                //Log.Info("Test", "Tau");
            }
        }

        //public static Vector3 ToVector(this double[] source)
        //{
        //    return new Vector3((float)(source?[0] ?? 0f), (float)(source?[1] ?? 0f), (float)(source?[2] ?? 0f));
        //}
        //public static Vector3[] ToVectors(this double[][] source)
        //{
        //    return source.Select(vec => vec.ToVector()).ToArray();
        //}

        //public static double VectorStandardDeviation(this Vector3[] source)
        //{
        //    Vector3 mean = source.Average();
        //    double cumulativeLSquared = 0.0;
        //    foreach (Vector3 vec in source) cumulativeLSquared += (vec - mean).LengthSquared();
        //    return Math.Sqrt(cumulativeLSquared);
        //}
        //public static double VectorStandardDeviation(this IEnumerable<Vector3> source)
        //{
        //    return source.ToArray().VectorStandardDeviation();
        //}
        //public static double QuatStandardDeviation(this Quaternion[] source)
        //{
        //    Quaternion mean = source.Average();
        //    double cumulativeDeltaSquared = 0.0;
        //    foreach (var q in source) { float qDotMean = Quaternion.Dot(q, mean); cumulativeDeltaSquared += qDotMean * qDotMean; }
        //    return Math.Sqrt(cumulativeDeltaSquared);
        //}
        //public static double QuatStandardDeviation(this IEnumerable<Quaternion> source)
        //{
        //    return QuatStandardDeviation(source.ToArray());
        //}

        public static Vector3 Normalize(this Vector3 source)
        {
            return Vector3.Normalize(source);
        }
        public static Quaternion Normalize(this Quaternion source)
        {
            return Quaternion.Normalize(source);
        }

        //public static double MaxBy(this double[] source, Func<double, double> operation)
        //{
        //    var s2 = source.Select(d => new double[] { d, operation(d) });
        //    var maxVal = double.NegativeInfinity;
        //    var retVal = double.NaN;
        //    foreach (double[] pair in s2)
        //    {
        //        if (pair[1] > maxVal)
        //        {
        //            maxVal = pair[1];
        //            retVal = pair[0];
        //        }
        //    }
        //    return retVal;
        //}

        //public static double MinBy(this double[] source, Func<double, double> operation)
        //{
        //    var s2 = source.Select(d => new double[] { d, operation(d) });
        //    var minVal = double.PositiveInfinity;
        //    var retVal = double.NaN;
        //    foreach (double[] pair in s2)
        //    {
        //        if (pair[1] < minVal)
        //        {
        //            minVal = pair[1];
        //            retVal = pair[0];
        //        }
        //    }
        //    return retVal;
        //}

        public static DateTime Average(this IEnumerable<DateTime> source)
        {
            DateTime first = source.First();
            double span = 0.0;
            foreach (DateTime t in source) span += (t - first).TotalSeconds;
            span /= source.Count();
            return first + TimeSpan.FromSeconds(span);
        }
        
        /// <summary>
        /// [Caution] As per the AsyncEx notes, this is a hack, and neither thread-safe nor truly immediate.
        /// </summary>
        public static bool IsSet(this AsyncManualResetEvent source)
        {
            try
            {
                source.Wait(CancellationTokenHelpers.FromTask(Task.Delay(5)).Token);
                return true;
            }
            catch (System.OperationCanceledException)
            {
                return false;
            }

        }

        public static Quaternion Parse(this Quaternion source, string fromString)
        {
            var subStrings = fromString.Trim('{', '}').Split(' ', ':'); // Typical Quaternion toString spits out stuff like (parens included) "{X:3.2 Y:5.42 Z:0 W:0.872}".
            if (subStrings.Length != 8) { Log.Warn("Extensions", $"Failure to parse {fromString} into quaternion form."); return source; }
            source.X = float.Parse(subStrings[1]);
            source.Y = float.Parse(subStrings[3]);
            source.Z = float.Parse(subStrings[5]);
            source.W = float.Parse(subStrings[7]);
            return source;
        }

        public static Task<bool> Before(this Task primary, params Task[] other)
        {
            Task[] allTasks = new Task[other.Length + 1];
            allTasks[0] = primary;
            other.CopyTo(allTasks, 1);
            return Task.WhenAny(allTasks).ContinueWith(t => { return (primary.Status == TaskStatus.RanToCompletion); });
        }

        public static Task<bool> Before(this Task primary, params CancellationToken[] tokens)
        {
            var localTCS = new TaskCompletionSource();
            foreach (var token in tokens)
                token.Register(localTCS.SetResult);
            return Task.WhenAny(primary, localTCS.Task).ContinueWith(t => { return (primary.Status == TaskStatus.RanToCompletion); });
        }

        public static Task<bool> Before(this Task primary, TimeSpan timeout)
        {
            return primary.Before(timeout, new Task[] { }); // Provide an empty list (rather than null) so that CopyTo doesn't throw in the following function.
        }

        public static Task<bool> Before(this Task primary, TimeSpan timeout, params Task[] others)
        {
            var timeoutTask = Task.Delay(timeout);

            Task[] allTasks = new Task[others.Length + 1];
            allTasks[0] = timeoutTask;
            others.CopyTo(allTasks, 1);

            return Before(primary, allTasks);
        }

        public static Task<bool> Before(this Task primary, TimeSpan timeout, params CancellationToken[] tokens)
        {
            var localTCS = new TaskCompletionSource();
            foreach (var token in tokens)
                token.Register(localTCS.SetResult);
            CancellationTokenHelpers.Timeout(timeout).Token.Register(localTCS.SetResult);
            return Task.WhenAny(primary, localTCS.Task).ContinueWith(t => { return (primary.Status == TaskStatus.RanToCompletion); });
        }

        public static Random getRandomRand = new Random();
        public static T GetRandom<T>(this IList<T> sourceList)
        {
            return sourceList.ElementAt(getRandomRand.Next(sourceList.Count));
        }
        

        // Cyclic clamping functions - for angles, mostly, but available otherwise if needed.
        private static Func<double, double> CyclicClampFactory(double min, double max)
        {
            var totalRange = max - min;
            var midpoint = (max + min) / 2.0;
            return (d) => ((d + midpoint) % totalRange) - midpoint;
        }
        public static double CyclicClamp(this double input, double min, double max)
        {
            return CyclicClampFactory(min, max).Invoke(input);
        }
        public static double CyclicClamp(this double input, double max)
        {
            return CyclicClamp(0.0, max);
        }

        private static Func<double, double> _clampPlusMinus180;
        public static double ClampPlusMinus180(this double input)
        {
            _clampPlusMinus180 = _clampPlusMinus180 ?? CyclicClampFactory(-180, 180);
            return _clampPlusMinus180(input);
        }

        private static Func<double, double> _clampPlusMinusPi;
        public static double ClampPlusMinusPi(this double input)
        {
            _clampPlusMinusPi = _clampPlusMinusPi ?? CyclicClampFactory(-Math.PI, Math.PI);
            return _clampPlusMinusPi(input);
        }
        public static int CyclicClamp(this int input, int min, int max)
        {
            var totalRange = max - min;
            var midpoint = (max + min) / 2;
            return ((input + midpoint) % totalRange) - midpoint;
        }



        public static bool IsOneOf<T>(this T query, params T[] options)
        {
            return options.Contains(query);
        }

        public static bool CanConvert<Tfrom, Tto>(this Tfrom source)
        {
            try
            {
                Operator.Convert<Tfrom, Tto>(source);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static bool Implements(this Type tgtType, string interfaceName)
        {
            return (tgtType.GetInterface(interfaceName) != null);
        }
        public static bool Implements<Tinterface>(this Type tgtType)
        {
            return Implements(tgtType, typeof(Tinterface).Name);
        }

        public static T GetStaticProperty<T>(this Type type, string propertyName)
        {
            var propInfo = type.GetProperty(propertyName);
            if (propInfo == null) throw new ArgumentNullException($"Error retrieving static property {type.Name}.{propertyName}.");
            return (T)propInfo.GetValue(null);
        }

        public static T InvokeStaticMethod<T>(this Type type, string methodName, object parameter)
        {
            return InvokeStaticMethod<T>(type, methodName, new object[] { parameter });
        }
        public static T InvokeStaticMethod<T>(this Type type, string methodName, object[] parameters = null)
        {
            var methInfo = type.GetMethod(methodName);
            if (methInfo.ReturnType != typeof(T)) throw new ArgumentException($"Static method {methodName} on {type.Name} returns {methInfo.ReturnType.Name} (not {typeof(T).Name}).");
            return (T)methInfo.Invoke(null, parameters);
        }

        public static TimeSpan MultipliedBy(this TimeSpan start, double multiplier)
        {
            return TimeSpan.FromTicks((long)(start.Ticks * multiplier));
        }
        public static TimeSpan DividedBy(this TimeSpan start, double divider)
        {
            if (divider == 0) throw new DivideByZeroException();
            return TimeSpan.FromTicks((long)(start.Ticks / divider));
        }

        public static void Vibration(this Plugin.Vibrate.Abstractions.IVibrate source, double milliseconds)
        {
            source.Vibration(TimeSpan.FromMilliseconds(milliseconds));
        }

        public static void Raise<T>(this EventHandler<EventArgs<T>> sourceEvent, object sender, T value)
        {
            sourceEvent?.Invoke(sender, new EventArgs<T>(value));
        }

        public static void Raise<T>(this EventHandler<EventArgs<T>> sourceEvent, T value, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "this")
        {
            sourceEvent?.Invoke(callerName, new EventArgs<T>(value));
        }
    }

    public static class AndroidLayoutUtilExtensions
    {
        public static void DisableScrolling(this ListView listView)
        {
            IListAdapter adapter = listView.Adapter;

            if (adapter == null)
            {
                return;
            }
            ViewGroup vg = listView;
            int totalHeight = 0;
            for (int i = 0; i < adapter.Count; i++)
            {
                View listItem = adapter.GetView(i, null, vg);
                listItem.Measure(0, 0);
                totalHeight += listItem.MeasuredHeight;
            }

            ViewGroup.LayoutParams par = listView.LayoutParameters;
            par.Height = totalHeight + (listView.DividerHeight * (adapter.Count - 1));
            listView.LayoutParameters = par;
            listView.RequestLayout();
        }
    }

    public class SimpleCircularList<T> // For very simple jobs; not currently worth looking up a full-featured version of this.
    {
        private List<T> contents;
        public int index = 0;
        public SimpleCircularList(params T[] inContents)
        {
            contents = new List<T>(inContents);
        }
        public T this[int i] { get { index = i; return contents[index]; } }
        public T Current { get { return contents[index]; } }
        public T Next { get { index++; index %= contents.Count; return contents[index]; } }
        public void Reset() { index = 0; }
        public void Add(T newItem)
        {
            contents.Add(newItem);
        }
        public bool Contains(T item)
        {
            return contents.Contains(item);
        }
    }

    internal static class Serializer
    {
        internal static string Serialize<T>(T o)
        {
            using (var stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, o);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        internal static T Deserialize<T>(string str)
        {
            using (var stream = new MemoryStream(Convert.FromBase64String(str)))
            {
                try
                {
                    System.Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");
                    return (T)new BinaryFormatter().Deserialize(stream);
                }
                catch (Exception e)
                {
                    Log.Error("ArbitrarySerializer", e.Message);
                    return default(T);
                }
            }
        }

        internal static T Roundtrip<T>(T obj)
        {
            return Deserialize<T>(Serialize<T>(obj));
        }

        internal static bool Check<T>(T obj)
        {
            if (!typeof(T).IsValueType)
                return Roundtrip<T>(obj) != null;
            else
                return Operator.NotEqual(Roundtrip<T>(obj), default(T));
        }

        // The below exists for one specific situation where we needed the serializer itself to be instantiated as a constructed generic type, then used.  Not intended for common use.
        internal static class TypedSerializer<T>
        {
            internal static string Serialize(T o) { return Serializer.Serialize<T>(o); }
            internal static T Deserialize(string str) { return Serializer.Deserialize<T>(str); }
            internal static T Roundtrip(T obj) { return Serializer.Roundtrip<T>(obj); }
            internal static bool Check(T obj) { return Serializer.Check<T>(obj); }
        }
    }
}