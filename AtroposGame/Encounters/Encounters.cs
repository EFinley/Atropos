

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CSharp;

using Android;
using Android.App;
using Android.Nfc;
using Android.OS;
using Android.Widget;
using Android.Util;
using DeviceMotion.Plugin;
using DeviceMotion.Plugin.Abstractions;
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
using MiscUtil;

namespace Atropos.Encounters
{
    //internal static class ElementExtensions
    //{
    //    public static EncounterElement Then(this string Name, params object[] nextEntries)
    //    {
    //        return new EncounterElement(Name).Then(nextEntries);
    //    }
    //}

    public static class VibrateExtensions
    {
        public static void Vibration(this Plugin.Vibrate.Abstractions.IVibrate source, double milliseconds)
        {
            source.Vibration(TimeSpan.FromMilliseconds(milliseconds));
        }
    }

    public class EncounterElement
    {
        public string Name;
        public string ButtonLabel;

        public bool isActive = false;
        public bool hasBeenCompleted = false;
        private List<EncounterElement> _nextElements;
        public List<EncounterElement> nextElements { get { _nextElements = _nextElements.Where(e => !e.hasBeenCompleted).ToList(); return _nextElements; } set { _nextElements = value; } }

        public bool isVisible = false;

        public DateTime startedAt, finishedAt;
        public void Begin() { isActive = true; startedAt = DateTime.Now; OnBegin?.Invoke(this, new EventArgs<string>(Name)); }
        public void Complete() { isActive = false; hasBeenCompleted = true; finishedAt = DateTime.Now; OnComplete?.Invoke(this, new EventArgs<string>(Name)); }

        public IGestureRecognizerStage EncounterEventLoop;

        protected Func<Task> _doElement;
        public async Task DoElement() { CurrentElement = this; await (_doElement?.Invoke() ?? Task.CompletedTask); }
        //public event EventHandler<EventArgs<string>> OnBecomeAvailable;
        public event EventHandler<EventArgs<string>> OnBegin;
        public event EventHandler<EventArgs<string>> OnComplete;

        public EncounterElement(string name, string buttonLabel = null)
        {
            Name = name;
            ButtonLabel = buttonLabel ?? Name;
            nextElements = new List<EncounterElement>();
        }

        public EncounterElement Then(params EncounterElement[] nextElems)
        {
            if (nextElements?.Count > 0) nextElements.Last().Then(nextElems);
            else nextElements = nextElems.ToList();
            return this;
        }
        public EncounterElement Then(params string[] names)
        {
            return Then(names.Select(n => new EncounterElement(n)).ToArray());
        }
        public EncounterElement Then(params object[] entries)
        {
            return Then(
                entries.Select<object, EncounterElement>((e) => 
                {
                    //dynamic tempE = e;
                    //if (e is EncounterElement) return (EncounterElement)tempE;
                    //else if (e is string) return new EncounterElement((string)tempE);
                    if (e is EncounterElement) return Operator.Convert<object, EncounterElement>(e);
                    else if (e is string) return new EncounterElement((string)e);
                    else throw new Exception("???");
                })
            );
        }

        public EncounterElement AndAlso(params EncounterElement[] nextElems)
        {
            nextElements.AddRange(nextElems);
            return this;
        }

        public static EncounterElement JustSpeech(string name, string speechtext, string label = null)
        {
            var ee = new EncounterElement(name, label);
            ee._doElement = () => { return Speech.SayAllOf(speechtext); };
            return ee;
        }

        public static EncounterElement JustText(string name, string afterText, string label = null)
        {
            var ee = new EncounterElement(name, label);
            ee._doElement = () => { name = afterText; return Task.CompletedTask; };
            return ee;
        }

        public static EncounterElement JustSFX(string name, int resId, string label = null, double? volume = null, bool? useSpeakers = null)
        {
            var ee = new EncounterElement(name, label);
            var fx = new Effect("Encounter|" + name, resId);
            ee._doElement = () => { return fx.PlayToCompletion(playVolume: volume, useSpeakers: useSpeakers); };
            return ee;
        }

        public static EncounterElement CurrentElement;
        public static EncounterElement SetUpPostcard()
        {
            EncounterElement HackingStage, BypassStage, ShootItOpen, PassTheDoor;
            BypassStage = new EncounterElement("Bypass Panel");
            HackingStage = new EncounterElement("Jack Into Dataport")
                .Then(new EncounterElement("Hacking...") { _doElement = () => { return Task.Delay(1200); } });
            ShootItOpen = JustSFX("Shoot It Open", Resource.Raw._169206_security_voice_activating_alarm);
            PassTheDoor = new EncounterElement("Enter").Then(
                new EncounterElement("Sentry Gun!")
                {
                    _doElement = async () =>
                    {
                        if (!HackingStage.hasBeenCompleted)
                        {
                            var gunFX = new Effect("Gunshot", Resource.Raw.gunshot_3, null);
                            foreach (int i in Enumerable.Range(0, 20))
                            {
                                await Task.Delay(50);
                                gunFX.Play(useSpeakers: true, stopIfNecessary: true);
                            }
                        }
                    }
                }
            );

            BypassStage.Then(HackingStage, PassTheDoor);
            HackingStage.Then(BypassStage, ShootItOpen);
            ShootItOpen.Then(PassTheDoor);

            return new EncounterElement("Begin 'Postcard'")
                .Then("Enter facility...")
                .Then("Open security panel")
                .Then(BypassStage, HackingStage, ShootItOpen);
        }
        //public static EncounterElement None = new EncounterElement("None");
    }

    public class EventSequence : OrderedDictionary<string, EncounterElement>
    {
        public List<EncounterElement> Encounters { get { return base.Values.ToList(); } }
        public EncounterElement CurrentEncounter;
        public void Add(EncounterElement elem)
        {
            base.Add(elem.Name, elem);
        }
        public void Add(string name)
        {
            Add(new EncounterElement(name));
        }
        public void Add(string name, string laterName)
        {
            Add(EncounterElement.JustText(name, laterName));
        }
        public void AddSpeak(string name, string speakText)
        {
            Add(EncounterElement.JustSpeech(name, speakText));
        }
    }

    [Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class TimeLineActivity : BaseActivity_Portrait, IRelayMessages
    {
        public EventSequence Sequence;
        private List<Button> encounterButtons = new List<Button>();
        private Dictionary<EncounterElement, Button> Elems = new Dictionary<EncounterElement, Button>();
        protected static TimeLineActivity Current { get; set; }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.EventTimeline);

            // Specific to this scenario
            Sequence.Add("Enter via outside door");
            Sequence.Add("Reach top of stairs");
            Sequence.Add("Doors have been unlocked");
            Sequence.Add("Enter subsequent room");
            Sequence.Add("Add more steps here...");

            SetUpEventButtons();
        }
        protected override async void OnResume()
        {
            await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
            UpdateEventButtons();
        }

        protected override void OnPause()
        {
            base.OnPause();
        }

        private void SetUpEventButtons()
        {
            var layoutpanel = FindViewById<LinearLayout>(Resource.Id.Event_timeline_layoutpane);

            foreach (var encElem in Sequence.Encounters)
            {
                var elemButton = new Button(this);
                elemButton.SetText(encElem.ButtonLabel, TextView.BufferType.Normal);
                elemButton.SetPadding(20, 20, 20, 20);
                layoutpanel.AddView(elemButton);
                encounterButtons.Add(elemButton);
                Elems.Add(encElem, elemButton);

                elemButton.Click += (o, e) =>
                {
                    Sequence.CurrentEncounter.Complete();
                    Sequence.CurrentEncounter = encElem;
                    Sequence.CurrentEncounter.Begin();
                };
            }
        }

        private void UpdateEventButtons()
        {
            foreach (var enc in Sequence.Encounters)
            {
                if (enc.hasBeenCompleted) Elems[enc].Enabled = false;
            }
        }

        public void RelayMessage(string message, int RelayTargetId = 1)
        {
            return; // Debug
            //try
            //{
            //    if (!useSecondaryDisplay) RunOnUiThread(() =>
            //            {
            //                currentSignalsDisplay.Text = message;
            //            });
            //    else RunOnUiThread(() => { bestSignalsDisplay.Text = message; });
            //}
            //catch (Exception)
            //{
            //    throw;
            //}
        }

        //public class BeginCastingSpecificSpellStage : GestureRecognizerStage
        //{
        //    private Focus Implement;
        //    private StillnessProvider Stillness;
        //    private GravityOrientationProvider Gravity;
        //    private Task sayIt;

        //    public BeginCastingSpecificSpellStage(string label, Focus Focus, bool AutoStart = false) : base(label)
        //    {
        //        Implement = Focus;
        //        Stillness = new StillnessProvider();
        //        Stillness.StartDisplayLoop(Current, 500);
        //        SetUpProvider(Stillness);
        //        Gravity = new GravityOrientationProvider();
        //        Gravity.Activate();

        //        if (AutoStart) Activate();
        //    }

        //    protected override async void startAction()
        //    {
        //        sayIt = Speech.SayAllOf($"Casting {Current.SpellBeingCast.SpellName}.  Take your zero stance to begin.");
        //        await sayIt;
        //    }

        //    protected override bool interimCriterion()
        //    {
        //        return Stillness.IsItDisplayUpdateTime();
        //    }

        //    protected override void interimAction()
        //    {
        //        Stillness.DoDisplayUpdate();
        //    }

        //    protected override bool nextStageCriterion()
        //    {
        //        return (Stillness.ReadsMoreThan(6f)
        //            && Current.SpellBeingCast.AngleTo(Gravity.Quaternion) < 30f);
        //    }
        //    protected override async void nextStageAction()
        //    {
        //        var newProvider = new GravityOrientationProvider();
        //        newProvider.Activate();
        //        await newProvider.SetFrameShiftFromCurrent();
        //        await Speech.SayAllOf("Begin");
        //        CurrentStage = new GlyphCastingStage($"Glyph 0", Implement, Current.SpellBeingCast.Glyphs[0], newProvider);
        //    }
        //}

        //public class BeginCastingSelectedSpellsStage : GestureRecognizerStage
        //{
        //    private Focus Focus;
        //    public StillnessProvider Stillness;
        //    private AdvancedRollingAverageQuat AverageAttitude;
        //    private FrameShiftedOrientationProvider AttitudeProvider;

        //    private List<Spell> PreparedSpells;
        //    private List<Spell> SpellsSortedByAngle { get {
        //            return PreparedSpells
        //                    .OrderBy(sp => sp.AngleTo(AttitudeProvider))
        //                    .ToList();
        //        } }
        //    private IEffect GetFeedbackFX(Spell spell)
        //    {
        //        return spell?.Glyphs?[0]?.FeedbackSFX ?? Effect.None;
        //    }
        //    private Task sayIt;

        //    public BeginCastingSelectedSpellsStage(string label, Focus focus, IEnumerable<Spell> possibleSpells) : base(label)
        //    {
        //        Focus = focus;
        //        Res.DebuggingSignalFlag = true;

        //        Stillness = new StillnessProvider();
        //        Stillness.StartDisplayLoop(Current, 500);
        //        SetUpProvider(Stillness);

        //        AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 10); // Not sure I'm even going to bother using this.
        //        AttitudeProvider = new GravityOrientationProvider();
        //        AttitudeProvider.Activate();
        //        AverageAttitude.Update(AttitudeProvider);

        //        PreparedSpells = possibleSpells.ToList();

        //        Activate();
        //    }

        //    public BeginCastingSelectedSpellsStage(string label, Focus focus, Spell singleSpell)
        //        : this(label, focus, new Spell[] { singleSpell }) { }

        //    protected override async Task startActionAsync()
        //    {
        //        string[] spellNames = PreparedSpells.Select(s => s.SpellName).ToArray();
        //        if (spellNames.Length == 0)
        //        {
        //            await Speech.SayAllOf("Entering spell casting mode but with no spells prepared!");
        //            Deactivate();
        //            return;
        //        }
        //        else if (spellNames.Length == 1)
        //        {
        //            Spell singleSpell = PreparedSpells.Single();
        //            sayIt = Speech.SayAllOf($"Casting {singleSpell.SpellName}.  Take your zero stance to begin.");
        //        }
        //        else
        //        {
        //            string spellNamesList;
        //            if (spellNames.Length == 2) spellNamesList = $"{spellNames[0]} and {spellNames[1]}";
        //            else spellNamesList = $"{spellNames.Length} spells";
        //            sayIt = Speech.SayAllOf($"Enter spell casting mode.  You have {spellNamesList} ready for casting.  Take a spell's zero stance to begin.");
        //        }
        //        await sayIt;
        //        foreach (var fx in PreparedSpells.Select(s => GetFeedbackFX(s))) fx.Play(0.0, true);
        //    }

        //    protected override bool nextStageCriterion()
        //    {
        //        //if (!FrameShiftFunctions.CheckIsReady(AttitudeProvider)) return false;
        //        var leeWay = Stillness.StillnessScore + 2f * Sqrt(Stillness.RunTime.TotalSeconds); // VIDEO way too easy for normal use.
        //        if (SpellsSortedByAngle[0].AngleTo(AttitudeProvider) < 15f + leeWay)
        //        {
        //            if (SpellsSortedByAngle.Count < 2) return true;
        //            else return (SpellsSortedByAngle[0].AngleTo(AttitudeProvider) < 0.5 * SpellsSortedByAngle[1].AngleTo(AttitudeProvider));
        //        }
        //        return false;
        //    }

        //    protected override void nextStageAction()
        //    {
        //        try
        //        {
        //            foreach (var fx in SpellsSortedByAngle.Select(s => GetFeedbackFX(s))) fx.Deactivate();

        //            Current.SpellBeingCast = SpellsSortedByAngle[0];

        //            Plugin.Vibrate.CrossVibrate.Current.Vibration(15);
        //            Current.SpellBeingCast.Glyphs?[0]?.ProgressSFX?.Play();
        //            AttitudeProvider.FrameShift = Current.SpellBeingCast.ZeroStance;
        //            CurrentStage = new GlyphCastingStage($"{Current.SpellBeingCast.SpellName} Glyph 0", Focus, Current.SpellBeingCast.Glyphs[0], AttitudeProvider);
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Error("Spell selection stage", e.Message);
        //            throw;
        //        }
        //    }

        //    protected override bool interimCriterion()
        //    {
        //        return true;
        //    }

        //    protected override void interimAction()
        //    {
        //        foreach (var s in SpellsSortedByAngle)
        //        {
        //            //FeedbackFX(s).Volume = Exp(-Sqrt(s.AngleTo(AttitudeProvider) / 8.0) + 0.65); // Old version
        //            GetFeedbackFX(s).Volume = 1.2f * Exp(-0.45f * (s.AngleTo(AttitudeProvider) / 5f - 1f));

        //        }
        //    }
        //}

        //public class GlyphCastingStage : GestureRecognizerStage
        //{
        //    private Focus Implement;
        //    public StillnessProvider Stillness;
        //    private float Volume;
        //    private AdvancedRollingAverageQuat AverageAttitude;
        //    private FrameShiftedOrientationProvider AttitudeProvider;
        //    private Glyph targetGlyph;

        //    public GlyphCastingStage(string label, Focus Focus, Glyph tgtGlyph, FrameShiftedOrientationProvider oProvider = null) : base(label)
        //    {
        //        Implement = Focus;
        //        targetGlyph = tgtGlyph;
        //        Res.DebuggingSignalFlag = true;

        //        Stillness = new StillnessProvider();
        //        Stillness.StartDisplayLoop(Current, 500);
                
        //        SetUpProvider(Stillness);
        //        Volume = 0.01f;

        //        AverageAttitude = new AdvancedRollingAverageQuat(timeFrameInPeriods: 10);
        //        AttitudeProvider = oProvider ?? new GravityOrientationProvider(Implement.FrameShift);
        //        AttitudeProvider.Activate();
        //        AverageAttitude.Update(AttitudeProvider);

        //        Activate();
        //    }

        //    protected override void startAction()
        //    {
        //        targetGlyph.FeedbackSFX.Play(Volume, true);
        //    }

        //    private double score;
        //    protected override bool nextStageCriterion()
        //    {
        //        score = targetGlyph.AngleTo(AttitudeProvider) - Stillness.StillnessScore / 4f - Sqrt(Stillness.RunTime.TotalSeconds);
        //        return (score < 12f && FrameShiftFunctions.CheckIsReady(AttitudeProvider));
        //    }
        //    protected override async Task nextStageActionAsync()
        //    {
        //        try
        //        {
        //            Log.Info("Casting stages", $"Success on {this.Label}. Angle was {targetGlyph.AngleTo(AttitudeProvider):f2} degrees [spell baseline on this being {targetGlyph.OrientationSigma:f2}], " +
        //                $"steadiness was {Stillness.StillnessScore:f2} [baseline {targetGlyph.SteadinessScoreWhenDefined:f2}], time was {Stillness.RunTime.TotalSeconds:f2}s [counted as {Math.Sqrt(Stillness.RunTime.TotalSeconds):f2} degrees].");
        //            targetGlyph.FeedbackSFX.Stop();
        //            await Task.Delay(150);

        //            if (targetGlyph.NextGlyph == Glyph.EndOfSpell)
        //            {
        //                if (Implement != null) Implement.ZeroOrientation = Quaternion.Identity;
        //                AttitudeProvider = null;

        //                Plugin.Vibrate.CrossVibrate.Current.Vibration(50 + 15 * Current.SpellBeingCast.Glyphs.Count);
        //                await Current.SpellBeingCast.CastingResult(this).Before(StopToken);
        //                CurrentStage?.Deactivate();
        //                CurrentStage = NullStage;
        //                if (Current == null) return;
        //                Current.SpellBeingCast = null;
        //                Current.Finish();
        //            }
        //            else
        //            {
        //                Plugin.Vibrate.CrossVibrate.Current.Vibration(25 + 10 * Current.SpellBeingCast.Glyphs.IndexOf(targetGlyph));
        //                targetGlyph.ProgressSFX.Play(1.0f);
        //                await Task.Delay(300); // Give a moment to get ready.
        //                CurrentStage = new GlyphCastingStage($"Glyph {Current.SpellBeingCast.Glyphs.IndexOf(targetGlyph) + 1}", Implement, targetGlyph.NextGlyph, AttitudeProvider);
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            Log.Error("Glyph casting stage progression", e.Message);
        //            throw;
        //        }
        //    }

        //    protected override bool interimCriterion()
        //    {
        //        Stillness.IsItDisplayUpdateTime(); // Updates max and min values.
        //        return true;
        //    }

        //    protected override void interimAction()
        //    {
        //        AverageAttitude.Update(AttitudeProvider.Quaternion);
        //        Volume = (float)Exp(-0.45f * (AttitudeProvider.Quaternion.AngleTo(targetGlyph.Orientation) / 5f - 1f));
        //        //Volume = (float)Exp(-0.5f * (Sqrt(AttitudeProvider.Quaternion.AngleTo(targetGlyph.Orientation)) - 1f)); // Old version
        //        targetGlyph.FeedbackSFX.SetVolume(Volume);

        //        var EulerAnglesOfError = Quaternion.Divide(AttitudeProvider, targetGlyph.Orientation).ToEulerAngles();
        //        string respString = null;

        //        if (Stillness.IsItDisplayUpdateTime())
        //        {
        //            Stillness.DoDisplayUpdate();
        //            respString = $"Casting {this.Label}.\n" +
        //                $"Angle to target {targetGlyph.AngleTo(AttitudeProvider):f1} degrees (score {score:f1})\n" +
        //                $"Volume set to {Volume * 100f:f1}%.";
        //            Current.RelayMessage(respString, true);
        //        }
        //    }
        //}

    }
}