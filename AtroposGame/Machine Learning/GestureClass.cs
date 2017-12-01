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

        //// Less obviously, some averaging processes to keep track of the typical features of the gesture - total dimensions, total time,
        //// eventually other stuff like energy spectra etc. if necessary.
        //private AdvancedRollingAverage<float> _avgWidth, _avgHeight, _avgDuration;
        //public double AverageWidth { get { return _avgWidth.Average; } }
        //public double AverageHeight { get { return _avgHeight.Average; } }
        //public TimeSpan AverageDuration { get { return TimeSpan.FromMilliseconds(_avgDuration?.Average ?? 0); } }
        //public double WidthSigma { get { return _avgWidth.Sigma; } }
        //public double HeightSigma { get { return _avgHeight.Sigma; } }
        //public double DurationSigma { get { return _avgDuration.Sigma; } }
        public void UpdateAverages(double width, double height, TimeSpan duration)
        {
            //if (_avgWidth == null)
            //{
            //    _avgWidth = AdvancedRollingAverage<float>.Create<float>(10, (float)width);
            //    _avgHeight = AdvancedRollingAverage<float>.Create<float>(10, (float)height);
            //    _avgDuration = AdvancedRollingAverage<float>.Create<float>(10, (float)duration.TotalMilliseconds);
            //}
            //else
            //{
            //    _avgWidth.Update((float)width);
            //    _avgHeight.Update((float)height);
            //    _avgDuration.Update((float)duration.TotalMilliseconds);
            //}
        }

        // Static members (defaults etc).
        [NonSerialized]
        public static Bitmap NullVisualization = Bitmap.CreateBitmap(64, 64, Bitmap.Config.Argb8888);
        public static GestureClass NullGesture = new GestureClass() { className = null, index = -10, visualization = NullVisualization };
    }
}
