using System;
using System.Collections.Generic;
//using System.Linq;
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

using Log = Android.Util.Log;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using System.Linq;
using com.Atropos.DataStructures;

namespace com.Atropos.Machine_Learning
{
    [Serializable]
    public class Classifier : ISerializable
    {
        [NonSerialized]
        protected MulticlassSupportVectorMachine<DynamicTimeWarping> svm; 
        
        public bool MachineOnline { get { return svm != null; } }
        public DataSet Dataset { get; set; }
        //public bool HasChanged { get; set; } = false;

        public const string FileExtension = "classifier";

        // These are ONLY set inside deserialized Classifiers, and are used to check compatibility with loaded Datasets.
        public string MatchingDatasetName { get; protected set; }
        public string[] MatchingDatasetClasses { get; protected set; }
        public int MatchingDatasetSequenceCount { get; protected set; }

        public void CreateMachine<T>(DataSet<T> dataSet) where T : struct
        {
            BindingList<Sequence<T>> samples = dataSet.Samples;

            double[][] inputs = new double[samples.Count][];
            try
            {
                int[] outputs = new int[samples.Count];

                for (int i = 0; i < inputs.Length; i++)
                {
                    inputs[i] = samples[i].MachineInputs;
                    outputs[i] = samples[i].TrueClassIndex;
                }

                // Zero as the length of an input seq. means that it should accept variable numbers of points in an input sequence
                // (which is what we need for DTW).
                svm = new MulticlassSupportVectorMachine<DynamicTimeWarping>(0, new DynamicTimeWarping(Datapoint<T>.Dimensions), dataSet.ClassNames.Count);

                // Create the learning algorithm to teach the multiple class classifier
                //var teacher = new MulticlassSupportVectorLearning<DynamicTimeWarping>(svm, inputs, outputs)
                //{
                //    Algorithm = (machine, classInputs, classOutputs, i, j) =>
                //       new SequentialMinimalOptimization(machine, classInputs, classOutputs)
                //};
                var teacher = new MulticlassSupportVectorLearning<DynamicTimeWarping>()
                {
                    // Setup the learning algorithm for each 1x1 subproblem
                    Learner = (param) => new SequentialMinimalOptimization<DynamicTimeWarping>()
                    {
                        Kernel = new DynamicTimeWarping(2),
                    }
                };

                foreach (var sample in dataSet.Samples) sample.HasContributedToClassifier = true;

                // Run the learning algorithm
                //double error = teacher.Run();
                //Log.Info("MachineLearning|Classifier", $"Classifier trained with resulting error of {error:f3}.");
                this.svm = teacher.Learn(inputs, outputs);

                Dataset = dataSet; // Recorded so we can access it through the Classifier (like inside icebreakers etc).
            }
            catch (Exception e)
            {
                Log.Error("MachineLearning|Classifier", $"Classifier error: {e.ToString()}");
                throw;
            }
        }
        
        public async Task Assess<T>(DataSet<T> dataSet, double percentageSampling = 100.0) where T : struct
        {
            List<Sequence<T>> Samples = dataSet.Samples.ToList();
            if (percentageSampling <= 99.9) 
            {
                Samples.Shuffle();
                Samples = Samples.Take((int)Math.Round(Samples.Count * percentageSampling / 100.0)).ToList();
            }
            //foreach (var sample in dataSet.Samples) sample.HasBeenSampled = false;
            foreach (var gC in dataSet.Classes) { gC.numExamplesSampled = gC.numExamplesSampledCorrectlyRecognized = 0; }

            // Classify selected training instances using the (typically new/updated) classifier
            int i = 0;
            foreach (var sample in Samples)
            {
                //sample.RecognizedAsIndex = svm.Compute(sample.MachineInputs);
                var prevRecog = sample.RecognizedAsName;
                var prevRecogErr = sample.RecognitionError;
                sample.RecognizedAsIndex = await Recognize(sample);

                // We will only perform sampling of the ones we have a formal classification for.  The few we might have which are still "guessed at" can be ignored.
                if (sample.TrueClassIndex >= 0)
                {
                    dataSet.Classes[sample.TrueClassIndex].numExamplesSampled++;
                    if (sample.RecognizedAsIndex == sample.TrueClassIndex)
                        dataSet.Classes[sample.TrueClassIndex].numExamplesSampledCorrectlyRecognized++;
                }
                //sample.HasBeenSampled = true;

                // Debugging time!  Let's take a look at any that have changed their recognition index since last time and see what's happened
                // with their RecognitionError.
                if (prevRecog != "-" &&
                    prevRecog != sample.RecognizedAsName &&
                    (i < 20 || Res.Random * 100.0 < percentageSampling))
                {
                    var prefix = (sample.TrueClassIndex >= 0) ? sample.TrueClassName : "Sample";
                    Log.Debug("Classifier|Assessment", $"{prefix}[{i}] reclassified from {prevRecog} ({prevRecogErr:f3}) to {sample.RecognizedAsName} ({sample.RecognitionError:f3}).");
                }
                i++;
            }
            dataSet.TallySequences();
        }

        public virtual async Task<int> Recognize<T>(Sequence<T> sequence) where T:struct
        {
            if (sequence.SourcePath.Length < 8)
            {
                Log.Warn("Classifier|Recognize", $"Sequence too short ({sequence.SourcePath.Length} points) to analyze.");
                return -1;
            }

            if (svm == null)
            {
                Log.Warn("Classifier|Recognize", $"SVM is null; cannot analyze sample with that!");
                return -1;
            }

            //// Do the actual calculation!  But try not to hog resources while you're doing it.
            //int index = await Task.Run<int>(() =>
            //{
            //    //double[] input = Sequence<T>.Preprocess(sequence.SourcePath).Merge(Datapoint<T>.Dimensions);
            //    //return svm.Compute(input);
            //    return svm.Compute(sequence.MachineInputs);
            //});

            // Do the actual calculation!  
            //double[] input = Sequence<T>.Preprocess(sequence.SourcePath).Merge(Datapoint<T>.Dimensions);
            //int index = svm.Compute(input);
            //int index = svm.Compute(sequence.MachineInputs);
            double calcError;
            //int index = svm.Compute(sequence.MachineInputs, Accord.MachineLearning.VectorMachines.MulticlassComputeMethod.Voting, out calcError);
            
            // Test whether we can now get actual values for the scoring...
            var score = svm.Score(sequence.MachineInputs, out int index);
            sequence.RecognitionError = score;
            Log.Debug("Classifier|Recognize", $"Recognized sequence as {Dataset.ClassNames[index]} ({index}), with score {score}");

            return await Task.FromResult<int>(index); // Suppresses the async warnings of returning simply 'index'
        }

        // I *think* this should work (from the docs on the Accord.IO namespace)... but for now it's untested.  
        // Be nice to be able to serialize the computed AI, though.
        public byte[] SerializedString
        {
            get
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    //svm.Save(stream);
                    Accord.IO.Serializer.Save(svm, stream);
                    return stream.GetBuffer();
                }
            }
            set
            {
                using (MemoryStream stream = new MemoryStream(value))
                {
                    //svm = MulticlassSupportVectorMachine<DynamicTimeWarping>.Load(stream);
                    svm = Accord.IO.Serializer.Load<MulticlassSupportVectorMachine<DynamicTimeWarping>>(stream);
                }
            }
        }

        public Classifier() { } // Needs to exist explicitly, since serialization requires the protected ctor below.
        protected Classifier(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            SerializedString = (byte[])info.GetValue("SVM_Serialized", typeof(byte[]));
            MatchingDatasetName = (string)info.GetValue("Matching_Dataset_Name", typeof(string));
            MatchingDatasetClasses = (string[])info.GetValue("Matching_Dataset_Classes", typeof(string[]));
            MatchingDatasetSequenceCount = (int)info.GetValue("Matching_Dataset_SequenceCount", typeof(int));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            info.AddValue("SVM_Serialized", SerializedString);
            info.AddValue("Matching_Dataset_Name", Dataset?.Name);
            info.AddValue("Matching_Dataset_Classes", Dataset?.ClassNames.ToArray());
            info.AddValue("Matching_Dataset_SequenceCount", Dataset?.SequenceCount);
        }
    }

    /// <summary>
    /// A specialized classifier intended to contain several null (ignorable) gestures as well as the
    /// desired meaningful classes; Recognize() will return ISNULL if one of the null gestures is the best
    /// match for the gesture in question.  There may be better ways to do this, but this'll do for now.
    /// </summary>
    public class DiscriminatingClassifier : Classifier
    {
        public const int ISNULL = -2;
        private IDataset nullDataSet;
        private int highestLegitClassIndex;

        public void CreateMachine<T>(DataSet<T> inDataSet, DataSet<T> nullGestureSet = null) where T : struct
        {
            var dataSet = inDataSet.Clone(); // Create a copy so we don't corrupt the one we were passed.
            highestLegitClassIndex = dataSet.Classes.Count - 1;

            nullDataSet = nullGestureSet ?? nullDataSet ?? new DataSet<T>();
            foreach (var gc in nullGestureSet.Classes) dataSet.AddClass(gc);
            foreach (var samp in nullGestureSet.Samples) dataSet.AddSequence(samp);

            CreateMachine(dataSet);
        }

        public override async Task<int> Recognize<T>(Sequence<T> sequence)
        {
            var index = await base.Recognize(sequence);

            //if (nullDataSet.ClassNames.Contains(DataSet.ClassNames[index])) index = ISNULL;
            if (index > highestLegitClassIndex) index = ISNULL;
            return index;
        }
    }
}