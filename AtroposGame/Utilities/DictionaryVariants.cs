
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

    /// <summary>
    /// Acts as a dictionary, but actually just couples together two Lists (and remains 'live' to changes to either one).
    /// Usually you'll expose this as a property, returning a new one derived just then from the two lists.
    /// </summary>
    /// <typeparam name="Tkey"></typeparam>
    /// <typeparam name="Tvalue"></typeparam>
    public class DictionaryFacade<Tkey, Tvalue> : IDictionary<Tkey, Tvalue>
    {
        private IList<Tkey> KeyList;
        private IList<Tvalue> ValueList;
        public DictionaryFacade(IList<Tkey> keyList, IList<Tvalue> valueList)
        {
            KeyList = keyList;
            ValueList = valueList;
        }

        private int indexOf(Tkey key) { return KeyList.IndexOf(key); }

        public Tvalue this[Tkey key]
        {
            get
            {
                return ValueList[indexOf(key)];
            }

            set
            {
                ValueList[indexOf(key)] = value;
            }
        }

        public int Count
        {
            get
            {
                return KeyList.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public ICollection<Tkey> Keys
        {
            get
            {
                return KeyList;
            }
        }

        public ICollection<Tvalue> Values
        {
            get
            {
                return ValueList;
            }
        }

        public void Add(KeyValuePair<Tkey, Tvalue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(Tkey key, Tvalue value)
        {
            if (KeyList.Contains(key))
            {
                ValueList[indexOf(key)] = value;
            }
            else
            {
                KeyList.Add(key);
                ValueList.Add(value);
            }
        }

        public void Clear()
        {
            KeyList.Clear();
            ValueList.Clear();
        }

        public bool Contains(KeyValuePair<Tkey, Tvalue> item)
        {
            var i = ValueList.IndexOf(item.Value);
            return (i >= 0 && i == indexOf(item.Key));
        }

        public bool ContainsKey(Tkey key)
        {
            return KeyList.Contains(key);
        }

        private IDictionary<Tkey, Tvalue> _asDict()
        {
            return ValueList
                .ToDictionary<Tvalue, Tkey>((v) => KeyList[ValueList.IndexOf(v)]);
        }
        public void CopyTo(KeyValuePair<Tkey, Tvalue>[] array, int arrayIndex)
        {
            _asDict().CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<Tkey, Tvalue>> GetEnumerator()
        {
            return _asDict().GetEnumerator();
        }

        public bool Remove(KeyValuePair<Tkey, Tvalue> item)
        {
            if (Contains(item))
            {
                KeyList.Remove(item.Key);
                ValueList.Remove(item.Value);
                return true;
            }
            else return false;
        }

        public bool Remove(Tkey key)
        {
            if (KeyList.Contains(key))
            {
                KeyList.Remove(key);
                ValueList.RemoveAt(indexOf(key));
                return true;
            }
            else return false;
        }

        public bool TryGetValue(Tkey key, out Tvalue value)
        {
            if (KeyList.Contains(key))
            {
                value = ValueList[indexOf(key)];
                return true;
            }
            else
            {
                value = default(Tvalue);
                return false;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (_asDict() as IEnumerable).GetEnumerator();
        }
    }

    public static class DictionaryExtensions
    {
        public static Dictionary<string, string> Parse(this Dictionary<string, string> source, params string[] parseStrings)
        {
            if (parseStrings.Length == 0) return source;
            if (parseStrings.Length == 1 && parseStrings[0].Contains(",")) parseStrings = parseStrings[0].Split(',');
            foreach (var pString in parseStrings)
            {
                if (pString.Count(c => c == ':') != 1) throw new ArgumentException();
                var subStr = pString.Split(':');
                source[subStr[0]] = subStr[1];
            }
            return source;
        }

        public static Dictionary<string, object> Parse(this Dictionary<string, object> source, params string[] parseStrings)
        {
            if (parseStrings.Length == 0) return source;
            if (parseStrings.Length == 1 && parseStrings[0].Contains(",")) parseStrings = parseStrings[0].Split(',');
            foreach (var pString in parseStrings)
            {
                if (pString.Count(c => c == ':') != 1) throw new ArgumentException();
                var subStr = pString.Split(':');
                object parsedArg;
                if (int.TryParse(subStr[1], out int intArg)) parsedArg = intArg;
                else if (double.TryParse(subStr[1], out double dblArg)) parsedArg = dblArg;
                //else if (DateTime.TryParse(subStr[1], out DateTime dtArg)) parsedArg = dtArg;
                else parsedArg = subStr[1];
                source[subStr[0]] = parsedArg;
            }
            return source;
        }

        public static Dictionary<string, object> Parse(this Dictionary<string, object> source, string arg1Name, int arg1Val, string arg2Name = null, object arg2Val = null, string arg3Name = null, object arg3Val = null)
        {
            if (!String.IsNullOrEmpty(arg1Name)) source[arg1Name] = arg1Val;
            if (!String.IsNullOrEmpty(arg2Name)) source[arg2Name] = arg2Val;
            if (!String.IsNullOrEmpty(arg3Name)) source[arg3Name] = arg3Val;
            return source;
        }

        // Second verse, same as the first (except for the type of argument #3)
        public static Dictionary<string, object> Parse(this Dictionary<string, object> source, string arg1Name, double arg1Val, string arg2Name = null, object arg2Val = null, string arg3Name = null, object arg3Val = null)
        {
            if (!String.IsNullOrEmpty(arg1Name)) source[arg1Name] = arg1Val;
            if (!String.IsNullOrEmpty(arg2Name)) source[arg2Name] = arg2Val;
            if (!String.IsNullOrEmpty(arg3Name)) source[arg3Name] = arg3Val;
            return source;
        }

        public static string ToParseableString<T>(this Dictionary<string, T> source)
        {
            return source.Select(kvp => $"{kvp.Key}:{kvp.Value}").Join(", ", "");
        }
    }

    /// <summary>
    /// A dictionary which takes two items as separate keys.  Order matters; an item filed under (a, b) is distinct from under (b, a),
    /// and in fact a and b need not even have the same type.  You need both keys; it'd be possible to use LINQ to obtain "all items with
    /// key 1 = A" or even "all items with one or both keys = A" but I don't need that for this application so I'm not bothering.
    /// </summary>
    /// <typeparam name="Tkey1">Type of the first key.</typeparam>
    /// <typeparam name="Tkey2">Type of the second key.</typeparam>
    /// <typeparam name="Tvalue">Type of the stored values.</typeparam>
    public class DoubleDictionary<Tkey1, Tkey2, Tvalue> : Dictionary<Tuple<Tkey1, Tkey2>, Tvalue>
    {
        // The cornerstone of this lil' guy - a convenience method standing in for a much wordier constructor.
        private static Tuple<Tkey1, Tkey2> Tup(Tkey1 item1, Tkey2 item2)
        {
            return new Tuple<Tkey1, Tkey2>(item1, item2);
        }

        public Tvalue this[Tkey1 key1, Tkey2 key2]
        {
            get { return base[Tup(key1, key2)]; }
            set { base[Tup(key1, key2)] = value; }
        }

        public void Add(Tkey1 key1, Tkey2 key2, Tvalue value)
        {
            base.Add(Tup(key1, key2), value);
        }

        public bool ContainsKeypair(Tkey1 key1, Tkey2 key2)
        {
            return base.ContainsKey(Tup(key1, key2));
        }

        public bool Remove(Tkey1 key1, Tkey2 key2)
        {
            return base.Remove(Tup(key1, key2));
        }

        public bool TryGetValue(Tkey1 key1, Tkey2 key2, out Tvalue value)
        {
            return base.TryGetValue(Tup(key1, key2), out value);
        }
    }


}