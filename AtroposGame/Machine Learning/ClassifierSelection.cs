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
using Android.Util;
using Log = Android.Util.Log;

using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Vector3 = System.Numerics.Vector3;

using Atropos.DataStructures;
using DKS = Atropos.DataStructures.DatapointSpecialVariants.DatapointKitchenSink;
using Atropos.Machine_Learning.Button_Logic;
using Accord.Math;
using static Atropos.DataStructures.DatapointSpecialVariants;
using static Atropos.Machine_Learning.FeatureListExtractor;
using static System.Math;

namespace Atropos.Machine_Learning
{
    [Serializable]
    public struct ClassifierMetrics : IComparable<ClassifierMetrics>
    {
        public static double Timebase = 25;

        public double TimeToCreate;
        public double CrossEntropyLoss;
        public double WeightedAccuracy;
        public double TimePerDatapoint;

        public double OverallScore(double TimeAllowance = 25)
        {
            //if (double.IsNaN(CrossEntropyLoss)) return (Exp(Pow(WeightedAccuracy / 100.0 + 0.1, 3)) - 1) * Exp(-Pow(TimePerDatapoint / Timebase, 2));
            //return (1.0 / CrossEntropyLoss) * Exp(-Pow(TimePerDatapoint / Timebase, 2));
            return WeightedAccuracy + 2 - Exp(Pow(TimePerDatapoint / TimeAllowance, 3)) - Exp(CrossEntropyLoss/10); // Both penalties are on the order of 1% for good ones, increasing to single digits for reasonable second-place stuff, then to double digits for "really shouldn't be in contention".
        }

        public int CompareTo(ClassifierMetrics other)
        {
            return (this > other) ? +1 : -1;
        }

        public static bool operator >(ClassifierMetrics first, ClassifierMetrics second)
        {
            //if (double.IsNaN(first.CrossEntropyLoss) || double.IsNaN(second.CrossEntropyLoss))
            //    return first.WeightedAccuracy > second.WeightedAccuracy;
            //else
                return first.OverallScore() > second.OverallScore();
        }
        public static bool operator <(ClassifierMetrics first, ClassifierMetrics second)
        {
            return !(first > second);
        }
    }

    public static class ClassifierSelection
    {
        public static async Task<Classifier> FindBestClassifier(DataSet<DKS> Dataset, GestureClass currentGC = null)
        {
            var sw = new System.Diagnostics.Stopwatch();
            
            Func<Classifier, DataSet<DKS>, Task<ClassifierMetrics>> assessmentFunc;
            DataSet<DKS> d;
            string gestureName;

            if (currentGC != null)
            {
                // First, create a special Dataset which contains only two GC's... the one we're looking at, and "other".
                d = new DataSet<DKS>();
                d.AddClass(currentGC.className);
                d.AddClass("Other");
                foreach (var seq in Dataset.Samples)
                {
                    var s = new Sequence<DKS>() { SourcePath = seq.SourcePath };
                    if (seq.TrueClassIndex == currentGC.index) s.TrueClassIndex = 0;
                    else s.TrueClassIndex = 1;
                    d.AddSequence(s, skipBitmap: true);
                }
                assessmentFunc = assessClassifier_singleGC;
                gestureName = currentGC.className;
            }
            else
            {
                d = Dataset;
                assessmentFunc = assessClassifier_multiGC;
                gestureName = "full dataset";
            }

            Classifier c = null, bestC = null;
            ClassifierMetrics bestOverallMetric = new ClassifierMetrics() { CrossEntropyLoss = 1000, WeightedAccuracy = -1 }; // Ridiculously worse than any conceivable real classifier's metrics.

            var fExNames = Enumerable.Range(0, 14).Select(n => PULL + n.ToString());
            var fExList = fExNames.Select<string, FeatureExtractor<DKS>>(name => new FeatureListExtractor(name)).ToHashSet();
            fExList.UnionWith(FeatureListExtractor.AllExtractors.Values);
            var fExTestedList = new List<FeatureExtractor<DKS>>();
            bool addedDerivedExtractors = false, addedPullExtractors = false;

            while (fExList.Count > 0)
            {
                // Pop the first entry in the extractor list - using While & Pop lets us add new extractors /inside/ the loop.
                var extractor = fExList.First();
                fExList.Remove(extractor);
                fExTestedList.Add(extractor);

                // Create a fresh classifier to build based on this extractor
                c = (extractor is FeatureClusterExtractor) ? new ClusterClassifier() : new Classifier();

                sw.Start();
                var loss = c.CreateMachine(d, extractor);
                sw.Stop();
                var createTime = sw.Elapsed.TotalMilliseconds;
                sw.Reset();

                extractor.metrics = await assessmentFunc(c, d);
                extractor.metrics.CrossEntropyLoss = loss;
                extractor.metrics.TimeToCreate = createTime;
                //Log.Debug("MachineLearning|SpecialTest", $"Created special classifier for {currentGC.className} with {extractor.Name} in " +
                //    $"{createTime:f1}ms; loss {loss:f3} / score {extractor.metrics.OverallScore():f2}. Assessed in {extractor.metrics.TimePerDatapoint:f1} ms/pt, with {extractor.metrics.WeightedAccuracy:f1}% accuracy.");

                if (extractor.metrics > bestOverallMetric)
                {
                    bestOverallMetric = extractor.metrics;
                    bestC = c;
                    if (extractor.metrics.OverallScore() > 80) // Worse than 80% accuracy => don't bother reporting it to me.
                        Log.Debug("MachineLearning|SpecialTest", $"New best classifier for {gestureName}: {extractor.Name} in " +
                        $"{createTime:f1}ms; loss {loss:f3} / accuracy score {extractor.metrics.OverallScore():f2}%. Assessed in " +
                        $"{extractor.metrics.TimePerDatapoint:f1} ms/pt, with {extractor.metrics.WeightedAccuracy:f1}% raw accuracy.");
                }

                if (fExList.Count == 0 && !addedPullExtractors)
                {
                    addedPullExtractors = true;
                    var bestExtractors1 = fExTestedList
                                            .Where(extr => extr.Dimensions > 1)
                                            .Where(extr => !extr.Name.IsOneOf("LinAccelVec", "GravityVec", "GyroVec", "RotQuat")) // 'Cause their 'pull' axes are already part of the pull-all-fourteen basic set.
                                            .OrderByDescending(extr => extr.metrics)
                                            .Take(3);
                    foreach (var bestEx in bestExtractors1)
                    {
                        fExList.Add(new FeatureListExtractor(PULLX + bestEx.Name));
                        fExList.Add(new FeatureListExtractor(PULLY + bestEx.Name));
                        fExList.Add(new FeatureListExtractor(PULLZ + bestEx.Name));
                        if (bestEx.Dimensions == 4) fExList.Add(new FeatureAxisSelector<DKS>(3, bestEx) { Name = "PULL_W_" + bestEx.Name });
                    }
                }

                if (fExList.Count == 0 && !addedDerivedExtractors)
                {
                    addedDerivedExtractors = true;
                    var bestExtractors2 = fExTestedList.OrderByDescending(extr => extr.metrics).Take(3).ToList();
                    //Log.Debug("MachineLearning|SpecialTest", $"\nFirst place goes to {bestExtractors[0].Name} with {bestExtractors[0].ExtractorScore:f2}, " +
                    //    $"second to {bestExtractors[1].Name} with {bestExtractors[1].ExtractorScore:f2}, and third to {bestExtractors[2].Name} with " +
                    //    $"{bestExtractors[2].ExtractorScore:f2}.\n ");
                    var be0 = bestExtractors2[0];
                    var be1 = bestExtractors2[1];
                    var be2 = bestExtractors2[2];
                    if (be0.Dimensions + be1.Dimensions < 5) fExList.Add(new FeatureListExtractor(be0.Name, be1.Name));
                    if (be0.Dimensions + be2.Dimensions < 5) fExList.Add(new FeatureListExtractor(be0.Name, be2.Name));
                    if (be1.Dimensions + be2.Dimensions < 5) fExList.Add(new FeatureListExtractor(be1.Name, be2.Name));
                    //if (be0.Dimensions + be1.Dimensions + be2.Dimensions < 6)
                        fExList.Add(new FeatureListExtractor(be0.Name, be1.Name, be2.Name));
                    //fExList.Add(new FeatureListExtractor(INTEGRATE + bestExtractors[0].Name));
                    fExList.Add(new FeatureClusterExtractor(be0.Name, be1.Name));
                    fExList.Add(new FeatureClusterExtractor(be0.Name, be2.Name));
                    fExList.Add(new FeatureClusterExtractor(be1.Name, be2.Name));
                    fExList.Add(new FeatureClusterExtractor(be0.Name, be1.Name, be2.Name));
                }
            }

            //var lastExtractors = fExTestedList.Skip(fExTestedList.Count - 5).ToList();
            //Log.Debug("MachineLearning|SpecialTest", $"\nDerived: {lastExtractors[0].Name} with {lastExtractors[0].ExtractorScore:f2}, " +
            //    $"{lastExtractors[1].Name} with {lastExtractors[1].ExtractorScore:f2}, {lastExtractors[2].Name} with " +
            //    $"{lastExtractors[2].ExtractorScore:f2}, {lastExtractors[3].Name} with {lastExtractors[3].ExtractorScore:f2}, and " +
            //    $" {lastExtractors[4].Name} with {lastExtractors[4].ExtractorScore:f2}.\n ");

            var bestExtractors = fExTestedList.OrderByDescending(extr => extr.metrics).Take(3).ToList();
            Log.Debug("MachineLearning|SpecialTest", $"For {gestureName}:");
            var labelStrings = new string[] { "First place", "Second place", "Third place" };
            for (int i = 0; i < 3; i++)
            {
                var bestExtr = bestExtractors[i];
                Log.Debug("MachineLearning|SpecialTest", $"  {labelStrings[i]} goes to {bestExtr.Name}, with a score of {bestExtr.metrics.OverallScore():f2} " +
                    $"(accuracy {(bestExtr.metrics.WeightedAccuracy):f1}%, loss {bestExtr.metrics.CrossEntropyLoss:f2}, time {bestExtr.metrics.TimePerDatapoint:f1}).");
            }

            return bestC;
        }

        public static async Task<ClassifierMetrics> assessClassifier_singleGC(Classifier c, DataSet<DKS> d)
        {
            var sw = new System.Diagnostics.Stopwatch();
            var metrics = new ClassifierMetrics() { CrossEntropyLoss = double.NaN, TimeToCreate = -1 }; // Those two details will need to be filled in separately.

            sw.Start();
            await c.FastAssess(d);
            sw.Stop();
            int numCorrectPositives = 0, numFalseNegatives = 0, numCorrectNegatives = 0, numFalsePositives = 0;
            double scoCorrectPositives = 0, scoFalseNegatives = 0, scoCorrectNegatives = 0, scoFalsePositives = 0;
            foreach (var seq in d.Samples)
            {
                if (seq.TrueClassIndex == 0)
                {
                    if (seq.RecognizedAsIndex == 0)
                    {
                        numCorrectPositives++;
                        scoCorrectPositives += (seq.RecognitionScore - scoCorrectPositives) / numCorrectPositives;
                    }
                    else
                    {
                        numFalseNegatives++;
                        scoFalseNegatives += (seq.RecognitionScore - scoFalseNegatives) / numFalseNegatives;
                    }
                }
                else
                {
                    if (seq.RecognizedAsIndex == 1)
                    {
                        numCorrectNegatives++;
                        scoCorrectNegatives += (seq.RecognitionScore - scoCorrectNegatives) / numCorrectNegatives;
                    }
                    else
                    {
                        numFalsePositives++;
                        scoFalsePositives += (seq.RecognitionScore - scoFalsePositives) / numFalsePositives;
                    }
                }
            }
            var elapsed = sw.Elapsed.TotalMilliseconds / d.Samples.Count;

            metrics.TimePerDatapoint = elapsed;
            metrics.WeightedAccuracy = 100.0 * (2.0 * numCorrectPositives / (numCorrectPositives + numFalseNegatives) + numCorrectNegatives / (numCorrectNegatives + numFalsePositives)) / 3.0;

            sw.Reset();
            return metrics;
        }

        public static async Task<ClassifierMetrics> assessClassifier_multiGC(Classifier c, DataSet<DKS> d)
        {
            var sw = new System.Diagnostics.Stopwatch();
            var metrics = new ClassifierMetrics() { CrossEntropyLoss = double.NaN, TimeToCreate = -1 }; // Those two details will need to be filled in separately.

            sw.Start();
            await c.FastAssess(d);
            sw.Stop();
            int numCorrect = 0, numIncorrect = 0;
            double scoCorrect = 0, scoIncorrect = 0;
            foreach (var seq in d.Samples)
            {
                if (seq.TrueClassIndex == seq.RecognizedAsIndex)
                {
                    numCorrect++;
                    scoCorrect += (seq.RecognitionScore - scoCorrect) / numCorrect;
                }
                else
                {
                    numIncorrect++;
                    scoIncorrect += (seq.RecognitionScore - scoIncorrect) / numIncorrect;
                }
            }
            var elapsed = sw.Elapsed.TotalMilliseconds / d.Samples.Count;
            //Log.Debug("MachineLearning|SpecialTest", $"Created special classifier for {currentGC.className} with {extractor.Name} in " +
            //    $"{createTime:f1}ms; loss {loss:f3}. Assessed in {elapsed:f1} ms/pt, with " +
            //    $"{numCorrectPositives} ({scoCorrectPositives:f2}) correct positives, " +
            //    $"{numCorrectNegatives} ({scoCorrectNegatives:f2}) correct negatives, " +
            //    $"{numFalseNegatives} ({scoFalseNegatives:f2}) false negatives, and " +
            //    $"{numFalsePositives} ({scoFalsePositives:f2}) false positives.");

            metrics.TimePerDatapoint = elapsed;
            metrics.WeightedAccuracy = 100.0 * (numCorrect / (numCorrect + numIncorrect));

            sw.Reset();
            return metrics;
        }
    }
}