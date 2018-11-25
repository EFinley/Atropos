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
using System.Threading.Tasks;
using Atropos.Locks;
using System.Numerics;
using MiscUtil;

namespace Atropos.Encounters
{
    public partial class Scenario
    {
        public static Scenario Postcard = new Scenario() { Name = "Postcard"}
            .OnQR("https://www.facebook.com/groups/315482778811048/?scenario=Postcard", "Welcome to Atropos!  Go on through the door to begin your run of: A Postcard From The Shadows.")
            .OnQR("Weapons", "Now that you're out of the public eye, you unpack your duffel bags and get out your weapons and gear.  Go ahead and arm up.")
            .AddVariable("securityDoorUnlock")
            .AddVariable("sentryGunDeactivate")
            .AddVariable("alarmSetOff", State.False)
            .OnVariable("alarmSetOff", State.True, () =>
            {
                var effect = new Effect("AlarmVoice", Resource.Raw._169206_security_voice_activating_alarm);
                effect.Play(SoundOptions.OnSpeakers);
            })
            .AddVariable("sysopSuspects", State.False)
            .OnQR_Locked("Security Door", Lock.Special)
                .IfItIs(State.Locked)
                .Then(async () =>
                {
                    await Speech.SayAllOf("The door is securely locked.");
                    if (RoleActivity.CurrentActivity.GetType() == typeof(SamuraiActivity))
                    {
                        EventHandler<EventArgs<double>> shootOpenDoor = null; // Has to exist before we can use it inside its own definition
                        shootOpenDoor = (object sender, EventArgs<double> e) =>
                        {
                            Current[LockPrefix + "Security Door"] = State.Unlocked;
                            Current["alarmSetOff"] = State.True;
                        };
                        GunfightActivity_Base.OnGunshotFired += shootOpenDoor;
                        Scenario.Current.OnVariable(LockPrefix + "Security Door", State.Unlocked,
                            () => { GunfightActivity_Base.OnGunshotFired -= shootOpenDoor; });
                        await Speech.SayAllOf("You think you could probably shoot the lock off, if you wanted to.");
                    }
                })
                .End_Locked(async () =>
                {
                    if (Current["sentryGunDeactivate"] != State.Success)
                    {
                        await Task.Delay(1250);
                        var gunFX = new Effect("Gunshot", Resource.Raw.gunshot_3, null);
                        foreach (int i in Enumerable.Range(0, 30))
                        {
                            await Task.Delay(50);
                            gunFX.Play(useSpeakers: true, stopIfNecessary: true);
                            if (i == 10) Speech.Say("Your bullet-riddled body slumps to the ground.  If you don't get a healing spell within the next few seconds, you die.");
                        }
                    }
                })
            .OnQR_DependingOn("Stairwell Hacking", UserRole)
                .IfItIs(State.Hacker).Then(async () =>
                {
                    await Speech.SayAllOf("You start hacking the system.  Mime some icon dragging.");
                    await Task.Delay(1000);
                    await Speech.SayAllOf("Now some typing.");
                    await Task.Delay(750);
                    await Speech.SayAllOf("Frantic typing.");
                    await Task.Delay(1250);
                    await Speech.SayAllOf("You're through the ice.  You seem to be logged in to some kind of automated minigun, covering a corridor nearby.  Probably the one right behind this door.  Drag a few icons around while you figure out what you can do here.  Oh, and if you haven't already done so, you might consider telling your team. Or not.");
                    await Task.Delay(1250);
                    await Speech.SayAllOf("Okay, you found a way to put it into a maintenance lock down state, which should keep it from firing, but not trigger the alarm.");
                    await Task.Delay(250);
                    await Speech.SayAllOf("Locking it down.");
                    await Task.Delay(450);
                    await Speech.SayAllOf("Almost there.");
                    await Task.Delay(250);
                    await Speech.SayAllOf("Done.");
                    Current["sentryGunDeactivate"] = State.Success;
                })
                .Otherwise().Then("Gotta be a hacker. Duh.")
                .End_DependingOn()
            .OnQR("Security Panel", () =>
            {
                if (RoleActivity.CurrentActivity.GetType() == typeof(ToolkitActivity))
                {
                    BypassActivity.OnBypassSuccessful += (o, e) => { Current[LockPrefix + "Security Door"] = State.Unlocked; };
                    LaunchActivity(typeof(BypassActivity));
                }
                else Speech.Say("Some kind of security control panel. Your team's spy, A.K.A. operative, will have to tackle this one using their toolkit.");
            })
            #region "Cheat" QR codes to simulate the NFC tags
            //.OnQR("FakeNFC_A", () => BypassActivity.RecognizeFakeNFC("A"))
            //.OnQR("FakeNFC_B", () => BypassActivity.RecognizeFakeNFC("B"))
            //.OnQR("FakeNFC_C", () => BypassActivity.RecognizeFakeNFC("C"))
            //.OnQR("FakeNFC_D", () => BypassActivity.RecognizeFakeNFC("D"))
            #endregion
            .OnQR("Sentry Gun", "This is an Ares Ultimatum-Six autonomous sentry cannon.  Scary fucker.  Do not taunt.")
            .OnQR("Ladder", "Yes, this is just a ladder.  It's in-game if you want it.")
            .OnQR_DependingOn("Corridor Astral", UserRole)
                .IfItIs(State.Sorceror).Then(async () =>
                {
                    await Speech.SayAllOf("You open your inner eye to the swirling currents of astral space, and see a few watcher spirits cruising about.  You'll want to get your spirit ward up before the party proceeds any further.");

                })
                .Otherwise().Then("Gotta be a sorcerer. Duh.")
                .End_DependingOn()
            .OnQR("Door Studio A", "This is the studio Vanessa is shooting in.  You really don't want to risk her wrath.  Trust me.")
            .OnQR_Locked("Door Studio B", Lock.LockByAngles("Studio B", 20, -30, -40))
                .IfItIs(State.Locked).Then("All's quiet on the other side, but the door is locked.")
                .End_Locked(async () =>
                {
                    await Task.Delay(1000);

                    // Testing the new Evasion code here... 
                    var Incoming = new IncomingRangedAttack() { AttackSpeech = "Jerk backward. Fall. Clap your hands to your right eye. You have been nailed by a sniper and have only seconds to live."};
                    var Evasion = new EvasionMode.Duck();
                    var EvasionStage = new IncomingAttackPrepStage<Vector3>(BaseActivity.CurrentActivity, Incoming, Evasion);
                    EvasionStage.Activate();
                })
            .OnQR("Door Studio C", "This door is locked.  On the other side, you can hear several people blaming one another for an equipment failure.")
            .OnQR("Door Studio D", "Before your hand even closes on the doorknob, you can hear shouting, gunshots. Probably blanks. And a director shouting excitedly that he wants it all bigger and badder.")
            .OnQR_Locked("Trap Door", Lock.LockByAngles("Studio B", 20, -30))
                .IfItIs(State.Locked).Then("All's quiet on the other side, but the trap door is locked.")
                .End_Locked(async () =>
                {
                    await Task.Delay(3500);
                    Speech.Say("You spot a sniper lurking up in the windows across the empty studio; apparently your intrusion hasn't gone entirely unnoticed.  Line up a shot and take them out!");
                })
            .AddVariable("lobbyDoorWait")
            .OnQR("Studio Hacking", async () =>
            {
                await Speech.SayAllOf("You start hacking the system.  Mime some icon dragging.");
                await Task.Delay(750);
                await Speech.SayAllOf("Now some typing.");
                await Task.Delay(750);
                await Speech.SayAllOf("Rearrange some windows in front of you.");
                await Task.Delay(1000);
                await Speech.SayAllOf("You've accessed a set of cameras, one of which covers the other side of this door.  Right now there are four stage hands out there moving a large heavy machine on small and uncooperative casters.");
                await Task.Delay(1500);
                await Speech.SayAllOf("They're finally getting it moving.  That, right there, is why you don't have a so-called normal job.");
                await Task.Delay(2000);
                await Speech.SayAllOf("Okay, they're getting it into the studio next door, you should be ready to go.  Wait, no.  An actor and some kind of gopher just came into view from the other direction.");
                await Task.Delay(1000);
                await Speech.SayAllOf("Could they possibly walk any slower?");
                await Task.Delay(1500);
                await Speech.SayAllOf("The two of them are passing right in front of your door now.  The gopher is being a total suck up.  Has he no pride?");
                await Task.Delay(1500);
                await Speech.SayAllOf("Okay!  The coast is clear.");
                Current["lobbyDoorWait"] = State.Success;
            })
            .OnQR_DependingOn("Lobby Door", "lobbyDoorWait")
                .IfItIs(State.Success).Then("Proceed.")
                .Otherwise().Then("You startle a crew of stage hands moving some piece of gear.  In a matter of seconds they will raise the alarm.")
                .End_DependingOn()
            .OnQR("Elevator", "insert elevator text here")
            .OnQR_Locked("Server Room Door", Lock.LockByAngles("serverRoomDoor", -30, 18, 27))
                .End_Locked()
            .OnQR("Server Room Hacking", "insert server room hacking sequence here")
            .OnQR("Server Room Astral", "This room is so steeped in tech that neither you, nor any surveillance mage, can see much of anything.")
            .OnQR_Locked("Office Outer Door", Lock.LockByAngles("officeOuterDoor", -20, 20, 45))
                .End_Locked(async () => 
                {
                    await Task.Delay(5000);
                    // TODO: Check on counterspell here.
                    await Speech.SayAllOf("Freeze! A paralysis spell has been cast on the team.  The more still you can hold, the sooner it will wear off.");
                    // TODO: Continue this with a StillnessProvider.
                })
            .OnQR("Office Hacking", "insert office hacking sequence here - camera looking at empty interior")
            .OnQR("Office Astral", "You spot a shimmering presence just in time to see the white tendrils of a spell approach the members of your team.  Counterspell, fast!")
            .OnQR("Office Inner Door", "Proceed.")
            .OnQR_Locked("Office Safe", Lock.SafeByCombination("officeSafe", 18, -22, 10))
                .IfItIs(State.Unlocked).Then("Open away.")
                .IfItIs(State.Locked).Then("Locked. No surprise there.")
                .End_Locked()
            .OnQR("Office Scanner", "You scan the documents.");
    }
}