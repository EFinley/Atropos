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
using System.Threading;

namespace Atropos
{
    [Serializable]
    public class SoundOptions
    {
        public double? Volume;
        public double? Pitch;
        public double? Speed;
        public bool? Looping;
        public bool? UseSpeakers;

        [NonSerialized]
        public CancellationToken? CancelToken;

        public static SoundOptions Default = new SoundOptions();
        public static SoundOptions OnSpeakers = new SoundOptions() { UseSpeakers = true };
        public static SoundOptions OnHeadphones = new SoundOptions() { UseSpeakers = false };
        public static SoundOptions AtSpeed(double speed) { return new SoundOptions() { Speed = speed }; }
        public static SoundOptions AtVolume(double volume) { return new SoundOptions() { Volume = volume }; }
    }
    
}