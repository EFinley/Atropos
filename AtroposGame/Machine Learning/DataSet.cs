using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using System.Drawing;
using Android.Graphics;
using PerpetualEngine.Storage;
using System.Threading.Tasks;

namespace com.Atropos.Machine_Learning
{
    public interface IDataset
    {
        string Name { get; set; }
        bool NameIsUserChosen { get; }
        List<string> ClassNames { get; }
        List<GestureClass> Classes { get; }
        //void Save();
        void Clear();
        int MinSamplesInAnyClass();
        void AddSequence(ISequence sample, string classLabel = null, bool skipBitmap = false);
        void RemoveSequence(int index = -1);
        void AddClass(string newClassName);
        void TallySequences(bool RedoFromStart = false);
        bool HasChanged { get; set; }
    }

    [Serializable]
    public abstract class DataSet : IDataset
    {
        // Core information contained
        public List<string> ClassNames { get { return Classes.Select(gc => gc.className).ToList(); } }
        private List<GestureClass> _classes = new List<GestureClass>();
        public List<GestureClass> Classes { get { return _classes; } protected set { _classes = value; } }

        // Ancillary information
        public const string FileExtension = "dataset";
        public bool NameIsUserChosen { get; protected set; } = false;
        protected string _name { get; set; }
        //private string _saltedName { get { return "Dataset:" + Name; } }
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                HasChanged = (_name != value) || HasChanged;

                if (value != null)
                {
                    RenameTo(value);
                    NameIsUserChosen = true;
                }
                else // Setting it to null == un-user-setting it (go back to autogeneration)
                {
                    _name = AutogenerateName();
                    NameIsUserChosen = false;
                }
            }
        }
        public string SavedAsName;

        protected int numExamplesUncategorized = 0;
        //// Storage containers
        //[NonSerialized] protected PersistentDictionary StorageDatabase;
        //[NonSerialized] public static PersistentList<string> DatasetIndex;

        // Static instance
        public static DataSet Current;

        // Utility functions
        protected string AutogenerateName()
        {
            //string salt = Guid.NewGuid().ToString().Substring(0, 5);
            return ((ClassNames.Count == 0) ? "NewDataset" : // + salt :
                    ClassNames.Select(n => n?.Substring(0, (12 / ClassNames.Count).Clamp(2, n.Length)) ?? "").Join("", ""));
        }
        protected void RenameTo(string value)
        {
            //var oldName = _name;
            _name = value;

            //Task.Run(() =>
            //{
            //    // The database is stored according to the dataset name; gotta shift that.
            //    StorageDatabase = PersistentDictionary.Rename(StorageDatabase, value);

            //    // Similarly, we're listed in the index under our name; have to change that too.
            //    DatasetIndex[DatasetIndex.IndexOf(oldName)] = _name;
            //});
        }

        // Stuff that (may) depend on <T> and thus needs to wait for the typed version.
        //public abstract void Save();
        public abstract void Clear();
        public abstract int MinSamplesInAnyClass();
        public abstract void AddSequence(ISequence sample, string classLabel = null, bool skipBitmap = false);
        public abstract void RemoveSequence(int index = -1);
        public abstract void AddClass(string newClassName);
        public abstract void TallySequences(bool RedoFromStart = false);
        public virtual int SequenceCount { get; } = -1;

        public bool HasChanged { get; set; } = false;

        public DataSet Clone() { return (DataSet)this.MemberwiseClone(); }
    }

    [Serializable]
    public class DataSet<T> : DataSet where T : struct
    {
        // Core information
        public BindingList<Sequence<T>> Samples { get; set; }

        // Static instances
        public readonly static DataSet<T> EmptyDataSet = new DataSet<T>();

        public DataSet() : this(null) { }

        public DataSet(string name)
        {
            //// Make sure the master index exists
            //DatasetIndex = DatasetIndex ?? new PersistentList<string>("Atropos|MasterDatasetIndex");

            // Create our basic data containers
            Samples = new BindingList<Sequence<T>>();
            Classes = new List<GestureClass>();

            // Give us a name (initially just "newDataset") and use it to represent us
            _name = (!String.IsNullOrEmpty(name)) ? name : AutogenerateName();
            //StorageDatabase = new PersistentDictionary(Name);
            //if (!DatasetIndex.Contains(Name)) DatasetIndex.Add(Name);

            // If we don't already have a current dataset, then we're it.
            Current = Current ?? this;
        }

        public override void AddClass(string newClassName)
        {
            if (ClassNames.Contains(newClassName) || String.IsNullOrEmpty(newClassName)) return;
            AddClass(new GestureClass() { className = newClassName, index = ClassNames.Count });
        }
        public void AddClass(GestureClass newClass)
        {
            if (ClassNames.Contains(newClass.className))
            {
                // Overwrite the existing class by that name with the data in this one
                int index = Classes.FindIndex(c => c.className == newClass.className);
                Classes[index] = newClass;
            }
            else
            {
                // Add it to the list
                newClass.index = Classes.Count;
                Classes.Add(newClass);
            }

            HasChanged = true;

            // We started out containing a null but if we have even one real one, get rid of that.
            if (Classes.Contains(GestureClass.NullGesture)) Classes.Remove(GestureClass.NullGesture);

            // Our autogenerated names represent the gesture classes strung together & abbreviated, so adding a new gesture class changes this.
            if (!NameIsUserChosen) RenameTo(AutogenerateName());
        }
        public void RemoveClass(GestureClass tgtClass)
        {
            if (!ClassNames.Contains(tgtClass.className)) throw new ArgumentException("Target gesture class does not exist for removal!");

            // Have to run through and relabel all our indices...
            int tgtIndex = tgtClass.index;
            foreach (var seq in Samples.ToArray()) // Make a copy so we don't have issues with removing items midway
            {
                if (seq.TrueClassIndex == tgtIndex) Samples.Remove(seq);
                else
                {
                    if (seq.RecognizedAsIndex == tgtIndex) seq.RecognizedAsIndex = -1; // Un-recognize it.
                    if (seq.TrueClassIndex > tgtIndex) seq.TrueClassIndex--;
                    if (seq.RecognizedAsIndex > tgtIndex) seq.RecognizedAsIndex--;
                }
            }
            foreach (var gC in Classes) if (gC.index > tgtIndex) gC.index--;
            Classes.RemoveAt(tgtIndex);
            TallySequences(true);
            if (!NameIsUserChosen) RenameTo(AutogenerateName());
        }

        // Saving to storage.  Note that this could be simplified to a serialization of the DataSet object, but, hey, it's here...
        //private const string classNameKey = "Dataset|ClassNames";
        //private const string nameKey = "Dataset|Name";
        //private const string gestureClassPrefix = "GestureClass:";
        //private const string sequencePrefix = "Sequence#";
        //public override void Save()
        //{
        //    StorageDatabase.Clear();
        //    StorageDatabase.Put<List<string>>(classNameKey, ClassNames);
        //    StorageDatabase.Put<string>(nameKey, Name);
        //    foreach (var GC in Classes)
        //    {
        //        StorageDatabase.Put<GestureClass>(gestureClassPrefix + GC.className, GC);
        //    }
        //    for (int i = 0; i < Samples.Count; i++)
        //    {
        //        StorageDatabase.Put<Sequence<T>>($"{sequencePrefix}{i}", Samples[i]);
        //    }
        //}
        //public static DataSet<Tnew> Load<Tnew>(string name) where Tnew : struct
        //{
        //    var result = new DataSet<Tnew>();
        //    var storedDatabase = new PersistentDictionary(name);
        //    var classnames = new List<string>();
        //    foreach (string key in storedDatabase.Keys)
        //    {
        //        if (key == classNameKey)
        //        {
        //            classnames = storedDatabase.Get<List<string>>(key);
        //        }
        //        if (key == nameKey)
        //        {
        //            result.Name = storedDatabase.Get(key);
        //        }
        //        if (key.StartsWith(gestureClassPrefix))
        //        {
        //            var gc = storedDatabase.Get<GestureClass>(key);
        //            result.AddClass(gc);
        //        }
        //        if (key.StartsWith(sequencePrefix))
        //        {
        //            var samp = storedDatabase.Get<Sequence<Tnew>>(key);
        //            result.AddSequence(samp, skipBitmap: true);
        //        }
        //    }
        //    Current = result;
        //    return result;
        //}

        //public void Save(Stream stream)
        //{
        //    var serializer = new XmlSerializer(typeof(BindingList<Sequence<T>>));
        //    serializer.Serialize(stream, Samples);
        //}

        //public void Load(Stream stream)
        //{
        //    var serializer = new XmlSerializer(typeof(BindingList<Sequence<T>>));
        //    var samples = (BindingList<Sequence<T>>)serializer.Deserialize(stream);

        //    Clear();

        //    foreach (string label in samples.FirstOrDefault().ClassNames)
        //        AddClass(label);
        //    foreach (Sequence<T> sample in samples)
        //    {
        //        sample.ClassNames = ClassNames;
        //        Samples.Add(sample);
        //    }
        //}

        public override void AddSequence(ISequence sample, string classLabel = null, bool skipBitmap = false)
        {
            //classLabel = classLabel ?? sample.TrueClassName; // Probably unnecessary?  Depends on order of adding new class vs. doing its first gesture.
            AddClass(classLabel); // No-op if it's already present

            //sample.ClassNames = new BindingList<string>(ClassNames);

            Samples.Add(sample as Sequence<T>);

            TallySequences();

            if (sample.TrueClassIndex >= 0)
            {
                //var sampleClass = Classes[sample.TrueClassIndex];
                //sampleClass.numNewExamples++;
                //if (sample.TrueClassIndex == sample.RecognizedAsIndex) sampleClass.numNewExamplesCorrectlyRecognized++;

                // SkipBitmap is used to avoid generating a shwack of unnecessary bitmaps during (e.g.) loading a saved dataset.
                // Other than in that case, the visualization on a GC is that of the most recent sample added to it.
                if (!skipBitmap) Classes[sample.TrueClassIndex].visualization = sample.Bitmap; 
            }
        }

        public override void RemoveSequence(int index = -1)
        {
            index = (index >= 0) ? index : (Samples.Count - 1); // In other words, with no argument, delete the most recent one. [This is the typical case.]
            Samples.RemoveAt(index);
            TallySequences();
        }

        public override void Clear()
        {
            Classes.Clear();
            Samples.Clear();
            numExamplesUncategorized = 0;
            HasChanged = true;
        }

        public override int SequenceCount { get { return Samples.Count; } }

        // Utility function for TallySequences() - easiest way to compare two separately generated List<valueType>s is to stringify them & compare the result.
        private string GetCounts()
        {
            var result = new List<int>();
            foreach (var GC in Classes)
            {
                result.Add(GC.numExamples);
                result.Add(GC.numExamplesCorrectlyRecognized);
                result.Add(GC.numNewExamples);
                result.Add(GC.numNewExamplesCorrectlyRecognized);
            }
            result.Add(numExamplesUncategorized);
            return result.ToString();
        }

        public override void TallySequences(bool RedoFromStart = false)
        {
            var beforeCounts = GetCounts();

            if (RedoFromStart)
            {
                foreach (var GC in Classes)
                {
                    GC.numExamples
                        = GC.numExamplesCorrectlyRecognized
                        = GC.numNewExamples
                        = GC.numNewExamplesCorrectlyRecognized
                            = 0;
                }
                numExamplesUncategorized = 0;

                foreach (var seq in Samples) TallySequence(seq);
            }
            else
            {
                foreach (var GC in Classes)
                {
                    GC.numNewExamples
                        = GC.numNewExamplesCorrectlyRecognized
                            = 0;
                }

                foreach (var seq in Samples.Where(s => !s.HasContributedToClassifier)) TallySequence(seq);
            }

            HasChanged = (beforeCounts != GetCounts()) || HasChanged;
        }

        protected void TallySequence(Sequence<T> seq)
        {
            if (seq.HasContributedToClassifier)
            {
                Classes[seq.TrueClassIndex].numExamples++;
                if (seq.TrueClassIndex == seq.RecognizedAsIndex) Classes[seq.TrueClassIndex].numExamplesCorrectlyRecognized++;
            }
            else if (seq.TrueClassIndex >= 0)
            {
                Classes[seq.TrueClassIndex].numNewExamples++;
                if (seq.TrueClassIndex == seq.RecognizedAsIndex) Classes[seq.TrueClassIndex].numNewExamplesCorrectlyRecognized++;
            }
            else if (seq.RecognizedAsIndex >= 0) // Happens in G&T mode; count it as a new example for what we /think/ it is (until we re-tally)
            {
                Classes[seq.RecognizedAsIndex].numNewExamples++;
            }
            else numExamplesUncategorized++;
        }

        public override int MinSamplesInAnyClass()
        {
            int min = 0;
            foreach (string label in ClassNames)
            {
                int c = Samples.Count(p => p.TrueClassName == label);

                if (min == 0)
                    min = c;

                else if (c < min)
                    min = c;
            }

            return min;
        }

        new public DataSet<T> Clone() { return (DataSet<T>)this.MemberwiseClone(); }

    }
}
