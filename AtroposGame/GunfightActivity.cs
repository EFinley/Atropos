

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using Android;
using Android.App;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Android.Util;


// using Accord.Math;
// using Accord.Statistics;
using Android.Content;
using System.Threading.Tasks;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Android.Runtime;
using Android.Views;
using Android.Media;
using Plugin.Vibrate;

using Atropos.Encounters;
using MiscUtil;
using System.Threading;

namespace Atropos
{
    /// <summary>
    /// This is the activity started when we detect a "gun" NFC tag.
    /// </summary>
    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class GunfightActivity : BaseActivity_Portrait
    {
        protected static GunfightActivity Current { get { return (GunfightActivity)CurrentActivity; } set { CurrentActivity = value; } }
        public static event EventHandler<EventArgs<double>> OnGunshotFired;
        public static event EventHandler<EventArgs<double>> OnGunshotHit;
        public static event EventHandler<EventArgs<double>> OnGunshotMiss;

        private Gun ThePlayersGun;

        private ImageView killMarkers;
        private int[] killMarkerResourceIDs
            = new int[]
            {
                -1,
                Resource.Drawable.kill_marks_1,
                Resource.Drawable.kill_marks_2,
                Resource.Drawable.kill_marks_3,
                Resource.Drawable.kill_marks_4,
                Resource.Drawable.kill_marks_5,
                Resource.Drawable.kill_marks_6,
                Resource.Drawable.kill_marks_7,
                Resource.Drawable.kill_marks_8,
                Resource.Drawable.kill_marks_9,
                Resource.Drawable.kill_marks_10,
                Resource.Drawable.kill_marks_star,
            };
        protected int killsCount = 0;

        private LinearLayout bulletPane;
        private List<ImageView> bulletList = new List<ImageView>();
        private StillnessProvider stillnessMonitor;

        public static IEffect ShotHitSFX, ShotMissSFX;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Gunfight);

            //currentSignalsDisplay = FindViewById<TextView>(Resource.Id.current_signals_text);
            //bestSignalsDisplay = FindViewById<TextView>(Resource.Id.best_signals_text);
            //resultsDisplay = FindViewById<TextView>(Resource.Id.result_text);
            //// These are no longer needed - but editing the XML is always a pain.  This is easier, for now, and keeps my options open.
            //currentSignalsDisplay.Visibility = ViewStates.Gone;
            //bestSignalsDisplay.Visibility = ViewStates.Gone;
            //resultsDisplay.Visibility = ViewStates.Gone;

            stillnessMonitor = new StillnessProvider(externalToken: StopToken);

            // See if the current gun is already in our (local, for now) library, and load it if so.  Otherwise, take us to calibration.
            var gunString = Res.SpecificTags.Get(InteractionLibrary.CurrentSpecificTag);
            if (gunString != null && CurrentActivity is GunfightActivity)
            {
                ThePlayersGun = Gun.FromString(gunString, InteractionLibrary.CurrentSpecificTag);
            }
            else if (InteractionLibrary.CurrentSpecificTag == InteractionLibrary.Gunfight.Name + "0000")
            {
                ThePlayersGun = new Gun();
            }
            else
            {
                ThePlayersGun = new Gun(InteractionLibrary.CurrentSpecificTag);
                //InteractionLibrary.Current = InteractionLibrary.GunCalibration;
            }

            bulletPane = FindViewById<LinearLayout>(Resource.Id.gunfight_bullet_pane);
            foreach (var i in Enumerable.Range(0, ThePlayersGun.MaxAmmoCapacity))
            {
                var bulletI = new ImageView(this);
                bulletI.SetImageDrawable(GetDrawable(Resource.Drawable.bullet));
                bulletI.LayoutParameters = new ViewGroup.LayoutParams(width: ViewGroup.LayoutParams.MatchParent, height: ViewGroup.LayoutParams.WrapContent);
                bulletList.Add(bulletI);
                bulletPane.AddView(bulletI);
            }

            killMarkers = FindViewById<ImageView>(Resource.Id.gunfight_killmarkers);

            //IActivator calibration_startStage = new Gun_Calibration_Stage("GravityCal", ThePlayersGun);
            IActivator aiming_startStage = new Gunfight_AimStage("Aim", ThePlayersGun, StopToken);
            //CurrentStage = (InteractionLibrary.Current == InteractionLibrary.GunCalibration) ?
            //    calibration_startStage : aiming_startStage;
            CurrentStage = aiming_startStage;

            //var button = FindViewById<Button>(Resource.Id.finish_btn);
            //button.Click += (sender, args) => { InteractionMode.Current = null; Finish(); };

            //Res.SFX.Register(this, "Gun.Cock", Resource.Raw.gun_cock);
            //Res.SFX.Register(this, "Gun.Shot.0", Resource.Raw.gunshot_0, "Gun.Shot");
            //Res.SFX.Register(this, "Gun.Shot.2", Resource.Raw.gunshot_2, "Gun.Shot");
            //Res.SFX.Register(this, "Gun.Shot.3", Resource.Raw.gunshot_3, "Gun.Shot");
            //Res.SFX.Register(this, "Gun.Shot.4", Resource.Raw.gunshot_4, "Gun.Shot");
            //Res.SFX.Register(this, "Gun.Shot.6", Resource.Raw.gunshot_6, "Gun.Shot");

            ThePlayersGun.CockSFX = new EffectGroup("Gun.Cock", new Effect("Gun.Cock.1", Resource.Raw.gun_cock),
                                                         new Effect("Gun.Cock.2", Resource.Raw._55337_gun_cock_2),
                                                         new Effect("Gun.Cock.3", Resource.Raw._55340_gun_cock_3));
            ThePlayersGun.ShotSFX = new EffectGroup("Gun.Shot", new Effect("Gun.Shot.0", Resource.Raw.gunshot_0),
                                                         new Effect("Gun.Shot.2", Resource.Raw.gunshot_2),
                                                         new Effect("Gun.Shot.3", Resource.Raw.gunshot_3),
                                                         new Effect("Gun.Shot.4", Resource.Raw.gunshot_4),
                                                         new Effect("Gun.Shot.6", Resource.Raw.gunshot_6));
            ThePlayersGun.SteadinessHintSFX = new Effect("Gun.Exhale", Resource.Raw.exhale);
            ThePlayersGun.ClickEmptySFX = new Effect("Gun.ClickEmpty", Resource.Raw.gun_click_empty);
            ThePlayersGun.ReloadSFX = Res.SFX.Preregister("Gun.Reload", Resource.Raw._348155_reload_seq, 0.0, ThePlayersGun?.ReloadTime.TotalSeconds ?? 5.0);

            ShotHitSFX = new EffectGroup("Gun.Hit", //new Effect("Gun.Hit.0", Resource.Raw._104170_mild_ouch),
                                                         //new Effect("Gun.Hit.1", Resource.Raw._129346_male_grunt_1),
                                                         new Effect("Gun.Hit.2", Resource.Raw._188544_breathy_ugh),
                                                         new Effect("Gun.Hit.3", Resource.Raw._262279_male_grunt_3),
                                                         new Effect("Gun.Hit.4", Resource.Raw._340280_male_oof_med),
                                                         new Effect("Gun.Hit.5", Resource.Raw._340285_female_oof_med),
                                                         new Effect("Gun.Hit.6", Resource.Raw._34249_mild_agh),
                                                         new Effect("Gun.Hit.7", Resource.Raw._44430_vocalized_ow)
                                                         //,new Effect("Gun.Shot.8", Resource.Raw._85553_male_grunt_2)
                                                         );
            ShotMissSFX = new EffectGroup("Gun.Miss", new Effect("Gun.Miss.0", Resource.Raw._148840_multiple_ricochet),
                                                         new Effect("Gun.Miss.1", Resource.Raw._156140_dull_platic_or_metal_impact),
                                                         //new Effect("Gun.Miss.2", Resource.Raw._169551_crisp_faint_impact),
                                                         new Effect("Gun.Miss.3", Resource.Raw._170509_crate_impact),
                                                         new Effect("Gun.Miss.4", Resource.Raw._96629_ricochet_metal),
                                                         new Effect("Gun.Miss.5", Resource.Raw._96630_ricochet_metal2),
                                                         new Effect("Gun.Miss.6", Resource.Raw._96631_ricochet_metal3),
                                                         new Effect("Gun.Miss.7", Resource.Raw._96632_ricochet_metal4),
                                                         new Effect("Gun.Miss.8", Resource.Raw._96633_ricochet_metal5),
                                                         new Effect("Gun.Miss.9", Resource.Raw._96634_ricochet_wood),
                                                         new Effect("Gun.Miss.10", Resource.Raw._96635_ricochet_wood2),
                                                         new Effect("Gun.Miss.11", Resource.Raw._96636_ricochet_wood3)
                                                         );

            //SetTagRemovalResult(Finish, 10, 2);
            //LinkSeekbars();

            useVolumeTrigger = true;
            OnVolumeButtonPressed += (o, e) => { ((Gunfight_AimStage)CurrentStage).ResolveTriggerPull(); };

            OnGunshotFired += (o, e) => 
            {
                if (e.Value > 0) OnGunshotHit.Raise(e.Value);
                else OnGunshotMiss.Raise(-e.Value);
            };
        }

        protected override void OnResume()
        {
            base.DoOnResume(async () =>
            {
                await System.Threading.Tasks.Task.Delay(750);
                //Res.SFX.PlaySound("Gun.Cock");
                ThePlayersGun.CockSFX.Play(useSpeakers: true);
                await System.Threading.Tasks.Task.Delay(500);
            });
        }

        //public static void RelayMessage(string message, bool useSecondaryDisplay = false)
        //{
        //    //return; // Debug
        //    try
        //    {
        //        //if (!useSecondaryDisplay) CurrentActivity.RunOnUiThread(() =>
        //        //        {
        //        //            ((GunfightActivity)CurrentActivity).currentSignalsDisplay.Text = message;
        //        //        });
        //        //else CurrentActivity.RunOnUiThread(() => { ((GunfightActivity)CurrentActivity).bestSignalsDisplay.Text = message; });
        //    }
        //    catch (Exception)
        //    {
        //        throw;
        //    }
        //}

        public void SetBulletDisplay(int numBullets)
        {
            //bulletList.Select((b, i) => { b.Visibility = (i <= numBullets) ? ViewStates.Visible : ViewStates.Invisible; return b.Visibility; });
            for (int i = 0; i < ThePlayersGun.MaxAmmoCapacity; i++)
            {
                if (i < numBullets) bulletList[i].Visibility = ViewStates.Visible;
                else bulletList[i].Visibility = ViewStates.Gone;
            }
            bulletPane.Invalidate();
        }

        public void AddKillTallymark()
        {
            killsCount++;
            killMarkers.SetImageResource(killMarkerResourceIDs[killsCount.Clamp(0, 11)]);
            killMarkers.Visibility = ViewStates.Visible;
            killMarkers.Invalidate();
        }


        // Because multiple different routines might be eligible for this - here we have the aiming and the (obsolete) calibration stage.
        private interface IRespondToTriggerPulls
        {
            void ResolveTriggerPull();
        }

        private class Gunfight_AimStage : GestureRecognizerStage, IRespondToTriggerPulls
        {
            private Gun Weapon;
            private DateTime lastTriggerPull, nextReadyTime;
            private StillnessProvider Stillness;
            private GravityOrientationProvider Gravity;
            private bool exhaleCueHasBeenProvided = false;
            private Random random;

            public Gunfight_AimStage(string label, Gun gun, CancellationToken? externalToken = null, bool AutoStart = false) : base(label)
            {
                Weapon = gun;
                DependsOn(externalToken ?? CancellationToken.None);
                //SetUpParser(Weapon.PitchAccel, Weapon.MinimumAimTime, 2, 1);
                Stillness = new StillnessProvider(externalToken: StopToken);
                SetUpProvider(Stillness);
                Gravity = new GravityOrientationProvider(null, StopToken);
                Gravity.Activate();
                random = new Random();
                
                if (AutoStart) Activate();
            }

            protected override void startAction()
            {
                //RelayMessage("Waiting for user to aim...");
                //updateTime = DateTime.Now + Weapon.UpdateInterval;

                lastTriggerPull = DateTime.Now;
                nextReadyTime = DateTime.Now + Weapon.MinimumAimTime;
            }

            protected override bool interimCriterion()
            {
                return true;
            }

            protected override void interimAction()
            {
                if (Vector3.Dot(Gravity.Vector, Weapon.vectorPointedForward) > 0.5) return; // Not close enough to vertical, ignore.
                if (Stillness.StillnessScore + Stillness.InstantaneousScore > 15 
                    && !exhaleCueHasBeenProvided 
                    && DateTime.Now > nextReadyTime)
                    //&& Weapon.CurrentAmmoCount > 0)
                {
                    exhaleCueHasBeenProvided = true;
                    Weapon.SteadinessHintSFX.Play(0.4, useSpeakers: false);
                }
                if (Stillness.StillnessScore < 1 && exhaleCueHasBeenProvided)
                {
                    exhaleCueHasBeenProvided = false;
                }
            }

            public async void ResolveTriggerPull()
            {
                try
                {
                    lock (volumeButtonSyncLock)
                    {
                        if (DateTime.Now < nextReadyTime) return; // Both debouncing and conceptually letting the gun's mechanism cycle - two birds, one rock!
                        lastTriggerPull = DateTime.Now;
                        nextReadyTime = DateTime.Now + Weapon.CooldownPeriod; 
                    }

                    if (Weapon.CurrentAmmoCount == 0)
                    {
                        //Log.Debug("Gunfight|Empty", $"Clicking on empty; currently, the dot product from 'forward' to 'gravity' is {Vector3.Dot(Gravity.Vector, Weapon.vectorPointedForward)}.");
                        if (Math.Abs(Vector3.Dot(Gravity.Vector, Weapon.vectorPointedForward)) < 0.75)
                        {
                            await (Weapon.ClickEmptySFX?.PlayToCompletion(null, true) ?? Task.CompletedTask);
                            return;
                        }
                        else
                        {
                            Weapon.ReloadSFX?.Play(useSpeakers: true);
                            nextReadyTime += Weapon.ReloadTime;
                            while (Weapon.CurrentAmmoCount < Weapon.MaxAmmoCapacity)
                            {
                                await Task.Delay((int)(Weapon.ReloadTime.TotalMilliseconds / (Weapon.MaxAmmoCapacity + 1)));
                                //await Task.Delay((int)(Weapon.ReloadTime.TotalMilliseconds / Weapon.MaxAmmoCapacity));
                                Current.SetBulletDisplay(++Weapon.CurrentAmmoCount);
                                //Weapon.ClickEmptySFX.Play(0.5); // Stand-in for the currently null ReloadSFX.
                            }
                            return; 
                        }
                    }

                    Weapon.CurrentAmmoCount--;
                    Current.SetBulletDisplay(Weapon.CurrentAmmoCount);

                    exhaleCueHasBeenProvided = false;

                    await Task.WhenAll(Task.Delay(150).ContinueWith(_ => CrossVibrate.Current.Vibration(50)), 
                        Weapon.ShotSFX.PlayToCompletion(1.0, true));
                    await Task.Delay(random.Next(100, 400));

                    // Do the dice rolls now
                    var DidHit = DidItHit();

                    // And assess the results one way or the other
                    bool DoResultSFX;
                    double ResultSFXVolume;
                    if (DidHit)
                    {
                        DoResultSFX = random.Next(1, 20) < 17;
                        ResultSFXVolume = 0.25 * (3.0 + Math.Max(random.NextDouble(), random.NextDouble()));
                        Current.AddKillTallymark();
                    }
                    else
                    {
                        DoResultSFX = random.Next(1, 20) < 11;
                        ResultSFXVolume = random.NextDouble();
                    }

                    // Play the SFX depending on hit/miss status and the odds (within each) of an audible response.  Not every wound/ricochet is audible!
                    if (DoResultSFX)
                    {
                        if (DidHit)
                        {
                            ShotHitSFX.Play(ResultSFXVolume, useSpeakers: true);
                            // Experimental!!
                            Atropos.Communications.HeyYou.MyTeammates.PlaySFX(ShotHitSFX);
                        }
                        else
                        {
                            ShotMissSFX.Play(ResultSFXVolume, useSpeakers: true);
                            // Experimental!!
                            Atropos.Communications.HeyYou.MyTeammates.PlaySFX(ShotMissSFX);
                        }
                    }

                    // Testing the new Evasion code here... for now after every third shot.
                    if (Weapon.CurrentAmmoCount % 3 == 0)
                    {
                        var Incoming = new IncomingRangedAttack();
                        EvasionMode<Vector3> Evasion = (Res.CoinFlip) ? new EvasionMode.Dodge() : new EvasionMode.Duck();
                        var EvasionStage = new IncomingAttackPrepStage<Vector3>(Current, Incoming, Evasion);
                        EvasionStage.Activate(); 
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }

            public bool DidItHit()
            {
                if (Vector3.Dot(Gravity.Vector, Weapon.vectorPointedForward) > 0.66)
                {
                    OnGunshotFired.Raise(-10);
                    return false; // Not close enough to horizontal, automatic miss.
                }
                // Placeholder function.  For now, fuck it, treating the Steadiness score as a d20 target number. ;)
                var TN = Stillness.StillnessScore + Stillness.InstantaneousScore;
                var dieRoll = random.Next(1, 20);
                Log.Info("DidItHit", $"Shot results: TN of {TN:f1}, based on cumulative {Stillness.StillnessScore:f1} & instantaneous {Stillness.InstantaneousScore:f1}.  Die roll {dieRoll}.");

                OnGunshotFired.Raise(TN - dieRoll);
                return dieRoll < TN;
            }

        }

        public class Gun_Calibration_Stage : GestureRecognizerStage, IRespondToTriggerPulls
        {
            private Gun Weapon;
            private StillnessProvider Stillness;
            private GravityOrientationProvider Gravity;
            private Vector3[] simpleAxes = new Vector3[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };

            public Gun_Calibration_Stage(string label, Gun gun, bool AutoStart = false) : base(label)
            {
                Weapon = gun;
                //SetUpParser(Weapon.GravityVector, 5.0);
                Stillness = new StillnessProvider();
                SetUpProvider(Stillness);

                Gravity = new GravityOrientationProvider();
                Gravity.Activate();

                if (AutoStart) Activate();
            }

            protected override async void startAction()
            {
                await Speech.SayAllOf("If your smartphone is mounted either parallel to the barrel of your prop, or perpendicular to it, then simply aim your weapon at the ground now and fire." +
                    "If not, either wait for Ahtreaupeaus Beta or.  Heh.  rebuild your prop so it is." );
            }

            //protected override bool nextStageCriterion()
            //{
            //    //var relevantData = Data.MostRecent(TimeSpan.FromSeconds(0.75)).Select(pt => (double)pt.value.Length());
            //    //return (relevantData.StandardDeviation() > 0.5 * relevantData.RootMeanSquare());
            //    return Stillness.StillnessScore > 0 && simpleAxes.Select(axis => Vector3.Dot(axis, Gravity.Vector)).Max() > 0.8;
            //}
            //protected override async Task nextStageActionAsync()
            //{
            //    Weapon.vectorPointedForward = simpleAxes.Single(axis => Vector3.Dot(axis, Gravity.Vector) > 0.8);
            //    Res.SpecificTags.Put(Weapon.TagID, Weapon.ToString());
            //    await Speech.SayAllOf("Thank you. Storing the information. Your weapon is now ready for use.  Fire at will.");
            //    CurrentStage = new Gunfight_AimStage("Calibrated", Weapon);
            //    CurrentStage.Activate();
            //}

            public async void ResolveTriggerPull()
            {
                Weapon.vectorPointedForward = simpleAxes.Single(axis => Vector3.Dot(axis, Gravity.Vector) > 0.8);
                Res.SpecificTags.Put(Weapon.TagID, Weapon.ToString());
                await Speech.SayAllOf("Thank you. Storing the information. Your weapon is now ready for use.  Fire at will.");
                CurrentStage = new Gunfight_AimStage("Calibrated", Weapon);
                CurrentStage.Activate();
            }
        }
        
    }
}