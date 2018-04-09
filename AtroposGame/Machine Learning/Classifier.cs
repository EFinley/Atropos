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
using Atropos.DataStructures;
using DKS = Atropos.DataStructures.DatapointSpecialVariants.DatapointKitchenSink;

namespace Atropos.Machine_Learning
{
    [Serializable]
    public class Classifier : ISerializable 
    {
        private static string _tag = "MachineLearning|Classifier";

        [NonSerialized]
        protected MulticlassSupportVectorMachine<DynamicTimeWarping> svm;

        [NonSerialized]
        protected FeatureExtractor<DKS> featureExtractor;

        public virtual bool MachineOnline { get { return svm != null; } }
        [NonSerialized]
        private DataSet _dataset;
        public DataSet Dataset { get => _dataset; set => _dataset = value; }
        //public bool HasChanged { get; set; } = false;

        public const string FileExtension = "classifier";

        protected int numRetries = 0;
        protected System.Diagnostics.Stopwatch stopwatch;
        protected int retryTimeoutMs = 15000;
        protected int retryLimitCount = 10;

        // These are ONLY set inside deserialized Classifiers, and are used to check compatibility with loaded Datasets.
        public string MatchingDatasetName { get; protected set; }
        public GestureClass[] MatchingDatasetClasses { get; protected set; }
        public int MatchingDatasetSequenceCount { get; protected set; }

        // These are used for preprocessing the sequence data before trying to classify it.
        public PreprocessorCoefficients preprocessorCoefficients { get; set; }

        public virtual double CreateMachine(DataSet<DKS> dataSet, FeatureExtractor<DKS> feature_extractor = null)
        {
            double loss;
            featureExtractor = feature_extractor ?? new FeatureListExtractor("LinAccelVec");

            dataSet.CleanOutNontrainableSequences();
            if (stopwatch == null)
            {
                stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
            }

            try
            {
                var samples = new BindingList<Sequence<DKS>>(dataSet.Samples.Where(s => s.TrueClassIndex >= 0).ToList());
                double[][] inputs = new double[samples.Count][];
                int[] outputs = new int[samples.Count];

                preprocessorCoefficients = featureExtractor.GetPreprocessorCoefficients(samples);

                for (int i = 0; i < inputs.Length; i++)
                {
                    inputs[i] = samples[i].GetMachineInputs(featureExtractor, preprocessorCoefficients);
                    outputs[i] = samples[i].TrueClassIndex;
                }

                // Zero as the length of an input seq. means that it should accept variable numbers of points in an input sequence
                // (which is what we need for DTW).
                svm = new MulticlassSupportVectorMachine<DynamicTimeWarping>(0, new DynamicTimeWarping(featureExtractor.Dimensions), dataSet.ActualGestureClasses.Count);

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
                svm = teacher.Learn(inputs, outputs);

                Dataset = dataSet; // Recorded so we can access it through the Classifier (like inside icebreakers etc).

                // Calibration for probabilistic work... see
                // http://accord-framework.net/docs/html/T_Accord_MachineLearning_VectorMachines_Learning_ProbabilisticOutputCalibration.htm
                // (second example on page)
                var Calibration = new MulticlassSupportVectorLearning<DynamicTimeWarping>()
                {
                    Model = svm, // Process starts with an existing machine

                    Learner = (param) => new ProbabilisticOutputCalibration<DynamicTimeWarping>()
                    {
                        Model = param.Model
                    }
                };

                Calibration.ParallelOptions.MaxDegreeOfParallelism = 1;

                Calibration.Learn(inputs, outputs);

                int[] RecognizedAsClasses = svm.Decide(inputs);
                //double[] Scores = svm.Score(inputs);
                //double[][] LogL = svm.LogLikelihoods(inputs);
                double[][] Probs = svm.Probabilities(inputs);

                for (int i = 0; i < samples.Count; i++)
                {
                    samples[i].RecognizedAsIndex = RecognizedAsClasses[i];
                    //Log.Debug(_tag, $"Sample #{i}: True {samples[i].TrueClassName}, Recog As {samples[i].RecognizedAsName}.  Score is {Scores[i]:f2}, LogLikelihoods are {LogL[i].Select(l => l.ToString("f1")).Join()}, Probabilities {Probs[i].Select(p => p.ToString("f1")).Join()}.");
                }

                double error = new Accord.Math.Optimization.Losses.ZeroOneLoss(outputs).Loss(RecognizedAsClasses);
                loss = new Accord.Math.Optimization.Losses.CategoryCrossEntropyLoss(outputs).Loss(Probs);
                //Log.Debug(_tag, $"Overall zero-one loss is {error:f4}, cross entropy loss is {loss:f4}, whatever the hell those are.");
                //Log.Debug(_tag, $"Classifier creation complete in {stopwatch.ElapsedMilliseconds} ms.");
                stopwatch.Stop();
                stopwatch = null;
                numRetries = 0;
            }
            catch (Exception e)
            {
                Log.Error(_tag, $"Classifier error: {e.ToString()}");
                if (stopwatch.ElapsedMilliseconds < retryTimeoutMs && numRetries < retryLimitCount)
                {
                    numRetries++;
                    BaseActivity.CurrentToaster.RelayToast($"Failed creating classifier; retrying (#{numRetries} after {stopwatch.ElapsedMilliseconds} ms).");
                    loss = CreateMachine(dataSet, feature_extractor);
                }
                else throw new Exception("Outright failure creating classifier!");
            }

            return loss;
        }
        
        public async Task FastAssess(DataSet<DKS> dataSet)
        {
            List<Sequence<DKS>> Samples = dataSet.Samples.ToList();
            foreach (var sample in Samples)
            {
                sample.RecognizedAsIndex = await Recognize(sample);
            }
        }

        public async Task Assess(DataSet<DKS> dataSet, double percentageSampling = 100.0)
        {
            List<Sequence<DKS>> Samples = dataSet.Samples.ToList();
            if (percentageSampling <= 99.9) 
            {
                Samples.Shuffle();
                Samples = Samples.Take((int)Math.Round(Samples.Count * percentageSampling / 100.0)).ToList();
            }
            //foreach (var sample in dataSet.Samples) sample.HasBeenSampled = false;
            foreach (var gC in dataSet.ActualGestureClasses)
            {
                gC.numExamplesSampled = gC.numExamplesSampledCorrectlyRecognized = 0;
                gC.ResetMetadata();
            }

            // Classify selected training instances using the (typically new/updated) classifier
            int i = 0;
            foreach (var sample in Samples)
            {
                //sample.RecognizedAsIndex = svm.Compute(sample.MachineInputs);
                var prevRecog = sample.RecognizedAsName;
                var prevRecogErr = sample.RecognitionScore;
                sample.RecognizedAsIndex = await Recognize(sample);

                // We will only perform sampling of the ones we have a formal classification for.  The few we might have which are still "guessed at" can be ignored.
                if (sample.TrueClassIndex >= 0)
                {
                    dataSet.ActualGestureClasses[sample.TrueClassIndex].numExamplesSampled++;
                    if (sample.RecognizedAsIndex == sample.TrueClassIndex)
                        dataSet.ActualGestureClasses[sample.TrueClassIndex].numExamplesSampledCorrectlyRecognized++;
                    dataSet.ActualGestureClasses[sample.TrueClassIndex].UpdateMetadata(sample.Metadata);
                }
                //sample.HasBeenSampled = true;

                // Debugging time!  Let's take a look at any that have changed their recognition index since last time and see what's happened
                // with their RecognitionError.
                if (prevRecog != "-" &&
                    prevRecog != sample.RecognizedAsName &&
                    (i < 20 || Res.Random * 100.0 < percentageSampling))
                {
                    var prefix = (sample.TrueClassIndex >= 0) ? sample.TrueClassName : "Sample";
                    Log.Debug("Classifier|Assessment", $"{prefix}[{i}] reclassified from {prevRecog} ({prevRecogErr:f3}) to {sample.RecognizedAsName} ({sample.RecognitionScore:f3}).");
                }
                i++;
            }
            dataSet.TallySequences();
        }

        public virtual async Task<int> Recognize(Sequence<DKS> sequence)
        {
            if (sequence.SourcePath.Length < (Dataset?.MinSequenceLength ?? 5))
            {
                Log.Warn("Classifier|Recognize", $"Sequence too short ({sequence.SourcePath.Length} points) to analyze.");
                return -1;
            }

            if (svm == null)
            {
                Log.Warn("Classifier|Recognize", $"SVM is null; cannot analyze sample with that!");
                return -1;
            }

            var score = svm.Score(sequence.GetMachineInputs(featureExtractor, preprocessorCoefficients), out int index);
            // Assign that score to the sequence's metadata.
            var metadata = sequence.Metadata;
            metadata.QualityScore = score;
            sequence.Metadata = metadata;

            // Log.Debug("Classifier|Recognize", $"Recognized sequence as {Dataset.ClassNames[index]} ({index}), with score {score}");

            // If the match is too lousy, call it unknown instead of making a bad call
            if (score < 0.5) index = -1;

            await Task.CompletedTask;
            return index; 
        }

        // I *think* this should work (from the docs on the Accord.IO namespace)... and it does seem to be working.
        public byte[] SerializedString
        {
            get
            {
                if (svm == null) return new byte[0];
                using (MemoryStream stream = new MemoryStream())
                {
                    //svm.Save(stream);
                    Accord.IO.Serializer.Save(svm, stream);
                    return stream.GetBuffer();
                }
            }
            set
            {
                if (value == null || value.Length == 0) return;
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
            MatchingDatasetClasses = (GestureClass[])info.GetValue("Matching_Dataset_Classes", typeof(GestureClass[]));
            MatchingDatasetSequenceCount = (int)info.GetValue("Matching_Dataset_SequenceCount", typeof(int));
            var extractorName = (string)info.GetValue("Feature_Extractor_Name", typeof(string));
            if (!string.IsNullOrEmpty(extractorName)) featureExtractor = new FeatureListExtractor(extractorName);
            preprocessorCoefficients = (PreprocessorCoefficients)info.GetValue("Preprocessor_Coefficients", typeof(PreprocessorCoefficients));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            info.AddValue("SVM_Serialized", SerializedString);
            info.AddValue("Matching_Dataset_Name", Dataset?.Name);
            info.AddValue("Matching_Dataset_Classes", Dataset?.Classes.ToArray());
            info.AddValue("Matching_Dataset_SequenceCount", Dataset?.SequenceCount);
            info.AddValue("Feature_Extractor_Name", featureExtractor?.Name);
            info.AddValue("Preprocessor_Coefficients", preprocessorCoefficients);
        }
    }

    [Serializable]
    public class ClusterClassifier : Classifier
    {
        [NonSerialized]
        private List<Classifier> classifiers = new List<Classifier>();
        public override bool MachineOnline { get => classifiers.All(c => c.MachineOnline); }

        // Required code for serialization/deserialization (since we inherited ISerializable)
        public ClusterClassifier() { }
        protected ClusterClassifier(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            classifiers = (List<Classifier>)info.GetValue("ClusterClassifiers", typeof(List<Classifier>));
        }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ClusterClassifiers", classifiers);
        }

        public override double CreateMachine(DataSet<DKS> dataSet, FeatureExtractor<DKS> feature_extractor = null)
        {
            if (feature_extractor is FeatureClusterExtractor featureExtractor)
            {
                var extractors = featureExtractor.Extractors;
                var losses = new List<double>();
                Dataset = dataSet;

                foreach (var extractor in extractors)
                {
                    var c = (extractor is FeatureClusterExtractor) ? new ClusterClassifier() : new Classifier();
                    classifiers.Add(c);
                    losses.Add(c.CreateMachine(dataSet, extractor));
                }

                //return double.NaN; // Could be losses.Average() but then it ALWAYS is less good than its best single contributor.  Returning NaN means it'll use its weighted accuracy instead.
                return 1.0 / losses.Select(l => 1.0 / l).Sum();
            }
            else
            {
                throw new ArgumentException($"Cannot use anything but a ClusterExtractor inside a ClusterClassifier!");
            }
        }

        public override async Task<int> Recognize(Sequence<DKS> sequence)
        {
            List<int> recognizedIndices = new List<int>();
            List<double> qualityScores = new List<double>();

            foreach (var c in classifiers)
            {
                recognizedIndices.Add(await c.Recognize(sequence));
                qualityScores.Add(sequence.Metadata.QualityScore);
            }

            // Gather the recognized indices into a set of votes.
            var votes = new OrderedDictionary<int, double>();
            for (int i = 0; i < recognizedIndices.Count; i++)
            {
                var thisVote = recognizedIndices[i];
                if (!votes.ContainsKey(thisVote)) votes.Add(thisVote, 0.0);
                votes[thisVote] += 1 + qualityScores[i] * 0.001; // Uses quality scores as the tiebreaker, but actual votes count far more.
            }
            //votes = (OrderedDictionary<int, double>)votes.OrderByDescending(kvp => kvp.Value);
            var sortedVotes = votes.ToList().OrderByDescending(kvp => kvp.Value);

            // Now tally the votes and quote an appropriate quality score (which in turn depends on whether the decision was unanimous or not).
            int winningVote;
            double effectiveScore;
            if (votes.Count == 1)
            {
                winningVote = sortedVotes.First().Key;
                effectiveScore = qualityScores.Average();
            }
            else // There was more than one different recognition made; report the majority vote, but assign it the minimum quality score.
            {
                winningVote = sortedVotes.First().Key;
                effectiveScore = qualityScores.Min();
            }

            // Update the sequence metadata to include our effective score.
            var meta = sequence.Metadata;
            meta.QualityScore = effectiveScore;
            sequence.Metadata = meta;

            // Return the winning index.
            return winningVote;
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

        public void CreateMachine(DataSet<DKS> inDataSet, DataSet<DKS> nullGestureSet = null)
        {
            var dataSet = inDataSet.Clone(); // Create a copy so we don't corrupt the one we were passed.
            highestLegitClassIndex = dataSet.Classes.Count - 1;

            nullDataSet = nullGestureSet ?? nullDataSet ?? new DataSet<DKS>();
            foreach (var gc in nullGestureSet.Classes) dataSet.AddClass(gc);
            foreach (var samp in nullGestureSet.Samples) dataSet.AddSequence(samp);

            CreateMachine(dataSet);
        }

        public override async Task<int> Recognize(Sequence<DKS> sequence)
        {
            var index = await base.Recognize(sequence);

            //if (nullDataSet.ClassNames.Contains(DataSet.ClassNames[index])) index = ISNULL;
            if (index > highestLegitClassIndex) index = ISNULL;
            return index;
        }
    }

    [Serializable]
    public class ClassifierTree : ISerializable
    {
        public List<GestureClass> GestureClasses;
        [NonSerialized] public Classifier MainClassifier;
        [NonSerialized] public Dictionary<GestureClass, Classifier> CueClassifiers;

        public ClassifierTree(DataSet dataset, Classifier classifier, Dictionary<GestureClass, Classifier> cueClassifiers)
        {
            GestureClasses = dataset.ActualGestureClasses;
            MainClassifier = classifier;
            CueClassifiers = cueClassifiers;
        }

        public ClassifierTree(SerializationInfo info, StreamingContext context)
        {
            var mainStr = info.GetString("MainClassifier_Serialized");
            MainClassifier = Serializer.Deserialize<ClusterClassifier>(mainStr) ?? Serializer.Deserialize<Classifier>(mainStr);

            var secondaries = (Dictionary<GestureClass, string>)info.GetValue("CueClassifiers_Serialized", typeof(Dictionary<GestureClass, string>));
            CueClassifiers = new Dictionary<GestureClass, Classifier>();
            foreach (var cc_kvp in secondaries)
            {
                Classifier result = Serializer.Deserialize<ClusterClassifier>(cc_kvp.Value) ?? Serializer.Deserialize<Classifier>(cc_kvp.Value);
                CueClassifiers.Add(cc_kvp.Key, result);
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("MainClassifier_Serialized", Serializer.Serialize(MainClassifier));
            var stringForms = new Dictionary<GestureClass, string>();
            foreach (var cc_kvp in CueClassifiers)
            {
                if (cc_kvp.Value is ClusterClassifier cc) stringForms.Add(cc_kvp.Key, Serializer.Serialize(cc));
                else stringForms.Add(cc_kvp.Key, Serializer.Serialize(cc_kvp.Value));
            }
            info.AddValue("CueClassifiers_Serialized", stringForms);
        }
    }
}