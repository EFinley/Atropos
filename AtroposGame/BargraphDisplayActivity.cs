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


using Log = Android.Util.Log;
using static System.Math;

namespace Atropos
{
    public class BargraphDisplayActivity : BaseActivity_Portrait
    {
        public class BargraphData
        {
            private int BargraphViewID;
            private LinearLayout BargraphView;
            private BargraphDisplayActivity parent;

            private TextView LabelTextView, ValueTextView;
            public string Label { get { return LabelTextView?.Text ?? "(None)"; } }

            private View LeftBar, CenterBar, RightBar;
            private LinearLayout BarContainer;
            public Color BarColour;
            
            private AdvancedRollingAverageFloat LowValues, HighValues, Values;
            public float LowValue { get { return LowValues.Average; } }
            public float Value { get { return Values.Average; } }
            public float HighValue { get { return HighValues.Average; } }
            private float FullScaleValue;

            private Color[] DefaultColours = new Color[] { Color.Green, Color.Blue, Color.Red, Color.Yellow, Color.Magenta, Color.Orange, Color.Purple, Color.LawnGreen };
            private static int defaultColourIndex = 0;
            private Color DefaultColour { get { var c = DefaultColours[defaultColourIndex]; defaultColourIndex = (defaultColourIndex + 1) % DefaultColours.Length; return c; } }

            public event EventHandler<MiscUtil.EventArgs<double>> OnValueChanged;
            private System.Timers.Timer UpdateLooper;

            public BargraphData(BargraphDisplayActivity parent, string label, Color? barColor = null, int ID = 0)
            {
                this.parent = parent;
                BarColour = barColor ?? DefaultColour;
                BargraphViewID = ID;

                UpdateLooper = new System.Timers.Timer(250);
                UpdateLooper.AutoReset = true;

                parent.RunOnUiThread(() =>
                {
                    CreateUIelement();
                    LabelTextView.Text = label;

                    parent.Datasets.Add(this);
                    parent.FindViewById<LinearLayout>(Resource.Id.bargraph_pane).AddView(BargraphView, ViewGroup.LayoutParams.MatchParent, 100);
                    BargraphView.Invalidate();
                    BargraphView.ForceLayout();
                });
                
                Log.Debug("Bargraph data", $"Created bargraph {label}, with {BargraphView.ChildCount} children.");
            }

            private void CreateUIelement()
            {
                //BargraphView = parent.FindViewById<LinearLayout>(ID);
                BargraphView = new LinearLayout(parent);
                BargraphView.Id = (BargraphViewID != 0) ? BargraphViewID : View.GenerateViewId();

                var BarPane = parent.FindViewById<LinearLayout>(Resource.Id.bargraph_pane);
                if (BarPane == null)
                {
                    Log.Error("BargraphData", $"Unable to find an <@id/bargraph_pane> element in activity - cannot display bar graphs!");
                    return;
                }
                //BarPane.AddView(BargraphView);
                
                View topLine = new View(parent);
                topLine.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 4) { BottomMargin = 5 };
                topLine.SetBackgroundColor(Android.Graphics.Color.LightGray);
                BargraphView.AddView(topLine, 0);

                LabelTextView = new TextView(parent);
                LabelTextView.Text = "?????";
                BargraphView.AddView(LabelTextView, 1, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, 100));

                BarContainer = new LinearLayout(parent);
                BarContainer.Orientation = Android.Widget.Orientation.Horizontal;
                BarContainer.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 25);
                //BargraphView.AddView(BarContainer);

                LeftBar = new View(parent);
                LeftBar.LayoutParameters = WeightedLayout(0.25);
                LeftBar.SetBackgroundColor(Darken(BarColour, 25.0));
                BarContainer.AddView(LeftBar, 0, WeightedLayout(0.25));

                CenterBar = new View(parent);
                CenterBar.LayoutParameters = WeightedLayout(0.25);
                CenterBar.SetBackgroundColor(BarColour);
                BarContainer.AddView(CenterBar, 1, WeightedLayout(0.25));

                RightBar = new View(parent);
                RightBar.LayoutParameters = WeightedLayout(0.25);
                RightBar.SetBackgroundColor(Lighten(BarColour, 25.0));
                BarContainer.AddView(RightBar, 2, WeightedLayout(0.25));

                ValueTextView = new TextView(parent);
                ValueTextView.LayoutParameters = WeightedLayout(0.25);
                ValueTextView.TextAlignment = TextAlignment.ViewEnd;
                ValueTextView.Text = "??";
                BarContainer.AddView(ValueTextView, 3, WeightedLayout(0.25));

                BargraphView.AddView(BarContainer, 2, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 25));

                View bottomLine = new View(parent);
                bottomLine.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 4) { TopMargin = 5 };
                bottomLine.SetBackgroundColor(Android.Graphics.Color.DarkGray);
                BargraphView.AddView(bottomLine, 3, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 4) { TopMargin = 5 });

                //BargraphView.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, LinearLayout.LayoutParams.WrapContent);
                BargraphView.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, 100);
            }

            public void Update(double newVal)
            {
                // Debugging
                if (Abs(newVal) < 0.01) return;

                if (Values == null)
                {
                    Values = new AdvancedRollingAverageFloat(timeFrameInPeriods: 5, initialAverage: (float)newVal);
                    LowValues = new AdvancedRollingAverageFloat(timeFrameInPeriods: 3, initialAverage: (float)(newVal * 0.98));
                    HighValues = new AdvancedRollingAverageFloat(timeFrameInPeriods: 3, initialAverage: (float)(newVal * 1.02));
                    FullScaleValue = 1.75f * (float)newVal;

                    UpdateLooper.Elapsed += UpdateLooper_Elapsed;
                    UpdateLooper.Start();
                }

                OnValueChanged?.Invoke(this, new MiscUtil.EventArgs<double>(newVal));

                Values.Update((float)newVal);
                float value = Values.Average;

                float DecayAmount = 0.05f;
                //var newLow = (float)Min(value, Min(newVal, LowValues.Average * (1.0 + DecayAmount)));
                var newLow = (float)Min(newVal, LowValues.Average + (Values.Average - LowValues.Average) * DecayAmount);
                LowValues.Update(newLow);
                //var newHigh = (float)Max(value, Max(newVal, HighValues.Average * (1.0 - DecayAmount)));
                var newHigh = (float)Max(newVal, HighValues.Average - (HighValues.Average - Values.Average) * DecayAmount);
                HighValues.Update(newHigh);

                //Log.Debug("BargraphData.Update", $"Updating {LabelTextView.Text} with {newVal:f2}, yielding {LowValues.Average:f2}/{Values.Average:f2}/{HighValues.Average:f2}");
            }

            private void UpdateLooper_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                if (BargraphView == null || Values == null) return; // Just to make sure we don't accidentally try to do stuff to the UI before we're ready to.
                //Log.Debug("UpdateLooper", $"Updating {LabelTextView.Text} with averaged value {Values.Average}");

                // Adjust the scale to reflect current value ranges
                if (HighValues.Average > 0.85 * FullScaleValue) FullScaleValue *= 1.5f;
                if (HighValues.Average < 0.4 * FullScaleValue) FullScaleValue /= 1.5f;

                var leftBarWeight = LowValues.Average / FullScaleValue;
                var leftBarFormat = WeightedLayout(leftBarWeight);

                var centerBarWeight = (Values.Average - LowValues.Average) / FullScaleValue;
                var centerBarFormat = WeightedLayout(centerBarWeight);

                var rightBarWeight = (HighValues.Average - Values.Average) / FullScaleValue;
                var rightBarFormat = WeightedLayout(rightBarWeight);

                var valueFieldWeight = 1.0 - leftBarWeight - centerBarWeight - rightBarWeight;
                var valueFieldFormat = WeightedLayout(valueFieldWeight);
                int precision = (1 - (int)Ceiling(Log10(Values.Average))).Clamp(0, 3); // So, zero decimals for 10 or above, one for 1-10, two for 0.1-1, and three for anything 0.1 or less.
                string valueFieldContents = String.Format($"f{precision}", Values.Average);

                parent.RunOnUiThread(() =>
                {
                    LeftBar.LayoutParameters = leftBarFormat;
                    CenterBar.LayoutParameters = centerBarFormat;
                    RightBar.LayoutParameters = rightBarFormat;
                    ValueTextView.LayoutParameters = valueFieldFormat;
                    ValueTextView.Text = valueFieldContents;

                    LeftBar.Invalidate();
                    CenterBar.Invalidate();
                    RightBar.Invalidate();
                    ValueTextView.Invalidate();

                    BargraphView.Invalidate();
                    // BargraphView.ForceLayout();
                    parent.FindViewById<LinearLayout>(Resource.Id.bargraph_pane).Invalidate();
                });
            }

            public void Start()
            {
                UpdateLooper.Start();
            }

            public void Stop()
            {
                UpdateLooper.Stop();
            }

            public void Dispose()
            {
                Stop();
                UpdateLooper.Dispose();
                parent.RunOnUiThread(() =>
                {
                    parent.FindViewById<LinearLayout>(Resource.Id.bargraph_pane).RemoveView(BargraphView);
                });
            }

            // Utility functions
            private LinearLayout.LayoutParams WeightedLayout(double weight, bool overrideWidth = true)
            {
                if (overrideWidth) return new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 100, (float)weight);
                else return new LinearLayout.LayoutParams(100, ViewGroup.LayoutParams.MatchParent, (float)weight);
            }

            private Color Lighten(Color inputCol, double percentage)
            {
                if (percentage > 1.0) percentage /= 100.0;
                byte[] initialValues = new byte[] { inputCol.R, inputCol.G, inputCol.B };
                byte[] finalValues = initialValues.Select<byte, byte>(b => (byte)(255 - Math.Round((255 - b) * (1.0 - percentage)))).ToArray();
                return new Color(finalValues[0], finalValues[1], finalValues[2], inputCol.A);
            }

            private Color Darken(Color inputCol, double percentage)
            {
                if (percentage > 1.0) percentage /= 100.0;
                byte[] initialValues = new byte[] { inputCol.R, inputCol.G, inputCol.B };
                byte[] finalValues = initialValues.Select<byte, byte>(b => (byte)Math.Round(b * (1.0 - percentage))).ToArray();
                return new Color(finalValues[0], finalValues[1], finalValues[2], inputCol.A);
            }
        }

        public List<BargraphData> Datasets = new List<BargraphData>();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.BarGraphFeedbackDisplay);
        }

        protected override void OnResume()
        {
            base.OnResume();
            foreach (var data in Datasets) data.Start();
        }

        protected override void OnPause()
        {
            base.OnPause();
            foreach (var data in Datasets) data.Stop();
        }
    }
}