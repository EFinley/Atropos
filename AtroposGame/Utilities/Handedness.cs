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

namespace Atropos
{
    public class Handedness
    {
        protected bool InvertX;
        protected bool InvertY;
        protected bool InvertZ;

        public int CoeffX { get => (InvertX) ? -1 : 1; }
        public int CoeffY { get => (InvertY) ? -1 : 1; }
        public int CoeffZ { get => (InvertZ) ? -1 : 1; }

        public static Handedness Current { get; set; } = Default;
        public static Handedness Default = new Handedness { InvertX = false, InvertY = false, InvertZ = false };
        public static Handedness LeftHanded = new Handedness { InvertX = true, InvertY = false, InvertZ = false };
        public static Handedness FaceFlipped = new Handedness { InvertX = true, InvertY = false, InvertZ = true };
        public static Handedness Lefty_FaceFlipped = new Handedness { InvertX = false, InvertY = false, InvertZ = true };

        public static void Update()
        {
            if (Res.LefthandedMode)
            {
                if (Res.ScreenFlipMode) Current = Lefty_FaceFlipped;
                else Current = LeftHanded;
            }
            else
            {
                if (Res.ScreenFlipMode) Current = FaceFlipped;
                else Current = Default;
            }
        }
    }
}