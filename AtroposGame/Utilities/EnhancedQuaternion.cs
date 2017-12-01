using Java.Util;
using System.Collections.Generic;
using Android.Util;
using Android.Runtime;

using System;
using System.Numerics;
using static System.Numerics.Vector2;
using static System.Numerics.Vector3;
using static System.Numerics.Quaternion;
using Android.App;
using Android.Content;
using System.Threading.Tasks;
using Android.Hardware;

namespace Atropos
{
    //[Serializable]
    //public struct Vector2
    //{
    //    public float X, Y;
    //    public Vector2(float x, float y)
    //    {
    //        X = x;
    //        Y = y;
    //    }

    //    public static implicit operator System.Numerics.Vector2(Vector2 source)
    //    {
    //        return new System.Numerics.Vector2(source.X, source.Y);
    //    }
    //    public static implicit operator Vector2(System.Numerics.Vector2 source)
    //    {
    //        return new Vector2(source.X, source.Y);
    //    }

    //    public float Length() { return (float)Math.Sqrt(X * X + Y * Y); }
    //    public float LengthSquared() { return X * X + Y * Y; }

    //    public static Vector2 UnitX = new Vector2(1f, 0f);
    //    public static Vector2 UnitY = new Vector2(0f, 1f);
    //    public static Vector2 Zero = new Vector2(0f, 0f);
    //}

    //[Serializable]
    //public struct Vector3
    //{
    //    public float X, Y, Z;
    //    public Vector3(float x, float y, float z)
    //    {
    //        X = x;
    //        Y = y;
    //        Z = z;
    //    }

    //    public static implicit operator System.Numerics.Vector3(Vector3 source)
    //    {
    //        return new System.Numerics.Vector3(source.X, source.Y, source.Z);
    //    }
    //    public static implicit operator Vector3(System.Numerics.Vector3 source)
    //    {
    //        return new Vector3(source.X, source.Y, source.Z);
    //    }

    //    public float Length() { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); }
    //    public float LengthSquared() { return X * X + Y * Y + Z * Z; }

    //    public static Vector3 UnitX = new Vector3(1f, 0f, 0f);
    //    public static Vector3 UnitY = new Vector3(0f, 1f, 0f);
    //    public static Vector3 UnitZ = new Vector3(0f, 0f, 1f);
    //    public static Vector3 Zero = new Vector3(0f, 0f, 0f);
    //}

    //[Serializable]
    //public struct Quaternion
    //{
    //    public float X, Y, Z, W;
    //    public Quaternion(float x, float y, float z, float w)
    //    {
    //        X = x;
    //        Y = y;
    //        Z = z;
    //        W = w;
    //    }

    //    public static implicit operator System.Numerics.Quaternion(Quaternion source)
    //    {
    //        return new System.Numerics.Quaternion(source.X, source.Y, source.Z, source.W);
    //    }
    //    public static implicit operator Quaternion(System.Numerics.Quaternion source)
    //    {
    //        return new Quaternion(source.X, source.Y, source.Z, source.W);
    //    }

    //    public static Quaternion Identity = System.Numerics.Quaternion.Identity;

    //    public static Quaternion operator *(Quaternion q1, Quaternion q2)
    //    {
    //        return (System.Numerics.Quaternion)q1 * (System.Numerics.Quaternion)q2;
    //    }

    //    public static Quaternion operator *(float scalar, Quaternion q)
    //    {
    //        return (System.Numerics.Quaternion)q * scalar;
    //    }

    //    public static Quaternion operator *(Quaternion q, float scalar)
    //    {
    //        return (System.Numerics.Quaternion)q * scalar;
    //    }
    //}

    public static class QuaternionExtensions
    {
        public const float radToDeg = (float)(180.0 / Math.PI);
        public const float degToRad = (float)(Math.PI / 180.0);

        public static Quaternion Inverse(this Quaternion source)
        {
            return Quaternion.Inverse(source);
        }

        public static Quaternion Conjugate(this Quaternion source)
        {
            return Quaternion.Conjugate(source);
        }

        public static Quaternion FollowedBy(this Quaternion first, Quaternion second)
        {
            return Quaternion.Concatenate(first, second);
        }

        public static Vector3 XYZ(this Quaternion sourceQuat)
        {
            return new Vector3(sourceQuat.X, sourceQuat.Y, sourceQuat.Z);
        }
        
        // Courtesy of https://blog.molecular-matters.com/2013/05/24/a-faster-quaternion-vector-multiplication/ - though commented out since I'm guessing Vector3.Transform will be at least as fast.
        public static Vector3 RotatedBy(this Vector3 sourceVector, Quaternion rotation)
        {
            //return Vector3.Transform(sourceVector, rotation);
            var qVec = rotation.XYZ();
            var t = 2.0f * Vector3.Cross(qVec, sourceVector);
            return sourceVector + (rotation.W * t) + Vector3.Cross(qVec, t);
        }

        public static float AngleTo(this Quaternion a, Quaternion b)
        {
            float f = Quaternion.Dot(a, b);
            return (float)Math.Acos(Math.Min(Math.Abs(f), 1f)) * 2f * radToDeg;
        }

        public static Quaternion QuaternionToGetTo(this Vector3 start, Vector3 end)
        {
            var tempVec = new Vector3();
            var dot = Vector3.Dot(start, end);
            var output = new Quaternion();

            if (dot < -0.99999999) // Vectors are opposite; any one-eighty quaternion will do, so use one perpendicular to the X axis or, if that's degenerate, the Y.
            {
                tempVec = Vector3.Cross(Vector3.UnitX, start);
                if (tempVec.Length() < 0.000001)
                    tempVec = Vector3.Cross(Vector3.UnitY, start);
                tempVec.Normalize();
                output = Quaternion.CreateFromAxisAngle(tempVec, (float)Math.PI);
            }
            else if (dot > 0.99999999) // Vectors are parallel; return the identity quaternion, plain and simple.
            {
                output = Quaternion.Identity;
            }
            else
            {
                tempVec = Vector3.Cross(start, end);
                output.X = tempVec.X;
                output.Y = tempVec.Y;
                output.Z = tempVec.Z;
                output.W = 1.0f + dot;
            }

            return output.Normalize();
        }

        public static Vector3 ToEulerAngles(this Quaternion source)
        {
            // Calculate Euler angles (roll, pitch, and yaw) from the unit quaternion.
            // NOTE - Have had to experimentally rearrange the order of these from where I found it; should be checked before other use.
            var yaw = Math.Atan2(2.0f * (source.W * source.Z + source.X * source.Y),
                            1.0f - 2.0f * (source.Y * source.Y + source.Z * source.Z));
            var roll = Math.Asin((2.0f * (source.W * source.Y - source.Z * source.X)).Clamp(-1, 1));
            var pitch = Math.Atan2(2.0f * (source.W * source.X + source.Y * source.Z),
                               1.0f - 2.0f * (source.X * source.X + source.Y * source.Y));
            return new Vector3((float)yaw, (float)pitch, (float)roll) * radToDeg;
        }

        public static Quaternion FromString(this Quaternion target, string inputString)
        {
            var vals = inputString.Trim('{', '}')
                                .Split(':', ' ');
            target.X = float.Parse(vals[1]);
            target.Y = float.Parse(vals[3]);
            target.Z = float.Parse(vals[5]);
            target.W = float.Parse(vals[7]);
            return target;
        }

        public static string ToStringFormatted(this Quaternion source, string formatString)
        {
            return $"{{X:{source.X.ToString(formatString)} Y:{source.Y.ToString(formatString)} Z:{source.Z.ToString(formatString)} W:{source.W.ToString(formatString)}}}";
        }

        public static double AsAngle(this Quaternion source)
        {
            return 2.0 * Math.Acos(source.W);
        }

        public static float Dot (this Vector3 first, Vector3 second)
        {
            return Vector3.Dot(first, second);
        }

        public static float Component(this Vector3 source, int axisNumber)
        {
            if (axisNumber == 0) return source.X;
            if (axisNumber == 1) return source.Y;
            if (axisNumber == 2) return source.Z;
            throw new ArgumentOutOfRangeException();
        }

        public static Vector3 SetComponent(this Vector3 source, int axisNumber, float value)
        {
            var result = new Vector3(source.X, source.Y, source.Z);
            if (axisNumber == 0) result.X = value;
            else if (axisNumber == 1) result.Y = value;
            else if (axisNumber == 2) result.Z = value;
            else throw new ArgumentOutOfRangeException();
            return result;
        }

        public static bool IsZero(this Quaternion source)
        {
            return source.LengthSquared() < 1e-20;
        }
    }

    /// <summary>
    /// Technically syntactic sugar on top of a simple quaternion (the inverse of the orientation it's based on),
    /// this should make it MUCH friendlier to work with.
    /// </summary>
    public class ReferenceFrame
    {
        //    public string Name { get; set; }
        //    public virtual Quaternion FrameShift { get; set; } // Is virtual (and necessarily thus a Property) so it can be overridden, e.g. by something that wants to retrieve this value on the fly.
        //    protected virtual Quaternion FrameShiftFromWorldSpace { get; set; }
        //    public ReferenceFrame Parent;
        //    public List<ReferenceFrame> Children;
        //    public volatile bool IsUpToDate = false;

        //    // World space: North, East, Up axes.
        //    public static ReferenceFrame WorldSpace = new ReferenceFrame("World", new Orientation { Rotation = Quaternion.Identity, Frame = null });
        //    // Gesture (or Player) space: Forward, Right, Up.  Generally set whenever we begin a gesture / gesture set.  THIS IS ALMOST ALL WE NEED!
        //    public static ReferenceFrame GestureSpace = new ReferenceFrame("Gesture", new Orientation { Rotation = Quaternion.Identity, Frame = WorldSpace });
        //    // Prop space: Pistol barrel, left-to-right of pistol, and bottom-to-top.  The prop itself will always be at the Identity orientation in PropSpace.
        //    public static ReferenceFrame PropSpace = new ReferenceFrame("Prop", new Orientation { Rotation = Quaternion.Identity, Frame = GestureSpace });
        //    // Device space: Standard device axes.  Device will always be at the Identity orientation in PropSpace.  Gyro and Accelerometer data are reported in THIS space.
        //    public static ReferenceFrame DeviceSpace = new ReferenceFrame("Device", new Orientation { Rotation = Quaternion.Identity, Frame = PropSpace });
        //    // Others can be created as necessary, but I *know* these will exist.

        //static ReferenceFrame()
        //{
        //    KnownSpaces = new Dictionary<string, ReferenceFrame>();
        //}

        //public ReferenceFrame(string name, Orientation sourceOrientation)
        //{
        //    FrameShifts.KnownSpaces = FrameShifts.KnownSpaces ?? new Dictionary<string, ReferenceFrame>();

        //    Name = name;
        //    FrameShift = sourceOrientation.Rotation.Inverse();
        //    Parent = sourceOrientation.Frame;
        //    Children = new List<ReferenceFrame>();
        //    if (Name == "World")
        //    {
        //        FrameShiftFromWorldSpace = Quaternion.Identity;
        //    }
        //    else
        //    {
        //        if (Parent == null) throw new NullReferenceException("Reference frame cannot be instantiated with a null parent frame.");
        //        Parent.Children.Add(this);

        //        FrameShiftFromWorldSpace = AccumulateFrameShifts();
        //    }
        //    if (FrameShifts.KnownSpaces.ContainsKey(Name))
        //    {
        //        Log.Warn("ReferenceFrame:ctor", $"Possible trouble... registering a reference frame named {Name} twice.  Not forbidden, but usually not done.");
        //    }
        //    else
        //    {
        //        FrameShifts.KnownSpaces.Add(Name, this);
        //    }
        //}

        //protected Quaternion AccumulateFrameShifts()
        //{
        //    var currentFrame = this;
        //    List<ReferenceFrame> chainToWorldSpace = new List<ReferenceFrame>();
        //    Quaternion totalFrameShift = Quaternion.Identity;

        //    while (currentFrame.Parent != null)
        //    {
        //        chainToWorldSpace.Add(currentFrame.Parent);
        //        currentFrame = currentFrame.Parent;
        //    }
        //    chainToWorldSpace.Reverse();

        //    foreach (var frame in chainToWorldSpace)
        //    {
        //        totalFrameShift = Quaternion.Concatenate(totalFrameShift, frame.FrameShift);
        //    }
        //    return totalFrameShift;
        //}

        //public void UpdateFromToValues()
        //{
        //    foreach (var other in FrameShifts.KnownSpaces.Values)
        //    {
        //        var netShift = other.FrameShiftFromWorldSpace
        //                            .Inverse()
        //                            .FollowedBy(this.FrameShiftFromWorldSpace);
        //        FrameShifts.FromTo.Add(other, this, netShift);
        //        FrameShifts.FromTo.Add(this, other, netShift.Inverse());
        //    }
        //    IsUpToDate = true;
        //}

        public static Quaternion CalibratePropOrientation(Quaternion PropPointedDown, Quaternion PropPointedForward)
        {
            // When the prop is pointed DOWN, the Y axis in Prop space will correspond to the -Z axis of World space (-pi/2 around the axis x, y, 0) and the Device will have orientation #1 (also WorldSpace).
            // When the prop is pointed FORWARD, the +Z axis in Prop space will match that of World space, and the Device will have orientation #2, having come pi/2 around that same axis - which is also the X axis in Prop space.
            // Therefore...
            // 
            // A) F (the Forward quaternion) = D (the Down one) * Q_3, Q_3 being the rotation to tip it up ninety degrees about the prop's tip axis Q_3.xyz.
            // B)  Back-multiplying by D' (conjugate/inverse), we get Q_3 = F * D'.
            // C)  The vector portion of Q_3 (Q_1.xyz) is our prop X axis in world space (both in orientation D and in orientation F), facing as we do now.
            // D) Thus we can get Q_2, the rotation (about either +Z or -Z, more or less, but it's just as easy to work it out), which rotates East (1, 0, 0) into Q_1.xyz.  
            //          Algorithm here from http://stackoverflow.com/a/1171995.
            // E) Q_2 thus turns world space into prop space and is the FromWorldSpace shift of PropSpace (again /now/ only).
            // F) F, then, is the product of (first) Q_1, the rotation of the phone within the prop, then Q_2.  So, to undo it, back-multiply by Q_2';
            //          F * Q_2' = Q_1 * (Q_2 * Q_2') = Q_1.
            // G) Lastly, later on, given some vector v in device coordinates (accel, gyro... NOT orientation, that's still in world coords), 
            //          the same vector in prop space will be Q_1 * v * Q_1'.


            Quaternion D = PropPointedDown;
            Quaternion F = PropPointedForward;
            Quaternion Q_3 = (Quaternion)D.Inverse() * F;

            Quaternion Q_2 = Vector3.UnitX.QuaternionToGetTo(Q_3.XYZ());

            Quaternion Q_1 = (Quaternion)Q_2.Inverse() * F;
            return Q_1;

        }
    }

    //public static class FrameShifts
    //{
    //    public static Dictionary<string, ReferenceFrame> KnownSpaces = new Dictionary<string, ReferenceFrame>();
    //    public static DoubleDictionary<ReferenceFrame, ReferenceFrame, Quaternion> FromTo = new DoubleDictionary<ReferenceFrame, ReferenceFrame, Quaternion>();
    //}

    //public class Orientation // A wrapper over a quaternion plus the reference frame it's a rotation/orientation IN.
    //{
    //    public Quaternion Rotation = Quaternion.Identity;
    //    public ReferenceFrame Frame;

    //    public static Orientation SetAs(Quaternion rotationInWorldSpace, ReferenceFrame desiredFrame)
    //    {
    //        var o = new Orientation { Rotation = rotationInWorldSpace, Frame = ReferenceFrame.WorldSpace };
    //        return o.As(desiredFrame);
    //    }

    //    public Quaternion In(ReferenceFrame otherFrame)
    //    {
    //        if (otherFrame == Frame) return Rotation;
    //        return Rotation * FrameShifts.FromTo[Frame, otherFrame];
    //    }

    //    public Orientation As(ReferenceFrame newFrame)
    //    {
    //        if (newFrame == Frame) return this;
    //        return new Orientation { Rotation = this.In(newFrame), Frame = newFrame };
    //    }
    //}
}