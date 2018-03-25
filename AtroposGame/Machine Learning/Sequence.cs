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
using Android.Graphics;

using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
//using Accord.Math;
using Accord.Statistics.Kernels;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Numerics;
using static Accord.Math.Vector;
using System.Runtime.Serialization;
using System.Security.Permissions;
using MiscUtil;
using Atropos.DataStructures;

namespace Atropos.Machine_Learning
{
    public interface ISequence
    {
        //BindingList<String> ClassNames { get; set; }
        int TrueClassIndex { get; set; }
        int RecognizedAsIndex { get; set; }
        string TrueClassName { get; }
        string RecognizedAsName { get; }
        double[] MachineInputs { get; }
        Bitmap Bitmap { get; }
        bool HasContributedToClassifier { get; set; }
        //bool HasBeenTallied { get; set; }
        bool HasBeenSampled { get; set; }
        double RecognitionScore { get; }
        SequenceMetadata Metadata { get; set; }
    }

    [Serializable]
    public struct PreprocessorCoefficients
    {
        public double[] Means;
        public double[] Sigmas;
    }

    [Serializable]
    public class Sequence<T> : ISequence, ICloneable, ISerializable
        where T : struct
    {
        [XmlIgnore]
        [NonSerialized]
        private double[] machineInputs;

        [XmlIgnore]
        [NonSerialized]
        private Bitmap bitmap;

        public string TypeName;
        //public BindingList<String> ClassNames { get; set; }

        //public Datapoint<T>[] SourcePath { get; set; }
        public T[] SourcePath { get; set; }
        //public IDatapoint[] SourcePath { get; set; }

        private int _trueClassIndex = -1, _recognizedAsIndex = -1;
        public int TrueClassIndex { get { return _trueClassIndex; } set { _trueClassIndex = value; } }
        public int RecognizedAsIndex { get { return _recognizedAsIndex; } set { _recognizedAsIndex = value; } }

        public bool HasContributedToClassifier { get; set; } = false;
        public bool HasBeenSampled { get; set; } = false;
        //bool HasBeenTallied { get; set; } = false;

        public double RecognitionScore { get { return Metadata.QualityScore; } }
        public SequenceMetadata Metadata { get; set; }

        public Sequence()
        {
            TypeName = typeof(T).Name;
        }

        protected Sequence(SerializationInfo info, StreamingContext context) : this()
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            var sourceArray = (float[][])info.GetValue("SourcePath_Serializable", typeof(float[][]));
            if (typeof(T).Implements<IDatapoint>()) // If we're using something like Sequence<Datapoint<Vector3,Quaternion>>...
            {
                SourcePath = sourceArray
                            //.Select(array => (T)(default(T) as IDatapoint).FromArray(array))
                            .Select(array => Operator.Convert<IDatapoint, T>((default(T) as IDatapoint).FromArray(array)))
                            //.Cast<T>()
                            .ToArray();
            }
            else // If we're using something simple like Sequence<Vector3>
            {
                SourcePath = sourceArray
                            .Select(array => (T)(default(Datapoint<T>).FromArray(array)))
                            .ToArray(); 
            }
            _trueClassIndex = (int)info.GetValue("_trueClassIndex", typeof(int));
            _recognizedAsIndex = (int)info.GetValue("_recognizedAsIndex", typeof(int));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new System.ArgumentNullException("info");
            info.AddValue("SourcePath_Serializable", SourcePath.Select((f) => ((Datapoint<T>)f).AsArray()).ToArray());
            info.AddValue("_trueClassIndex", _trueClassIndex);
            info.AddValue("_recognizedAsIndex", _recognizedAsIndex);
        }

        public string TrueClassName
        {
            //get { return TrueClassIndex >= 0 ? ClassNames[TrueClassIndex] : "-"; }
            get { return TrueClassIndex >= 0 ? DataSet.Current.ActualGestureClasses.ElementAtOrDefault(TrueClassIndex).className : "-"; }
        }

        public string RecognizedAsName
        {
            //get { return RecognizedAsIndex >= 0 ? ClassNames[RecognizedAsIndex] : "-"; }
            get { return RecognizedAsIndex >= 0 ? DataSet.Current.ActualGestureClasses.ElementAtOrDefault(RecognizedAsIndex).className : "-"; }
        }

        [NonSerialized]
        public Func<IList<T>, double[][]> PreprocessorFunction = null;
        public void ResetMachineInputs() { machineInputs = null; }

        public double[] MachineInputs
        {
            get
            {
                if (machineInputs == null)
                    if (PreprocessorFunction == null)
                        machineInputs = Accord.Math.Matrix.Merge(Preprocess(SourcePath), Datapoint<T>.Dimensions);
                    else machineInputs = Accord.Math.Matrix.Merge(PreprocessorFunction(SourcePath), Datapoint<T>.Dimensions);
                return machineInputs;
            }
        }


        public Bitmap Bitmap
        {
            get
            {
                if (bitmap == null && SourcePath != null)
                {
                    //if (Datapoint<T>.Dimensions < 3)
                    //    bitmap = ToBitmap<Vector2>(SourcePath);
                    //else if (Datapoint<T>.Dimensions == 3) bitmap = ToBitmap<Vector3>(SourcePath);
                    bitmap = ToBitmap(SourcePath);
                }

                return bitmap;
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        //public static double[][] Preprocess(T[] sourcePathRaw)
        //{
        //    return Preprocess(Array.ConvertAll(sourcePathRaw, t => (IDatapoint)(Datapoint<T>)t));
        //}

        // Take the feature vector and scale it so that it's expressed in terms of relative, instead of absolute, distances/accels/etc.
        internal static double[][] Preprocess(IList<T> sequence)
        {
            return Preprocess(sequence.ToArray());
        }
        internal static double[][] Preprocess(T[] sequence)
        {
            if (sequence.Length == 0) return new double[0][];
            int dims = Datapoint<T>.Dimensions;

            double[][] result = new double[sequence.Length][];
            for (int i = 0; i < sequence.Length; i++)
                //result[i] = new double[] { sequence[i].X, sequence[i].Y };
                result[i] = ((IDatapoint)sequence[i])
                    .AsArray()
                    //.Cast<double>()
                    .Select(f => (double)f)
                    .ToArray();

            double[] currentAxisValues = new double[sequence.Length]; // Just to save on allocations a tiny bit by reusing it within a single axis.
            double[] means = new double[dims];
            double[] sigmas = new double[dims];

            for (int j = 0; j < dims; j++)
            {
                for (int i = 0; i < sequence.Length; i++)
                {
                    currentAxisValues[i] = result[i][j];
                }
                means[j] = currentAxisValues.Average();
                sigmas[j] = Accord.Statistics.Measures.StandardDeviation(currentAxisValues);
            }
            sigmas = CrosslinkAxes.Apply<T>(sigmas); // Datapoint<T>.CrosslinkScalingFactors(sigmas); // Locks values in the same space - like X and Y axes - to the same window size.

            double[][] zscores = Accord.Statistics.Tools.ZScores(result, means, sigmas);

            return Accord.Math.Elementwise.Add(zscores, 10);
        }

        /// <summary>
        /// Generates the parameters for a preprocessor function which will turn absolute values of the supplied sequence set, into sigmas of same, based on the overall mean and standard deviation within the set.
        /// </summary>
        /// <param name="sampleSet">The sample set to examine.  Turns absolute values of parameters into z-scores (based on the set's mean X, and the set's maximum window size (sigma) in X/Y/Z, for every axis within every vector in the datapoint type).</param>
        /// <param name="crosslinkFunc">A function to correlate sigmas in sets of features which should share a common window size.  Leave blank to use the normal "max sigma in any one axis" funtion, or use <see cref="CrosslinkAxes.None"/> for no crosslinking at all.</param>
        /// <returns></returns>
        internal static PreprocessorCoefficients GetPreprocessorCoefficients(IEnumerable<Sequence<T>> sampleSet, Func<double[], double[]> crosslinkFunc = null)
        {
            int dims = Datapoint<T>.Dimensions;
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
                    allValues.Add(Datapoint.From<T>(pt).AsArray().Select(f => (double)f).ToArray());
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

            crosslinkFunc = crosslinkFunc ?? ((s) => CrosslinkAxes.Apply<T>(s));
            sigmas = crosslinkFunc(sigmas); // Locks values in the same space - like X and Y axes - to the same window size.

            return new PreprocessorCoefficients() { Means = means, Sigmas = sigmas };
        }

        public static Func<IList<T>, double[][]> CreatePreprocessorFunction(PreprocessorCoefficients coefficients)
        {
            var means = coefficients.Means;
            var sigmas = coefficients.Sigmas;

            return (sequence) =>
            {
                if (sequence.Count == 0) return new double[0][];

                double[][] result = new double[sequence.Count][];
                for (int i = 0; i < sequence.Count; i++)
                    //result[i] = new double[] { sequence[i].X, sequence[i].Y };
                    result[i] = ((IDatapoint)sequence[i])
                        .AsArray()
                        //.Cast<double>()
                        .Select(f => (double)f)
                        .ToArray();

                double[][] zscores = Accord.Statistics.Tools.ZScores(result, means, sigmas);

                return Accord.Math.Elementwise.Add(zscores, 10);
            };
        }

        private const int bmpSize = 128;
        //internal static Bitmap ToBitmap<Tgraphical>(Datapoint<T>[] sequence) where Tgraphical : struct
        internal static Bitmap ToBitmap(T[] seq)
        {
            if (seq.Length == 0)
                return null;
            var sequence = seq
                            .Select(s => Datapoint.From(s))
                            .ToArray();

            //List<Tgraphical> Displacements = new List<Tgraphical>(); // Tgraphical is a vector with one dimension per axis we wish to plot.
            //List<float> DistancesTraveled = new List<float>();

            //if (Datapoint<T>.Dimensions <= 2)
            //{
            //    Vector2 V = new Vector2(0, 0);
            //    Vector2 D = new Vector2(0, 0);
            //    // Note - this uses the special convert-to-Vector2 overload of Datapoint<T>.
            //    foreach (Vector2 pt in sequence)
            //    {
            //        //V.X += pt.X;
            //        //V.Y += pt.Y;
            //        //D.X += V.X;
            //        //D.Y += V.Y;
            //        V += pt;
            //        D += V;
            //        Displacements.Add(Operator.Convert<Vector2, Tgraphical>(D));
            //        DistancesTraveled.Add(D.Length());
            //    }
            //}
            //else if (Datapoint<T>.Dimensions >= 3)
            //{
            //    Vector3 V = new Vector3(0, 0, 0);
            //    Vector3 D = new Vector3(0, 0, 0);
            //    // Note - this uses the special convert-to-Vector3 overload of Datapoint<T>.
            //    foreach (Vector3 pt in sequence)
            //    {
            //        V += pt;
            //        D += V;
            //        Displacements.Add(Operator.Convert<Vector3, Tgraphical>(D));
            //        DistancesTraveled.Add(D.Length());
            //    }
            //}

            //// Trim off the first and last 10% *by distance traveled*
            //var TotalDistance = DistancesTraveled.Last();
            //Displacements = Displacements.Where((v, i) => DistancesTraveled[i] > TotalDistance * 0.1 && DistancesTraveled[i] < TotalDistance * 0.9).ToList();

            //int xmax = (int)Displacements.Max(x => x.X);
            //int xmin = (int)Displacements.Min(x => x.X);

            //int ymax = (int)Displacements.Max(x => x.Y);
            //int ymin = (int)Displacements.Min(x => x.Y);

            //int width = xmax - xmin;
            //int height = ymax - ymin;

            //float xmax = sequence.Max(x => x.X);
            //float xmin = sequence.Min(x => x.X);
            //float ymax = sequence.Max(x => x.Y);
            //float ymin = sequence.Min(x => x.Y);
            //// Tweak so that they share the same x-axis and scaling
            //xmax = ymax = Math.Max(xmax, ymax);
            //xmin = ymin = Math.Min(xmin, ymin);
            
            var DisplayedDimensions = Enumerable.Range(0, Datapoint<T>.Dimensions);
            float maxCoord = float.NegativeInfinity;
            float minCoord = float.PositiveInfinity;
            foreach (int i in DisplayedDimensions)
            {
                minCoord = Math.Min(minCoord, sequence.Min(v => v.AsArray()[i]));
                maxCoord = Math.Max(maxCoord, sequence.Max(v => v.AsArray()[i]));
            }

            //float width = xmax - xmin;
            //float height = ymax - ymin;

            var bmp = Bitmap.CreateBitmap(bmpSize, bmpSize, Bitmap.Config.Argb8888);
            Canvas canvas = new Canvas(bmp);
            Paint paintX = new Paint();
            paintX.StrokeCap = Paint.Cap.Round;
            Paint paintY = new Paint();
            paintY.StrokeCap = Paint.Cap.Round;

            List<Paint> paints = new List<Paint>();
            #region Paint spectrum functions (mostly just makes things pretty)
            List<Action<int, Paint>> getColorFuncs = new List<Action<int, Paint>> // Colour spectra which can easily be told apart onscreen
            {
                (p, paint) => paint.SetARGB(255, 255 - p/3, 0, p/3),
                (p, paint) => paint.SetARGB(255, p/3, 255 - p/3, p/3),
                (p, paint) => paint.SetARGB(255, 255 - p/3, p/3, 0),
                (p, paint) => paint.SetARGB(255, p/3, p/3, 255 - p/3),
                (p, paint) => paint.SetARGB(255, p/3, 255 - p/3, 177 - p/6),
                (p, paint) => paint.SetARGB(255, 255 - p/3, 177 - p/6, 50 + p/2)
            };
            // If even that isn't enough, create new ones until we have enough such paint functions.
            //while (Datapoint<Tgraphical>.Dimensions > getColorFuncs.Count)
            while (Datapoint<T>.Dimensions > getColorFuncs.Count)
            {
                List<Func<int, int>> converters = new List<Func<int, int>>()
                    {
                        p => 0,
                        p => 128,
                        p => 255,
                        p => p,
                        p => p/2,
                        p => p/3,
                        p => 255 - p,
                        p => 255 - p/2,
                        p => 255 - p/3,
                        p => 50 + p/2,
                        p => 200 - p/2,
                        p => 100 + p/2,
                        p => 150 - p/2,
                        p => Math.Abs(p * 2 - 128).Clamp(0, 255),
                        p => Math.Abs(128 - p * 2).Clamp(0, 255)
                    };
                var f1 = converters.GetRandom();
                var f2 = converters.GetRandom();
                var f3 = converters.GetRandom();
                getColorFuncs.Add((p, paint) => paint.SetARGB(255, f1(p), f2(p), f3(p)));
            }
            #endregion

            foreach (int i in DisplayedDimensions)
            {
                var newPaint = new Paint
                {
                    StrokeCap = Paint.Cap.Round,
                    StrokeWidth = 1.5f
                };
                paints.Add(newPaint);
            }

            Paint paintAxis = new Paint();
            paintAxis.SetARGB(127, 127, 127, 127);
            var zeroLine = 0f.Scale(minCoord, maxCoord, 0, bmpSize);
            canvas.DrawLine(0, zeroLine, bmpSize, zeroLine, paintAxis);

            int prev_x = 0.Scale(0, sequence.Length - 1, 0, bmpSize); // ??? Not sure why I'm not just saying "zero" here.
            int[] prev_ys = sequence[0].AsArray().Select(d => d.Scale(minCoord, maxCoord, 0, bmpSize)).ToArray();
            int x, i255;
            int[] ys;

            for (int i = 1; i < sequence.Length; i++)
            {
                ////int x = (int)Displacements[i].X - xmin;
                ////int y = (int)Displacements[i].Y - ymin;
                //int x = i.Scale(0, sequence.Length - 1, 0, 128);
                //int yX = sequence[i].X.Scale(xmin, xmax, 0, 128);
                //int yY = sequence[i].Y.Scale(ymin, ymax, 0, 128);
                x = i.Scale(1, sequence.Length, 0, bmpSize);
                ys = sequence[i].AsArray().Select(d => d.Scale(minCoord, maxCoord, 0, bmpSize)).ToArray();

                i255 = i.Scale(0, sequence.Length, 0, 255);

                ////int prevX = (int)Displacements[i - 1].X - xmin;
                ////int prevY = (int)Displacements[i - 1].Y - ymin;
                //int prev_X = (i-1).Scale(0, sequence.Length - 1, 0, 128);
                //int prev_yX = sequence[i-1].X.Scale(xmin, xmax, 0, 128);
                //int prev_yY = sequence[i-1].Y.Scale(ymin, ymax, 0, 128);

                //paintX.SetARGB(255, 255 - p, 0, p);
                //paintY.SetARGB(255, p/2, 255 - p, p);
                //canvas.DrawLine(prev_X, prev_yX, x, yX, paintX);
                //canvas.DrawLine(prev_X, prev_yY, x, yY, paintY);

                foreach (int j in DisplayedDimensions)
                {
                    getColorFuncs[j].Invoke(i255, paints[j]);
                    canvas.DrawLine(prev_x, prev_ys[j], x, ys[j], paints[j]);
                }

                prev_x = x;
                prev_ys = ys;
            }

            return bmp;
            // Note - use myImageView.setImageBitmap(bmp) to display.
        }
    }

    [Serializable]
    public struct SequenceMetadata
    {
        public double QualityScore;
        public TimeSpan Delay;
        public TimeSpan Duration;
        public double NumPoints;
        public double PeakAccel;

        public static double GetOverallMagnitude<T>(T datum) where T : struct
        {
            return Datapoint.From(datum).Magnitude();
        }

        public static double GetSubvectorOneMagnitude<T, T1, T2>(T datum) where T : struct where T1 : struct where T2 : struct
        {
            if (typeof(T) != typeof(Datapoint<T1, T2>)) return -1;
            var datumAs = Operator.Convert<T, Datapoint<T1, T2>>(datum);
            return Datapoint.From(datumAs.Value1).Magnitude();
        }

        public static double GetSubvectorTwoMagnitude<T, T1, T2>(T datum) where T : struct where T1 : struct where T2 : struct
        {
            if (typeof(T) != typeof(Datapoint<T1, T2>)) return -1;
            var datumAs = Operator.Convert<T, Datapoint<T1, T2>>(datum);
            return Datapoint.From(datumAs.Value2).Magnitude();
        }
    }
}