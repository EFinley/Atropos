using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Atropos.DataStructures;
using static Atropos.DataStructures.DatapointSpecialVariants;
using System.Numerics;

namespace Atropos.Machine_Learning
{

    [Serializable]
    public struct PreprocessorCoefficients
    {
        public double[] Means;
        public double[] Sigmas;
    }

    [Serializable]
    public class FeatureExtractor<Tsource> where Tsource : struct
    {
        private Func<Tsource, double[]> _extractorFunc;

        public virtual string Name { get; set; }
        public bool Crosslink { get; set; } = true;
        public int Dimensions { get; protected set; }

        public virtual double[] Extract(Tsource rawInput)
        {
            return _extractorFunc(rawInput);
        }

        public virtual double[][] ExtractSeq(IList<Tsource> rawInputs, PreprocessorCoefficients? coefficients = null)
        {
            if (rawInputs.Count() == 0) return new double[0][];
            return rawInputs.Select(Extract).ToArray();
        }

        public double[][] Preprocess(double[][] extracted, PreprocessorCoefficients? coefficients = null)
        { 
            // If not using the (more advanced) full-dataset averages & sigmas, calculate them locally here.
            if (coefficients == null)
            {
                var dims = Dimensions;
                double[] currentAxisValues = new double[extracted.Length]; // Just to save on allocations a tiny bit by reusing it within a single axis.
                double[] means = new double[dims];
                double[] sigmas = new double[dims];

                for (int j = 0; j < dims; j++)
                {
                    for (int i = 0; i < extracted.Length; i++)
                    {
                        currentAxisValues[i] = extracted[i][j];
                    }
                    means[j] = currentAxisValues.Average();
                    sigmas[j] = Accord.Statistics.Measures.StandardDeviation(currentAxisValues);
                }
                sigmas = CrosslinkAxes.Apply<Tsource>(sigmas); // Locks values in the same space - like X and Y axes - to the same window size.
                coefficients = new PreprocessorCoefficients() { Means = means, Sigmas = sigmas };
            }

            // Calculate the z-scores, then make them positive by the simple expedient of adding ten to them.  (Yes, yes, but ten-sigma? Gotta mean an error anyway.)
            double[][] zscores = Accord.Statistics.Tools.ZScores(extracted, coefficients.Value.Means, coefficients.Value.Sigmas);
            return Accord.Math.Elementwise.Add(zscores, 10);
        }

        public virtual void Reset()
        {
            // No-op for the general extractor - basically this is used for the integrator variants.
        }

        public FeatureExtractor(int? dimensions = null, Func<Tsource, double[]> extractorFunc = null)
        {
            Dimensions = dimensions ?? Datapoint<Tsource>.Dimensions;
            _extractorFunc = extractorFunc;
            if (extractorFunc == null)
            {
                _extractorFunc = (vec) =>
                {
                    var result = new double[Dimensions];
                    var source = Datapoint.AsDblArrayFrom(vec);
                    for (int i = 0; i < Dimensions; ++i)
                    {
                        if (i < Datapoint.From<Tsource>(default(Tsource)).Dimensions) result[i] = source[i];
                        else result[i] = 0.0;
                    }
                    return result;
                };
            }
        }

        /// <summary>
        /// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        /// </summary>
        /// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        /// <param name="crosslinkFunc">A function to correlate sigmas in sets of features which should share a common window size.  Leave blank to use the normal "max sigma in any one axis" funtion, or use <see cref="CrosslinkAxes.None"/> for no crosslinking at all.</param>
        /// <returns></returns>
        [Obsolete]
        internal PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<Tsource>> sampleSet, Func<double[], double[]> crosslinkFunc = null)
        {
            int dims = Datapoint<Tsource>.Dimensions;
            double[] means = new double[dims];
            double[] sigmas = new double[dims];
            List<double[]> allValues = new List<double[]>();

            foreach (var sequence in sampleSet.Select(seq => seq.SourcePath))
            {
                if (sequence.Length == 0) continue;

                //double[][] result = new double[sequence.Length][];
                //for (int i = 0; i < sequence.Length; i++)
                //    //result[i] = new double[] { sequence[i].X, sequence[i].Y };
                //    results[i] = ((IDatapoint)sequence[i])
                //        .AsArray()
                //        //.Cast<double>()
                //        .Select(f => (double)f)
                //        .ToArray();

                foreach (var pt in sequence)
                {
                    allValues.Add(Datapoint.From<Tsource>(pt).AsArray().Cast<float, double>().ToArray());
                }
            }

            double[] currentAxisValues = new double[allValues.Count]; // Just to save on allocations a tiny bit by reusing it within a single axis.

            for (int j = 0; j < dims; j++)
            {
                for (int i = 0; i < allValues.Count; i++)
                {
                    currentAxisValues[i] = allValues[i][j];
                }
                means[j] = currentAxisValues.Average();
                sigmas[j] = Accord.Statistics.Measures.StandardDeviation(currentAxisValues);
            }

            crosslinkFunc = crosslinkFunc ?? (Func<double[], double[]>)((s) => CrosslinkAxes.Apply<Tsource>(s));
            sigmas = crosslinkFunc(sigmas); // Locks values in the same space - like X and Y axes - to the same window size.

            return new PreprocessorCoefficients() { Means = means, Sigmas = sigmas };
        }
        [Obsolete]
        public Func<IList<Tsource>, double[][]> CreatePreprocessorFunction(PreprocessorCoefficients coefficients)
        {
            var means = coefficients.Means;
            var sigmas = coefficients.Sigmas;

            return (sequence) =>
            {
                if (sequence.Count == 0) return new double[0][];

                double[][] result = new double[sequence.Count][];
                for (int i = 0; i < sequence.Count; i++)
                    result[i] = Datapoint.AsDblArrayFrom<Tsource>(sequence[i]);

                double[][] zscores = Accord.Statistics.Tools.ZScores(result, means, sigmas);

                return Accord.Math.Elementwise.Add(zscores, 10);
            };
        }        
    }

    public class FeatureListExtractor : FeatureExtractor<DatapointKitchenSink>
    {
        public const string PULL = "Pull_";
        public const string PULLX = "Pull_X_";
        public const string PULLY = "Pull_Y_";
        public const string PULLZ = "Pull_Z_";
        public const string INTEGRATE = "Integrate_";
        public const string DBLINTEGRAL = "DoubleIntegral_";  // Different conjugation means that it'll never overlap, even partially.

        private string _name;
        public override string Name
        {
            get { return _name; }
            set { throw new Exception("Cannot assign a new name to a FeatureListExtractor!"); }
        }

        [NonSerialized]
        public static OrderedDictionary<string, FeatureExtractor<DatapointKitchenSink>> AllExtractors
            = new OrderedDictionary<string, FeatureExtractor<DatapointKitchenSink>>();
        protected static void DefineExtractor(string name, FeatureExtractor<DatapointKitchenSink> extractor)
        {
            extractor.Name = name;
            AllExtractors.Add(name, extractor);
        }

        public List<FeatureExtractor<DatapointKitchenSink>> Extractors;

        public override double[] Extract(DatapointKitchenSink rawInput)
        {
            List<double> extractedResults = new List<double>();
            foreach (var extractor in Extractors)
            {
                extractedResults.AddRange(extractor.Extract(rawInput));
            }
            return extractedResults.ToArray();
        }

        public override void Reset()
        {
            foreach (var ex in Extractors) ex.Reset();
        }
        
        /// <summary>
        /// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        /// </summary>
        /// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        /// <param name="crosslinkFunc">A function to correlate sigmas in sets of features which should share a common window size.  Leave blank to use the normal "max sigma in any one axis" funtion, or use <see cref="CrosslinkAxes.None"/> for no crosslinking at all.</param>
        /// <returns></returns>
        public PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<DatapointKitchenSink>> source)
        {
            var extractedValues = source.Select(s => ExtractSeq(s.SourcePath));
            return GetPreprocessorCoefficients(extractedValues);
        }

        /// <summary>
        /// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        /// </summary>
        /// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        /// <param name="crosslinkFunc">A function to correlate sigmas in sets of features which should share a common window size.  Leave blank to use the normal "max sigma in any one axis" funtion, or use <see cref="CrosslinkAxes.None"/> for no crosslinking at all.</param>
        /// <returns></returns>
        public PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<double[][]> sampleSet)
        {
            double[] means = new double[Dimensions];
            double[] sigmas = new double[Dimensions];
            List<double[]> allValues = new List<double[]>();

            // Flatten them into simple lists of values taken (with safeguards against zero or null).
            foreach (var sequence in sampleSet)
            {
                if (sequence == null || sequence.Length == 0) continue;
                foreach (var pt in sequence)
                {
                    if (pt == null) continue;
                    allValues.Add(pt);
                }
            }

            // For each axis, find the mean and standard deviation of all the data points.
            double[] currentAxisValues = new double[allValues.Count]; // Just to save on allocations a tiny bit by reusing it within a single axis.
            for (int j = 0; j < Dimensions; j++)
            {
                for (int i = 0; i < allValues.Count; i++)
                {
                    currentAxisValues[i] = allValues[i][j];
                }
                means[j] = currentAxisValues.Average();
                sigmas[j] = Accord.Statistics.Measures.StandardDeviation(currentAxisValues);
            }

            int numValuesSkipped = 0;
            int numValuesTaken;
            List<double> crossCorrelatedSigmas = new List<double>();
            foreach (var extractor in Extractors)
            {
                numValuesTaken = extractor.Dimensions;
                var subSet = sigmas.Skip(numValuesSkipped).Take(numValuesTaken);
                if (extractor.Crosslink)
                    crossCorrelatedSigmas.AddRange(Enumerable.Repeat(subSet.Max(), numValuesTaken));
                else crossCorrelatedSigmas.AddRange(subSet);
                numValuesSkipped += numValuesTaken;
            }

            return new PreprocessorCoefficients() { Means = means, Sigmas = crossCorrelatedSigmas.ToArray() };
        }

        public FeatureListExtractor(params string[] extractorNames)
        {
            Dimensions = 0;
            Extractors = new List<FeatureExtractor<DatapointKitchenSink>>();

            // Parse the requested extractors
            foreach (var extractorName in extractorNames)
            {
                //int numCrosslinkedAxes;
                if (AllExtractors.ContainsKey(extractorName))
                {
                    Extractors.Add(AllExtractors[extractorName]);
                    //numCrosslinkedAxes = AllExtractors[extractorName].Dimensions;
                }
                else if (extractorName.StartsWith(PULL))
                {
                    var subExtrName = extractorName.Substring(PULLX.Length);
                    int axisNum = (extractorName.StartsWith(PULLX) ? 0 : (extractorName.StartsWith(PULLY) ? 1 : 2));
                    if (!AllExtractors.ContainsKey(subExtrName)) throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
                    Extractors.Add(new FeatureAxisSelector<DatapointKitchenSink>(axisNum, AllExtractors[subExtrName]) { Name = extractorName });
                    //numCrosslinkedAxes = AllExtractors[subExtrName].Dimensions;
                }
                else if (extractorName.StartsWith(INTEGRATE))
                {
                    var subExtrName = extractorName.Substring(INTEGRATE.Length);
                    if (!AllExtractors.ContainsKey(subExtrName)) throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
                    Extractors.Add(new FeatureIntegrator<DatapointKitchenSink>(AllExtractors[subExtrName]) { Name = extractorName });
                    //numCrosslinkedAxes = AllExtractors[subExtrName].Dimensions;
                }
                else if (extractorName.StartsWith(DBLINTEGRAL))
                {
                    var subExtrName = extractorName.Substring(DBLINTEGRAL.Length);
                    if (!AllExtractors.ContainsKey(subExtrName)) throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
                    Extractors.Add(new FeatureDblIntegrator<DatapointKitchenSink>(AllExtractors[subExtrName]) { Name = extractorName });
                    //numCrosslinkedAxes = AllExtractors[subExtrName].Dimensions;
                }
                else throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");

                //Dimensions += numCrosslinkedAxes;
            }

            _name = Extractors.Select(ex => ex.Name).Join("|", "<>");
            Dimensions = Extractors.Sum(ex => ex.Dimensions);
        }

        static FeatureListExtractor()
        {
            DefineExtractor("LinAccelMag", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.LinAccel.Length() }));
            DefineExtractor("LinAccelVec", new FeatureExtractor<DatapointKitchenSink>(3, v => Datapoint.AsDblArrayFrom(v.LinAccel)));
            DefineExtractor("GravityVec", new FeatureExtractor<DatapointKitchenSink>(3, v => Datapoint.AsDblArrayFrom(v.Gravity)));
            DefineExtractor("GyroMag", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.Gyro.Length() }));
            DefineExtractor("GyroVec", new FeatureExtractor<DatapointKitchenSink>(3, v => Datapoint.AsDblArrayFrom(v.Gyro)));
            DefineExtractor("RotAngle", new FeatureExtractor<DatapointKitchenSink>(1, v =>  new double[] { v.Orientation.AsAngle() }));
            DefineExtractor("RotAxis", new FeatureExtractor<DatapointKitchenSink>(3, v => Datapoint.AsDblArrayFrom(v.Orientation.XYZ().Normalize())));
            DefineExtractor("RotQuat", new FeatureExtractor<DatapointKitchenSink>(4, v => Datapoint.AsDblArrayFrom(v.Orientation)));
            DefineExtractor("AccelXRot", new FeatureExtractor<DatapointKitchenSink>(3, v => 
                Datapoint.AsDblArrayFrom( v.LinAccel.RotatedBy(v.Orientation) )));
            DefineExtractor("AccelXRotInv", new FeatureExtractor<DatapointKitchenSink>(3, v =>
                Datapoint.AsDblArrayFrom(v.LinAccel.RotatedBy(v.Orientation.Inverse()))));
            DefineExtractor("AccelParallelToG", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.LinAccel.Dot(v.Gravity.Normalize()) }));
            DefineExtractor("AccelPerpToG", new FeatureExtractor<DatapointKitchenSink>(1, v => 
            {
                var gHat = v.Gravity.Normalize();
                return new double[]
                {
                    (double)(v.LinAccel - gHat * (v.LinAccel.Dot(gHat))).Length()
                };
            }));
            DefineExtractor("AngleBetweenAccelAndG", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.LinAccel.AngleTo(v.Gravity) }));
            DefineExtractor("AccelParallelToGyroAxis", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.LinAccel.Dot(v.Gyro.Normalize()) }));
            DefineExtractor("AccelPerpToGyroAxis", new FeatureExtractor<DatapointKitchenSink>(1, v =>
            {
                var gHat = v.Gyro.Normalize();
                return new double[]
                {
                    (double)(v.LinAccel - gHat * (v.LinAccel.Dot(gHat))).Length()
                };
            }));
            DefineExtractor("AngleBetweenAccelAndGyroAxis", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.LinAccel.AngleTo(v.Gyro) }));
            DefineExtractor("GyroParallelToGravity", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.Gyro.Dot(v.Gravity.Normalize()) }));
            DefineExtractor("GyroPerpToGravity", new FeatureExtractor<DatapointKitchenSink>(1, v =>
            {
                var gHat = v.Gravity.Normalize();
                return new double[]
                {
                    (double)(v.Gyro - gHat * (v.Gyro.Dot(gHat))).Length()
                };
            }));
            DefineExtractor("AngleBetweenGravAndGyroAxis", new FeatureExtractor<DatapointKitchenSink>(1, v => new double[] { v.Gravity.AngleTo(v.Gyro) }));
        }
    }

    public class FeatureAxisSelector<T> : FeatureExtractor<T> where T : struct
    {
        public int SelectedAxis;
        private FeatureExtractor<T> SourceExtractor;

        public FeatureAxisSelector(int selectedAxis, FeatureExtractor<T> sourceExtractor)
        {
            if (selectedAxis >= sourceExtractor.Dimensions) throw new ArgumentException($"Cannot take the {selectedAxis}th axis of an extractor which produces only {sourceExtractor.Dimensions} dimensions.");
            SelectedAxis = selectedAxis;
            SourceExtractor = sourceExtractor;

            // By definition this extractor returns a single dimensional object.
            Dimensions = 1;
        }

        public override double[] Extract(T rawInput)
        {
            var primaryResults = SourceExtractor.Extract(rawInput);
            return new double[] { primaryResults[SelectedAxis] };
        }
    }

    public class FeatureIntegrator<T> : FeatureExtractor<T> where T : struct, IInterval
    {
        public double[] CumulativeValue { get; private set; }
        private FeatureExtractor<T> SourceExtractor;

        public FeatureIntegrator(FeatureExtractor<T> sourceExtractor)
        {
            SourceExtractor = sourceExtractor;
            Dimensions = sourceExtractor.Dimensions;
            CumulativeValue = Enumerable.Repeat(0.0, Dimensions).ToArray();
        }

        public override double[] Extract(T rawInput)
        {
            var primaryResults = SourceExtractor.Extract(rawInput);
            var interval = rawInput.Interval;
            for (int i = 0; i < Dimensions; ++i)
            {
                CumulativeValue[i] += primaryResults[i] * interval;
            }
            return CumulativeValue;
        }

        public override void Reset()
        {
            CumulativeValue = Enumerable.Repeat(0.0, Dimensions).ToArray();
        }
    }

    public class FeatureDblIntegrator<T> : FeatureExtractor<T> where T : struct, IInterval
    {
        public double[] CumulativeValue { get; private set; }
        public double[] CurrentMomentum { get; private set; }
        private FeatureExtractor<T> SourceExtractor;

        public FeatureDblIntegrator(FeatureExtractor<T> sourceExtractor)
        {
            SourceExtractor = sourceExtractor;
            Dimensions = sourceExtractor.Dimensions;
            CumulativeValue = Enumerable.Repeat(0.0, Dimensions).ToArray();
            CurrentMomentum = Enumerable.Repeat(0.0, Dimensions).ToArray();
        }

        public override double[] Extract(T rawInput)
        {
            var primaryResults = SourceExtractor.Extract(rawInput);
            var interval = rawInput.Interval;
            for (int i = 0; i < Dimensions; ++i)
            {
                CumulativeValue[i] += CurrentMomentum[i] * interval + 0.5 * primaryResults[i] * interval * interval;
                CurrentMomentum[i] += primaryResults[i] * interval;
            }
            return CumulativeValue;
        }

        public override void Reset()
        {
            CumulativeValue = Enumerable.Repeat(0.0, Dimensions).ToArray();
            CurrentMomentum = Enumerable.Repeat(0.0, Dimensions).ToArray();
        }
    }
}