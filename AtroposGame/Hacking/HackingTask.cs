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

namespace Atropos.Hacking
{
    public enum HTaskState
    {
        Foreground,
        Background,
        SelectableInactive,
        Inactive
    }

    public class HackingTaskGraphic
    {
        public RelativeLayout GraphicsPane;
        public ImageView ImageView;
        public int ImageResourceID;
        public Action<HackingTask> establishImageView;
        public void SetImageResource(Context context, HackingTask htask)
        {
            var NameView = new TextView(context) { Text = htask.Name };
            ImageView = new ImageView(context);
            GraphicsPane?.AddView(ImageView);
            if (ImageResourceID > 0) ImageView?.SetImageResource(ImageResourceID);
            establishImageView?.Invoke(htask);
        }
        public Action<HackingTaskGraphic, object> doOnUpdate;
        public void Update(object data) { doOnUpdate?.Invoke(this, data); }
    }

    public class HackingGesture
    {
        public string GestureName;
        public int IconID;
        public Action<HackingTask> doOnGesture;
    }

    public class HackingBar
    {
        public LinearLayout Bar;
        public double BottomPercentage;
        public double TopPercentage;
        public double RemainingPercentage { get { return 1.0 - BottomPercentage - TopPercentage; } }
        public Android.Graphics.Color Colour;
    }

    public class HackingTask
    {
        public string Name;

        public HTaskState State;
        public View BackgroundView;
        public HackingTaskGraphic Graphic;
        public HackingGesture SelectionGesture;
        public HackingGesture GestureA;
        public HackingGesture GestureB;
        public HackingBar SuccessBar;
        public HackingBar RiskBar;
        public double VerticalWeightShare;

        public HackingTask(string name, string selectionGestureName)
        {
            Name = name;
            Graphic = new HackingTaskGraphic() { ImageResourceID = Resource.Drawable.atropos_sigil };
            SelectionGesture = new HackingGesture() { GestureName = selectionGestureName, IconID = Resource.Drawable.atropos_sigil };
            VerticalWeightShare = 1.0;
        }

        public static bool operator==(HackingTask first, HackingTask second) { return first.Name == second.Name; }
        public static bool operator!=(HackingTask first, HackingTask second) { return first.Name != second.Name; }
    }
}