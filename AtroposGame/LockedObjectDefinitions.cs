
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
using PerpetualEngine.Storage;
using Android.Media;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using MiscUtil;

namespace Atropos.Locks
{
    public class Tumbler
    {
        //public Quaternion Orientation { get; set; }
        //public float AngleTo(Quaternion other) { return Orientation.AngleTo(other); }
        public double Angle { get; set; }
        public int Direction { get; set; } // Constrained to +1 or -1, sign of travel direction req'd.
        public Tumbler NextTumbler { get; set; }
        //public double SteadinessScoreWhenDefined { get; set; }
        //public double OrientationSigma { get; set; }
        //public double MaxRotationRate { get; set; } // Only relevant for key locks, not safes

        //public Tumbler(Quaternion orientation, double maxRotationRate, double steadinessBase, double orientationSigma)
        public Tumbler(double angle, int? direction = null)
        {
            //Orientation = orientation;
            Angle = angle;
            Direction = Math.Sign(direction ?? angle);
            //MaxRotationRate = maxRotationRate;
            //SteadinessScoreWhenDefined = steadinessBase;
            //OrientationSigma = orientationSigma;
            NextTumbler = EndOfLock;
        }

        public override string ToString()
        {
            //var valuesArray = new double[] { Orientation.X, Orientation.Y, Orientation.Z, Orientation.W, MaxRotationRate, SteadinessScoreWhenDefined, OrientationSigma };
            //var stringsArray = valuesArray.Select(d => d.ToString("f4")).ToArray();
            //return stringsArray.Join(",");
            return $"{Angle:f4},{Direction}";
        }

        public static Tumbler FromString(string inputStr)
        {
            var strArray = inputStr.Split(',');
            return new Tumbler(double.Parse(strArray[0]), int.Parse(strArray[1]));
            //try
            //{
            //    var inputArray = inputStr.Trim('(', ')').Split(',');
            //    var floatsArray = inputArray.Select(inp => float.Parse(inp)).ToArray();
            //    //var floatsArray = new float[7];
            //    //foreach (int i in Enumerable.Range(0, 7)) floatsArray[i] = float.Parse(inputArray[i]);
                
            //    //var Orientation = new Quaternion(floatsArray[0], floatsArray[1], floatsArray[2], floatsArray[3]);
            //    //var tumbler = new Tumbler(Orientation, floatsArray[4], floatsArray[5], floatsArray[6]);
            //    var tumbler = new Tumbler(floatsArray[0], floatsArray[1], floatsArray[2], floatsArray[3]);
            //    return tumbler;
            //}
            //catch (Exception)
            //{
            //    Log.Debug("LockedObjects", $"Problem reading tumbler from string.  String was < {inputStr} >.");
            //    return EndOfLock;
            //}
        }

        //public static Tumbler EndOfLock = new Tumbler(Quaternion.Identity, 0.0, 0.0, 0.0);
        public static Tumbler EndOfLock = new Tumbler(double.NaN, 0); // Functions as a signal; its details are never used.
        public static Tumbler ResetToZero = new Tumbler(0.0);
        public static Tumbler PinMoveTarget = new Tumbler(0.0);
        public static Tumbler None = new Tumbler(double.NegativeInfinity);
    }

    public class Lock
    {
        public static Lock Current { get; set; } = None;

        public enum LType
        {
            Unknown,
            KeyLock,
            SafeDial
        }

        public string LockName { get; set; }
        public LType LockType { get; set; }
        public Quaternion ZeroStance { get; set; } = Quaternion.Identity;
        public float AngleTo(Quaternion orientation) { return ZeroStance.AngleTo(orientation); }
        public List<Tumbler> Tumblers { get; set; }

        public double OffAxisMaxDuringFindingPhase { get; set; } = 45.0;
        public double OffHorizontalMaxDuringFindingPhase { get; set; } = 30.0;

        public int NumberOfAttempts { get; set; } = 0;
        public event EventHandler<EventArgs<int>> OnLockOpened;
        public void AnnounceLockOpened() { OnLockOpened.Raise(NumberOfAttempts); }

        // Relevant only for safes
        public double AngleLeftSittingAt = 0.0;
        public double DegreesBetweenClicks { get; set; } = 3.6; 

        // Relevant only for key locks
        public double AngleMinForLiftingPhase { get; set; } = 20.0;
        public double AngleMaxForLiftingPhase { get; set; } = 40.0;
        public double RotationAccuracyRequired { get; set; } = 5.0; // Degrees
        public double MaxRotationRateInLiftingPhase { get; set; } = 75.0; // Degrees per second

        public Lock(string name = "NoLock", LType type = LType.Unknown, Quaternion? zeroStance = null, string succResult = null, string failResult = null)
        {
            LockName = name;
            LockType = type;
            Tumblers = new List<Tumbler>();
            ZeroStance = zeroStance ?? Quaternion.Identity;
            if (LockType == LType.SafeDial) RotationAccuracyRequired = DegreesBetweenClicks - 0.01;
        }

        public void AddTumbler(Tumbler newTumbler)
        {
            if (Tumblers.Count > 0) Tumblers.Last().NextTumbler = newTumbler;
            Tumblers.Add(newTumbler);
        }

        public void UndoAddTumbler()
        {
            Tumblers.RemoveAt(Tumblers.Count - 1);
            if (Tumblers.Count > 0) Tumblers.Last().NextTumbler = Tumbler.EndOfLock;
        }

        public override string ToString()
        {
            return $"{LockName};{LockType};" + Tumblers.Select(g => g.ToString()).Join(":", "") + $";{ZeroStance.ToString()}";
        }

        public static Lock FromString(string inputStr)
        {
            var subStrings = inputStr.Split(';');

            Lock resultLock = new Lock(subStrings[0], 
                (LType)Enum.Parse(typeof(LType), subStrings[1], true), 
                new Quaternion().Parse(subStrings[3]), 
                subStrings[4], 
                subStrings[5]);

            var TumblerStrings = subStrings[2].Split(':');
            foreach (var TumblerStr in TumblerStrings)
            {
                resultLock.AddTumbler(Tumbler.FromString(TumblerStr));
            }
            return resultLock;
        }

        public static Lock None = new Lock();
        public static Lock Special = new Lock();
        public static Lock TestLock = new Lock("Test Lock", LType.KeyLock);
        public static Lock TestSafe = new Lock("Test Safe", LType.SafeDial);
        static Lock()
        {
            TestSafe.AddTumbler(new Tumbler(13 * TestSafe.DegreesBetweenClicks));
            TestSafe.AddTumbler(new Tumbler(-8 * TestSafe.DegreesBetweenClicks));
            TestSafe.AddTumbler(new Tumbler(22 * TestSafe.DegreesBetweenClicks));
            TestLock.AddTumbler(new Tumbler(-45));
            TestLock.AddTumbler(new Tumbler(60));
        }

        public static Lock SafeByCombination(string name, params int[] combination)
        {
            var result = new Lock(name, LType.SafeDial);
            foreach (var digit in combination)
            {
                result.AddTumbler(new Tumbler(digit * result.DegreesBetweenClicks));
            }
            return result;
        }

        public static Lock LockByAngles(string name, params double[] tumblerAngles)
        {
            var result = new Lock(name, LType.KeyLock);
            foreach (var angle in tumblerAngles)
            {
                result.AddTumbler(new Tumbler(angle));
            }
            return result;
        }
    }

    public static class MasterLockLibrary
    {

        private const string persistentLibraryName = "MasterLockLib";
        private const string masterIndexName = "MasterLockIndex";
        private static SimpleStorage persistentLibrary { get; set; }
        public static string[] LockNames { get; set; }

        public static void LoadAll()
        {
            persistentLibrary = SimpleStorage.EditGroup(persistentLibraryName);
            LockNames = persistentLibrary.Get<string[]>(masterIndexName);
            if (LockNames == null) LockNames = new string[0];
            if (LockNames.Length == 0) AddLock(Lock.None); // Weird errors get thrown up with an empty master index.

            // Testing mode only
            AddLock(Lock.TestLock);
            AddLock(Lock.TestSafe);
        }

        public static Lock Get(string Lockname)
        {
            if (LockNames.Contains(Lockname) && persistentLibrary.Get(Lockname) != null)
            {
                return Lock.FromString(persistentLibrary.Get(Lockname));
            }
            else
            {
                Log.Error("LockLibrary", $"Could not find Lock '{Lockname}' in master library.");
                return new Lock();
            }
        }

        public static void AddLock(Lock newLock)
        {
            persistentLibrary.Put(newLock.LockName, newLock.ToString());
            if (!LockNames?.Contains(newLock.LockName) ?? false) LockNames = LockNames.Append(newLock.LockName).ToArray();
            persistentLibrary.Put(masterIndexName, LockNames);
        }

        public static void Erase(string Lockname)
        {
            persistentLibrary.Delete(Lockname);
            LockNames = LockNames.Where(n => n != Lockname).ToArray();
            persistentLibrary.Put(masterIndexName, LockNames);
        }
    }
}