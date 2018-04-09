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
using static System.Math;

namespace Atropos.Machine_Learning
{

    [Serializable]
    public struct PreprocessorCoefficients
    {
        public double[] Means;
        public double[] Sigmas;
    }

    [Serializable]
    public class FeatureExtractor<Tin> where Tin : struct
    {
        private Func<Tin, double[]> _extractorFunc;

        public virtual string Name { get; set; }
        public bool doCrosslink { get; set; } = true;
        public int Dimensions { get; protected set; }

        //public double timebase = 25;
        //public double CrossEntropyLoss { get; set; } = 100; // Somewhat worse than bad
        //public double MillisecondsPerPt { get; set; } = 25; // Ditto
        //public double CorrectRecognitionPct { get; set; } = 0;
        //public double ExtractorScore { get => (1.0 / CrossEntropyLoss) * Exp(-Pow(MillisecondsPerPt / timebase, 2)); }
        public ClassifierMetrics metrics;

        public virtual double[] Extract(Tin rawInput)
        {
            return _extractorFunc(rawInput);
        }

        public virtual double[][] ExtractSeq(IList<Tin> rawInputs, PreprocessorCoefficients? coefficients = null)
        {
            if (rawInputs.Count() == 0) return new double[0][];
            return rawInputs.Select(Extract).ToArray();
        }

        public double[][] Preprocess(double[][] extracted, PreprocessorCoefficients? coefficients = null)
        { 
            // If not using the (more advanced) full-dataset averages & sigmas, calculate them locally here.
            if (coefficients == null)
            {
                //var dims = Datapoint.From<Tsource>(default(Tsource)).Dimensions;
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

                // Use our standard crosslinking function to lock values which should share a normalization constant into the same scale as each other.
                sigmas = CrosslinkAxes.Apply<Tin>(sigmas); // Locks values in the same space - like X and Y axes - to the same window size.
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

        public FeatureExtractor(int? dimensions = null, Func<Tin, double[]> extractorFunc = null)
        {
            Dimensions = dimensions ?? Datapoint.From(default(Tin)).Dimensions;
            _extractorFunc = extractorFunc;
            if (extractorFunc == null)
            {
                _extractorFunc = (vec) =>
                {
                    var result = new double[Dimensions];
                    var source = Datapoint.AsDblArrayFrom(vec);
                    for (int i = 0; i < Dimensions; ++i)
                    {
                        if (i < Datapoint.From<Tin>(default(Tin)).Dimensions) result[i] = source[i];
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
        public virtual PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<Tin>> source)
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
        public virtual PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<double[][]> sampleSet)
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

            if (doCrosslink) sigmas = Crosslink(sigmas);

            return new PreprocessorCoefficients() { Means = means, Sigmas = sigmas };
        }

        public virtual double[] Crosslink(double[] sigmas)
        {
            return Enumerable.Repeat(sigmas.Max(), sigmas.Length).ToArray();
        }

        ///// <summary>
        ///// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        ///// </summary>
        ///// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        ///// <returns></returns>
        //public virtual PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<Tsource>> sampleSet)
        //{
        //    //int dims = Datapoint.From(default(Tsource)).Dimensions;
        //    int dims = Dimensions;
        //    double[] means = new double[dims];
        //    double[] sigmas = new double[dims];
        //    List<double[]> allValues = new List<double[]>();

        //    foreach (var sequence in sampleSet.Select(seq => seq.SourcePath))
        //    {
        //        if (sequence.Length == 0) continue;

        //        //double[][] result = new double[sequence.Length][];
        //        //for (int i = 0; i < sequence.Length; i++)
        //        //    //result[i] = new double[] { sequence[i].X, sequence[i].Y };
        //        //    results[i] = ((IDatapoint)sequence[i])
        //        //        .AsArray()
        //        //        //.Cast<double>()
        //        //        .Select(f => (double)f)
        //        //        .ToArray();

        //        foreach (var pt in sequence)
        //        {
        //            allValues.Add(Datapoint.From<Tsource>(pt).AsArray().Cast<float, double>().ToArray());
        //        }
        //    }

        //    double[] currentAxisValues = new double[allValues.Count]; // Just to save on allocations a tiny bit by reusing it within a single axis.

        //    for (int j = 0; j < dims; j++)
        //    {
        //        for (int i = 0; i < allValues.Count; i++)
        //        {
        //            currentAxisValues[i] = allValues[i][j];
        //        }
        //        means[j] = currentAxisValues.Average();
        //        sigmas[j] = Accord.Statistics.Measures.StandardDeviation(currentAxisValues);
        //    }

        //    sigmas = CrosslinkAxes.Apply<Tsource>(sigmas); // Locks values in the same space - like X and Y axes - to the same window size.

        //    return new PreprocessorCoefficients() { Means = means, Sigmas = sigmas };
        //}

        [Obsolete]
        public Func<IList<Tin>, double[][]> CreatePreprocessorFunction(PreprocessorCoefficients coefficients)
        {
            var means = coefficients.Means;
            var sigmas = coefficients.Sigmas;

            return (sequence) =>
            {
                if (sequence.Count == 0) return new double[0][];

                double[][] result = new double[sequence.Count][];
                for (int i = 0; i < sequence.Count; i++)
                    result[i] = Datapoint.AsDblArrayFrom<Tin>(sequence[i]);

                double[][] zscores = Accord.Statistics.Tools.ZScores(result, means, sigmas);

                return Accord.Math.Elementwise.Add(zscores, 10);
            };
        }        
    }

    public class FeatureExtractor<Tin, Tout> : FeatureExtractor<Tin> 
        where Tin : struct 
        where Tout : struct
    {
        public FeatureExtractor(Func<Tin, double[]> extractorFunc) : base(Datapoint.From(default(Tout)).Dimensions, extractorFunc)
        {

        }

        public override double[] Crosslink(double[] sigmas)
        {
            return CrosslinkAxes.Apply<Tout>(sigmas);
        }
    }

    public class FeatureListExtractor : FeatureExtractor<DatapointKitchenSink>
    {
        public const string PULL = "Pull_";
        public const string PULLX = "Pull_X_";
        public const string PULLY = "Pull_Y_";
        public const string PULLZ = "Pull_Z_";
        public const string INTEGRATE = "Integrate_";
        public const string DBLINTEGRAL = "DoubleIntegral_";  // Different conjugation means that it'll never overlap with INTEGRATE, even partially.  Makes search and replace easier.

        private string _name;
        public override string Name
        {
            get { return _name; }
            set { throw new InvalidOperationException("Cannot assign a new name to a FeatureListExtractor!"); }
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
        
        ///// <summary>
        ///// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        ///// </summary>
        ///// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        ///// <param name="crosslinkFunc">A function to correlate sigmas in sets of features which should share a common window size.  Leave blank to use the normal "max sigma in any one axis" funtion, or use <see cref="CrosslinkAxes.None"/> for no crosslinking at all.</param>
        ///// <returns></returns>
        //public override PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<DatapointKitchenSink>> source)
        //{
        //    var extractedValues = source.Select(s => ExtractSeq(s.SourcePath));
        //    return GetPreprocessorCoefficients(extractedValues);
        //}

        ///// <summary>
        ///// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        ///// </summary>
        ///// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        ///// <param name="crosslinkFunc">A function to correlate sigmas in sets of features which should share a common window size.  Leave blank to use the normal "max sigma in any one axis" funtion, or use <see cref="CrosslinkAxes.None"/> for no crosslinking at all.</param>
        ///// <returns></returns>
        //public override PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<double[][]> sampleSet)
        //{
        //    double[] means = new double[Dimensions];
        //    double[] sigmas = new double[Dimensions];
        //    List<double[]> allValues = new List<double[]>();

        //    // Flatten them into simple lists of values taken (with safeguards against zero or null).
        //    foreach (var sequence in sampleSet)
        //    {
        //        if (sequence == null || sequence.Length == 0) continue;
        //        foreach (var pt in sequence)
        //        {
        //            if (pt == null) continue;
        //            allValues.Add(pt);
        //        }
        //    }

        //    // For each axis, find the mean and standard deviation of all the data points.
        //    double[] currentAxisValues = new double[allValues.Count]; // Just to save on allocations a tiny bit by reusing it within a single axis.
        //    for (int j = 0; j < Dimensions; j++)
        //    {
        //        for (int i = 0; i < allValues.Count; i++)
        //        {
        //            currentAxisValues[i] = allValues[i][j];
        //        }
        //        means[j] = currentAxisValues.Average();
        //        sigmas[j] = Accord.Statistics.Measures.StandardDeviation(currentAxisValues);
        //    }

        //    if (doCrosslink) sigmas = Crosslink(sigmas);

        //    return new PreprocessorCoefficients() { Means = means, Sigmas = sigmas };
        //}

        public override double[] Crosslink(double[] sigmas)
        {
            int numValuesSkipped = 0;
            int numValuesTaken;
            List<double> crossCorrelatedSigmas = new List<double>();
            foreach (var extractor in Extractors)
            {
                numValuesTaken = extractor.Dimensions;
                var subSet = sigmas.Skip(numValuesSkipped).Take(numValuesTaken);
                if (extractor.doCrosslink)
                    crossCorrelatedSigmas.AddRange(extractor.Crosslink(subSet.ToArray()));
                else crossCorrelatedSigmas.AddRange(subSet);
                numValuesSkipped += numValuesTaken;
            }

            return crossCorrelatedSigmas.ToArray();
        }

        public FeatureListExtractor(params string[] extractor_names)
        {
            // Unpack any nested extractors which might be present in the list.
            var extractorNames = new HashSet<string>(extractor_names);
            while (extractorNames.Any(exN => exN.Contains("|")))
            {
                var multiExtractor = extractorNames.First(exN => exN.Contains("|"));
                extractorNames.Remove(multiExtractor);
                multiExtractor = multiExtractor.Trim('<', '>');
                extractorNames.UnionWith(multiExtractor.Split('|'));
            }

            Dimensions = 0;
            Extractors = new List<FeatureExtractor<DatapointKitchenSink>>();

            // Parse the requested extractors
            foreach (var extractorName in extractorNames)
            {
                if (AllExtractors.ContainsKey(extractorName))
                {
                    Extractors.Add(AllExtractors[extractorName]);
                }
                else if (FeatureAbsoluteAxisSelector<DatapointKitchenSink>.axisNames.Contains(extractorName))
                {
                    var index = Accord.Math.Matrix.IndexOf(FeatureAbsoluteAxisSelector<DatapointKitchenSink>.axisNames, extractorName);
                    Extractors.Add(new FeatureAbsoluteAxisSelector<DatapointKitchenSink>(index));
                }
                else if (extractorName.Contains("&"))
                {
                    Extractors.Add(new FeatureClusterExtractor(extractorName));
                }
                else if (extractorName.StartsWith(PULL))
                {
                    // Two options here: PULL_5 and so forth, which indicate an absolute axis number (out of our max of 14), or...
                    if (int.TryParse(extractorName.Substring(PULL.Length), out int index))
                    {
                        Extractors.Add(new FeatureAbsoluteAxisSelector<DatapointKitchenSink>(index));
                    }

                    // ...PULL_X_LinAccelVec or what have you, which indicate an axis out of an existing extractor.
                    else
                    { 
                        var subExtrName = extractorName.Substring(PULLX.Length);
                        int axisNum = (extractorName.StartsWith(PULLX) ? 0 : (extractorName.StartsWith(PULLY) ? 1 : 2));
                        //if (!AllExtractors.ContainsKey(subExtrName)) throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
                        // TODO (Optional): Instead of throwing here, check if it starts with INTEGRATE/DBLINTEGRAL and if so use an appropriate FeatureListExtractor(substring) as the extractor here.
                        //Extractors.Add(new FeatureAxisSelector<DatapointKitchenSink>(axisNum, AllExtractors[subExtrName]) { Name = extractorName });
                        Extractors.Add(new FeatureAxisSelector<DatapointKitchenSink>(axisNum, new FeatureListExtractor(subExtrName)) { Name = extractorName });
                    }
                }
                else if (extractorName.StartsWith(INTEGRATE))
                {
                    var subExtrName = extractorName.Substring(INTEGRATE.Length);
                    //if (!AllExtractors.ContainsKey(subExtrName)) throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
                    //Extractors.Add(new FeatureIntegrator<DatapointKitchenSink>(AllExtractors[subExtrName]) { Name = extractorName });
                    Extractors.Add(new FeatureIntegrator<DatapointKitchenSink>(new FeatureListExtractor(subExtrName)) { Name = extractorName });
                }
                else if (extractorName.StartsWith(DBLINTEGRAL))
                {
                    var subExtrName = extractorName.Substring(DBLINTEGRAL.Length);
                    //if (!AllExtractors.ContainsKey(subExtrName)) throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
                    //Extractors.Add(new FeatureDblIntegrator<DatapointKitchenSink>(AllExtractors[subExtrName]) { Name = extractorName });
                    Extractors.Add(new FeatureDblIntegrator<DatapointKitchenSink>(new FeatureListExtractor(subExtrName)) { Name = extractorName });
                }
                else throw new ArgumentException($"Unable to parse extractor '{extractorName}'.");
            }

            _name = (Extractors.Count > 1) ? Extractors.Select(ex => ex.Name).Join("|", "<>") : Extractors[0].Name;
            Dimensions = Extractors.Sum(ex => ex.Dimensions);
        }

        static FeatureListExtractor()
        {
            DefineExtractor("LinAccelMag", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.LinAccel.Length() }));
            DefineExtractor("LinAccelVec", new FeatureExtractor<DatapointKitchenSink, Vector3>(v => Datapoint.AsDblArrayFrom(v.LinAccel)));
            DefineExtractor("GravityVec", new FeatureExtractor<DatapointKitchenSink, Vector3>(v => Datapoint.AsDblArrayFrom(v.Gravity)));
            DefineExtractor("GyroMag", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.Gyro.Length() }));
            DefineExtractor("GyroVec", new FeatureExtractor<DatapointKitchenSink, Vector3>(v => Datapoint.AsDblArrayFrom(v.Gyro)));
            DefineExtractor("RotAngle", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.Orientation.AsAngle() }));
            DefineExtractor("RotAxis", new FeatureExtractor<DatapointKitchenSink, Vector3>(v =>
            {
                //var axis = ;
                //if (axis.LengthSquared() < 1e-8) return new double[] { 0, 0, 0 };
                return Datapoint.AsDblArrayFrom(v.Orientation.XYZ().Normalize());
            }));
            DefineExtractor("RotQuat", new FeatureExtractor<DatapointKitchenSink, Quaternion>(v => Datapoint.AsDblArrayFrom(v.Orientation)));
            DefineExtractor("AccelXRot", new FeatureExtractor<DatapointKitchenSink, Vector3>(v => 
                Datapoint.AsDblArrayFrom( v.LinAccel.RotatedBy(v.Orientation) )));
            DefineExtractor("AccelXRotInv", new FeatureExtractor<DatapointKitchenSink, Vector3>(v =>
                Datapoint.AsDblArrayFrom(v.LinAccel.RotatedBy(v.Orientation.Inverse()))));
            DefineExtractor("AccelParallelToG", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.LinAccel.Dot(v.Gravity.Normalize()) }));
            DefineExtractor("AccelPerpToG", new FeatureExtractor<DatapointKitchenSink, double>(v => 
            {
                if (v.Gravity.LengthSquared() < 1e-8) return new double[] { 0 };
                var gHat = v.Gravity.Normalize();
                return new double[]
                {
                    (double)(v.LinAccel - gHat * (v.LinAccel.Dot(gHat))).Length()
                };
            }));
            DefineExtractor("AngleBetweenAccelAndG", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.LinAccel.AngleTo(v.Gravity) }));
            DefineExtractor("AccelParallelToGyroAxis", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.LinAccel.Dot(v.Gyro.Normalize()) }));
            DefineExtractor("AccelPerpToGyroAxis", new FeatureExtractor<DatapointKitchenSink, double>(v =>
            {
                if (v.Gyro.LengthSquared() < 1e-8) return new double[] { 0 };
                var gHat = v.Gyro.Normalize();
                return new double[]
                {
                    (double)(v.LinAccel - gHat * (v.LinAccel.Dot(gHat))).Length()
                };
            }));
            DefineExtractor("AngleBetweenAccelAndGyroAxis", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.LinAccel.AngleTo(v.Gyro) }));
            DefineExtractor("GyroParallelToGravity", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.Gyro.Dot(v.Gravity.Normalize()) }));
            DefineExtractor("GyroPerpToGravity", new FeatureExtractor<DatapointKitchenSink, double>(v =>
            {
                if (v.Gravity.LengthSquared() < 1e-8) return new double[] { 0 };
                var gHat = v.Gravity.Normalize();
                return new double[]
                {
                    (double)(v.Gyro - gHat * (v.Gyro.Dot(gHat))).Length()
                };
            }));
            DefineExtractor("AngleBetweenGravAndGyroAxis", new FeatureExtractor<DatapointKitchenSink, double>(v => new double[] { v.Gravity.AngleTo(v.Gyro) }));
        }
    }

    public class FeatureClusterExtractor : FeatureExtractor<DatapointKitchenSink> 
    {
        public FeatureExtractor<DatapointKitchenSink>[] Extractors;
        private string _name;
        public override string Name
        {
            get { return _name; }
            set { throw new InvalidOperationException("Cannot assign a new name to a FeatureClusterExtractor!"); }
        }

        public FeatureClusterExtractor(params FeatureExtractor<DatapointKitchenSink>[] extractors)
        {
            Extractors = extractors;
            _name = Extractors.Select(e => e.Name).Join("&", "{}");
        }

        public FeatureClusterExtractor(params string[] extractor_names)
        {
            // Unpack any nested extractors which might be present in the list.
            var extractorNames = new HashSet<string>(extractor_names);
            while (extractorNames.Any(exN => exN.Contains("&")))
            {
                var multiExtractor = extractorNames.First(exN => exN.Contains("&"));
                extractorNames.Remove(multiExtractor);
                multiExtractor = multiExtractor.Trim('{', '}');
                extractorNames.UnionWith(multiExtractor.Split('&'));
            }

            Extractors = extractorNames.Select(n => new FeatureListExtractor(n)).ToArray();
            _name = Extractors.Select(e => e.Name).Join("&", "{}");
        }

        public override double[] Crosslink(double[] sigmas)
        {
            throw new NotImplementedException();
        }

        public override double[] Extract(DatapointKitchenSink rawInput)
        {
            throw new NotImplementedException();
        }

        public override double[][] ExtractSeq(IList<DatapointKitchenSink> rawInputs, PreprocessorCoefficients? coefficients = null)
        {
            throw new NotImplementedException();
        }

        public override PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<double[][]> sampleSet)
        {
            throw new NotImplementedException();
        }

        public override PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<DatapointKitchenSink>> source)
        {
            throw new NotImplementedException();
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

    public class FeatureAbsoluteAxisSelector<T> : FeatureExtractor<T> where T : struct
    {
        public int SelectedAxis;
        public static string[] axisNames = new string[] { "LaccX", "LaccY", "LaccZ", "GravX", "GravY", "GravZ", "GyroX", "GyroY", "GyroZ", "QuatX", "QuatY", "QuatZ", "QuatW", "Intrv" };

        public FeatureAbsoluteAxisSelector(int selectedAxis) : base(1, v => new double[] { Datapoint.AsDblArrayFrom(v)[selectedAxis] })
        {
            Name = axisNames[selectedAxis];
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