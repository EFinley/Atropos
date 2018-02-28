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

namespace Atropos.Machine_Learning
{
    [Serializable]
    public class GestureClass
    {
        // Easy and obvious stuff to do with data entry
        public int index;
        public string className;
        [NonSerialized] public Bitmap visualization = NullVisualization;
        public int numExamples;
        public int numExamplesCorrectlyRecognized;
        public int numNewExamples;
        public int numNewExamplesCorrectlyRecognized;
        public int numExamplesSampled;
        public int numExamplesSampledCorrectlyRecognized;
        //public double percentageCorrect { get { return (numExamples > 0) ? 100 * (double)numExamplesCorrectlyRecognized / (double)numExamples : 0.0; } }

        public virtual bool IsTrainable { get { return true; } } // Characteristic of this entire class (simpler to write than testing for whether it's this class or a subclass)

        //// Less obviously, some averaging processes to keep track of the typical features of the gesture - total dimensions, total time,
        //// eventually other stuff like energy spectra etc. if necessary.
        public SequenceMetadata AverageMetadata;
        private int numMetadataPoints = 0;
        private double rollinAverage(double oldAvg, double newVal) { return oldAvg + (newVal - oldAvg) / (numMetadataPoints + 1); }
        private TimeSpan rollinAverage(TimeSpan oldAvg, TimeSpan newVal) { return TimeSpan.FromMilliseconds(oldAvg.TotalMilliseconds + ((newVal - oldAvg).TotalMilliseconds) / (numMetadataPoints + 1)); }
        public void UpdateMetadata(SequenceMetadata newData)
        {
            AverageMetadata.QualityScore = rollinAverage(AverageMetadata.QualityScore, newData.QualityScore);
            AverageMetadata.Delay = rollinAverage(AverageMetadata.Delay, newData.Delay);
            AverageMetadata.Duration = rollinAverage(AverageMetadata.Duration, newData.Duration);
            AverageMetadata.NumPoints = rollinAverage(AverageMetadata.NumPoints, newData.NumPoints);
            AverageMetadata.PeakAccel = rollinAverage(AverageMetadata.PeakAccel, newData.PeakAccel);
            numMetadataPoints++;
            Android.Util.Log.Debug("MachineLearning|GC", $"Updating average metadata for {className} to Qual {AverageMetadata.QualityScore}, Delay {AverageMetadata.Delay.TotalMilliseconds}, Duration {AverageMetadata.Duration.TotalMilliseconds}, #pts {AverageMetadata.NumPoints}, and peak accel {AverageMetadata.PeakAccel}.");
        }
        public void ResetMetadata()
        {
            AverageMetadata = new SequenceMetadata();
            numMetadataPoints = 0;
        }

        public virtual string PercentageText
        {
            get
            {
                if (numExamplesSampled == numExamples) // 100% sampling - display actual counts right/wrong
                {
                    if (numExamples > 0) return $"{(100.0 * numExamplesCorrectlyRecognized / numExamples):f0}%";
                    else return "0%";
                }
                else
                {
                    if (numExamplesSampled > 0) return $"{(100.0 * numExamplesSampledCorrectlyRecognized / numExamplesSampled):f0}%";
                    else return "0%";
                }
            }
        }
        public virtual string DetailsText
        {
            get
            {
                if (numExamplesSampled == numExamples) // 100% sampling - display actual counts right/wrong
                {
                    return $"({numExamplesCorrectlyRecognized} / {numExamples})";
                }
                else
                {
                    return $"({numExamples} @ {(100.0 * numExamplesSampled / numExamples):f0}%)";
                }
            }
        }
        public virtual string AddedItemsText
        {
            get
            {
                return $"+ {numNewExamplesCorrectlyRecognized} / {numNewExamples}";
            }
        }


        // Static members (defaults etc).
        [NonSerialized]
        public static Bitmap NullVisualization = Bitmap.CreateBitmap(64, 64, Bitmap.Config.Argb8888);
        public static GestureClass NullGesture = new GestureClass() { className = null, index = -10, visualization = NullVisualization };
    }

    [Serializable]
    public class ColourGestureClass : GestureClass
    {
        public override bool IsTrainable { get { return false; } } // Characteristic of this entire class (simpler to write than testing for whether it's this class or the base class)

        public override string PercentageText { get { return String.Empty; } }
        public override string DetailsText { get { return "(Used for colour only)"; } }
        public override string AddedItemsText { get { return String.Empty; } }
    }
}
