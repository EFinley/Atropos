

using Android.App;
using Android.OS;
using Android.Widget;
using MiscUtil;
using System;
using System.Collections.Generic;
using System.Linq;
// using Accord.Math;
// using Accord.Statistics;
using System.Threading.Tasks;

namespace Atropos.Encounters
{
    //internal static class ElementExtensions
    //{
    //    public static EncounterElement Then(this string Name, params object[] nextEntries)
    //    {
    //        return new EncounterElement(Name).Then(nextEntries);
    //    }
    //}

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
        public void Begin() { isActive = true; startedAt = DateTime.Now; OnBegin.Raise(Name); }
        public void Complete() { isActive = false; hasBeenCompleted = true; finishedAt = DateTime.Now; OnComplete.Raise(Name); }
        public Task CompleteAsync(Task previousTask = null) { Complete(); return Task.CompletedTask; }

        public IGestureRecognizerStage EncounterEventLoop;

        protected Func<Task> EncounterTask;
        public object EncounterTaskResult;
        public async Task DoElement() { CurrentElement = this; await (EncounterTask?.Invoke() ?? Task.CompletedTask); }
        //public event EventHandler<EventArgs<string>> OnBecomeAvailable;
        public event EventHandler<EventArgs<string>> OnBegin;
        public event EventHandler<EventArgs<string>> OnComplete;

        public EncounterElement(string name, string buttonLabel = null)
        {
            Name = name;
            ButtonLabel = buttonLabel ?? Name;
            nextElements = new List<EncounterElement>();
        }

        public EncounterElement DuringWhich(Func<Task> encounterTask)
        {
            EncounterTask = async () => { await encounterTask(); Complete(); };
            return this;
        }

        public EncounterElement DuringWhich(Task encounterTask)
        {
            EncounterTask = async () => { await encounterTask; Complete(); };
            return this;
        }

        public EncounterElement DuringWhich(Action encounterAction)
        {
            EncounterTask = () => { encounterAction?.Invoke(); Complete(); return Task.CompletedTask; };
            return this;
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
            return ee.DuringWhich(Speech.SayAllOf(speechtext));
        }

        //public static EncounterElement JustText(string name, string afterText, string label = null)
        //{
        //    var ee = new EncounterElement(name, label);
        //    ee.EncounterTask = () => { name = afterText; return Task.CompletedTask; };
        //    return ee;
        //}

        public static EncounterElement JustSFX(string name, int resId, string label = null, double? volume = null, bool? useSpeakers = null)
        {
            var ee = new EncounterElement(name, label);
            var fx = new Effect("Encounter|" + name, resId);
            return ee.DuringWhich(fx.PlayToCompletion(playVolume: volume, useSpeakers: useSpeakers));
        }

        public static EncounterElement CurrentElement;
        public static EncounterElement SetUpPostcard()
        {
            EncounterElement HackingStage, BypassStage, ShootItOpen, PassTheDoor;
            BypassStage = new EncounterElement("Bypass Panel");
            HackingStage = new EncounterElement("Jack Into Dataport")
                .Then(new EncounterElement("Hacking...").DuringWhich(Task.Delay(1200)));
            ShootItOpen = JustSFX("Shoot It Open", Resource.Raw._169206_security_voice_activating_alarm);
            PassTheDoor = new EncounterElement("Enter")
                                .DuringWhich(async () =>
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
                                });

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
        //public void Add(string name, string laterName)
        //{
        //    Add(EncounterElement.JustText(name, laterName));
        //}
        public void AddSpeak(string name, string speakText)
        {
            Add(EncounterElement.JustSpeech(name, speakText));
        }
    }

    //[Activity(ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    //public class TimeLineActivity : BaseActivity_Portrait
    //{
    //    public EventSequence Sequence;
    //    private List<Button> encounterButtons = new List<Button>();
    //    private Dictionary<EncounterElement, Button> Elems = new Dictionary<EncounterElement, Button>();
    //    protected static TimeLineActivity Current { get; set; }

    //    protected override void OnCreate(Bundle savedInstanceState)
    //    {
    //        base.OnCreate(savedInstanceState);
    //        SetContentView(Resource.Layout.EventTimeline);

    //        // Specific to this scenario
    //        Sequence.Add("Enter via outside door");
    //        Sequence.Add("Reach top of stairs");
    //        Sequence.Add("Doors have been unlocked");
    //        Sequence.Add("Enter subsequent room");
    //        Sequence.Add("Add more steps here...");

    //        SetUpEventButtons();
    //    }
    //    protected override async void OnResume()
    //    {
    //        await base.DoOnResumeAsync(Task.Delay(500), AutoRestart: true);
    //        UpdateEventButtons();
    //    }

    //    protected override void OnPause()
    //    {
    //        base.OnPause();
    //    }

    //    private void SetUpEventButtons()
    //    {
    //        var layoutpanel = FindViewById<LinearLayout>(Resource.Id.Event_timeline_layoutpane);

    //        foreach (var encElem in Sequence.Encounters)
    //        {
    //            var elemButton = new Button(this);
    //            elemButton.SetText(encElem.ButtonLabel, TextView.BufferType.Normal);
    //            elemButton.SetPadding(20, 20, 20, 20);
    //            layoutpanel.AddView(elemButton);
    //            encounterButtons.Add(elemButton);
    //            Elems.Add(encElem, elemButton);

    //            elemButton.Click += (o, e) =>
    //            {
    //                Sequence.CurrentEncounter.Complete();
    //                Sequence.CurrentEncounter = encElem;
    //                Sequence.CurrentEncounter.Begin();
    //            };
    //        }
    //    }

    //    private void UpdateEventButtons()
    //    {
    //        foreach (var enc in Sequence.Encounters)
    //        {
    //            if (enc.hasBeenCompleted) Elems[enc].Enabled = false;
    //        }
    //    }
    //}
}