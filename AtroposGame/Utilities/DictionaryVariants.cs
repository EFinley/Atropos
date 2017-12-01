
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
        public static IEnumerable<Tkey> KeysFor<Tkey, Tvalue>(this IDictionary<Tkey, Tvalue> source, Tvalue value) where Tvalue : IEquatable<Tvalue>
        {
            return source.Where(kvp => kvp.Value.Equals(value)).Select(kvp => kvp.Key);
        }

        public static Tkey KeyFor<Tkey, Tvalue>(this IDictionary<Tkey, Tvalue> source, Tvalue value) where Tvalue : IEquatable<Tvalue>
        {
            return source.KeysFor(value).FirstOrDefault();
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