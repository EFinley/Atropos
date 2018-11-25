
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
    // This is a glorified Enum used as our principal centralized search point.
    public static class InteractionLibrary
    {
        public static Res.InteractionMode Current;
        public static string CurrentSpecificTag;
        public static Android.Nfc.Tag CurrentTagHandle;

        public static Res.InteractionMode Gunfight;
        public static Res.InteractionMode SpellCasting;
        public static Res.InteractionMode Examine;
        public static Res.InteractionMode Multimeter;
        public static Res.InteractionMode Wirecutters;
        public static Res.InteractionMode SolderingIron;
        public static Res.InteractionMode LockPicking;
        public static Res.InteractionMode SafeCracking;
        public static Res.InteractionMode SecurityPanel;
        public static Res.InteractionMode Decking;

        public static Res.InteractionMode GunCalibration;
        public static Res.InteractionMode SpellTeaching;
        public static Res.InteractionMode MeleeTeaching;
        public static Res.InteractionMode LockTraining;

        public static void InitializeAll()
        {
            Gunfight = Res.DefineInteractionMode("gunfight", "Fire Gun", new GunfightActivity_Base());
            GunCalibration = Res.DefineInteractionMode("calibGun", "Calibrate Gun", new GunfightActivity_Base(), (stringOrID)Resource.String.directive_calibrate_gun);
            //Res.InteractionModes.Remove("calibGun"); // We want it to exist, yes, but not to show up in the list when we run through it later.  This does that.
            SpellTeaching = Res.DefineInteractionMode("spellTeaching", "Train Spell", new SpellTrainingActivity());
            SpellCasting = Res.DefineInteractionMode("spellCasting", "Cast Spell", new SpellCastingActivity());
            MeleeTeaching = Res.DefineInteractionMode("meleeTeaching", "Train Melee", new MeleeTrainingActivity());
            Examine = Res.DefineInteractionMode("kit_examine", "Examine Objects", new BypassActivity(), (stringOrID)Resource.String.directive_examine);
            Multimeter = Res.DefineInteractionMode("kit_multimeter", "Multimeter", new BypassActivity(), (stringOrID)Resource.String.directive_multimeter);
            Wirecutters = Res.DefineInteractionMode("kit_wirecutters", "Wirecutters", new BypassActivity(), (stringOrID)Resource.String.directive_wirecutter);
            SolderingIron = Res.DefineInteractionMode("kit_solderingIron", "Soldering Iron", new BypassActivity(), (stringOrID)Resource.String.directive_soldering);
            LockPicking = Res.DefineInteractionMode("kit_lockpicking", "Pick Lock", new BypassActivity(), (stringOrID)Resource.String.directive_lock_picking);
            SafeCracking = Res.DefineInteractionMode("kit_safecracking", "Crack Safe", new BypassActivity(), (stringOrID)Resource.String.directive_safe_cracking);
            SecurityPanel = Res.DefineInteractionMode("kit_securitypanel", "Hack Electronics", new BypassActivity(), (stringOrID)Resource.String.directive_security_panel);
            //LockTraining = Res.DefineInteractionMode("lockTraining", "Train Lock/Vault", new Atropos.Locks.LockTrainingActivity());
        }
    }
}