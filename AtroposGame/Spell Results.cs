
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace com.Atropos
{
    class Spell_Results
    {
        public static async Task SonarDemo(object state)
        {
            var m = MasterSpellLibrary.SpellSFX["Magic.SonarAlert"] as Effect;
            await m.PlayFromTo(20, 38.5);
            //await m.WhenFinishedPlaying;
            //var foundSomething = Task.Delay(20000).ContinueWith(
            //    (t) => { MasterSpellLibrary.SpellSFX["Magic.Riffle"].PlayToCompletion(0.65); });
            //var doDecreasingSonarTone = Task.Run(async () =>
            //{
            //    //List<float> volumesArray = new List<float> { 0.32f, 0.32f, 0.324f, 0.325f };
            //    //List<float> pitchesArray = new List<float> { 1.0f, 1.0f, 1.0f, 1.0f };
            //    //while (volumesArray.Last() > 0.02f || volumesArray.Count < 50)
            //    //{
            //    //    var vLast = volumesArray.Last();
            //    //    volumesArray.Add(vLast * (0.95f + 0.05f * Res.RandomF));
            //    //    pitchesArray.Add(1.0f);
            //    //}
            //    int i0 = 24;
            //    List<float> volumesArray = new List<float>();
            //    List<float> pitchesArray = new List<float>();
            //    for (int i = 0; i < 3 * i0; i++)
            //    {
            //        //volumesArray[i] = volumesArray[i] + (1.0f - volumesArray[i]) * (float)Math.Pow(0.75, Math.Abs((i - i0) - 3)); // At 500ms intervals, this starts at t+15 seconds.

            //        var factor = (float)Math.Exp(-(i - i0) * (i - i0) / (i0 / 2));
            //        pitchesArray.Add( 1.0f + 1.5f * factor);
            //        volumesArray.Add( 0.05f + 0.2f * factor);
            //    }

            //    var sonarFX = MasterSpellLibrary.SpellSFX["Magic.Detection"];
            //    sonarFX.Play(volumesArray[0], playLooping: true);

            //    //while (sonarFX.Volume > 0.02f)
            //    //foreach (var vol in volumesArray)
            //    for (int i = 0; i < volumesArray.Count; i++)
            //    {
            //        await Task.Delay((int)(1000.0 / pitchesArray[i]));
            //        //sonarFX.Volume = Math.Min(sonarFX.Volume * 0.993f, sonarFX.Volume - 0.005f);
            //        //sonarFX.Volume = volumesArray[i];
            //        sonarFX.Speed = 1.0 / pitchesArray[i];
            //        sonarFX.Pitch = pitchesArray[i];
            //    }
            //    sonarFX.StopPlayback();
            //});

            ////await Task.WhenAll(foundSomething, doDecreasingSonarTone);
            //await doDecreasingSonarTone;
        }
    }
}