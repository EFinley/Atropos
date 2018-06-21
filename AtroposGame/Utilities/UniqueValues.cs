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
    public static class UniqueValues
    {
        public static char ACKchar = (char)6;
        public static char START = (char)2; // Not actually used, except as a placeholder value, but that could change.
        public static char LF = (char)10;
        public static char ENDTRANSBLOCK = (char)23;
        public static char GROUP_SEPARATOR = (char)29;
        public static char RECORD_SEPARATOR = (char)30;
        public static char NEXT = GROUP_SEPARATOR;
        public static char[] onNEXT = new char[] { NEXT }; // Because Split(onThis, numGroups) requires a char array as onThis.
        public static char END = LF;
    }
}