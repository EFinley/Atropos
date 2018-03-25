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
using Android.Graphics;

using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Math;
using Accord.Statistics.Kernels;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Numerics;

using Vector3 = System.Numerics.Vector3;
using System.Runtime.Serialization;
using MiscUtil;

using Atropos.DataStructures;

namespace Atropos.Machine_Learning
{
    public static class CrosslinkAxes
    {
        public static double[] Apply<T>(double[] inputs) where T : struct
        {
            return CrosslinkAxesImpl<T>.Apply(inputs);
        }

        public static Func<double[], double[]> None
        {
            get { return t => t; }
        }

        private static class CrosslinkAxesImpl<T>
        where T : struct
        {
            public static Func<double[], double[]> _apply;
            public static double[] Apply(double[] inputs)
            {
                if (inputs.Any(i => i < 0))
                    throw new ArgumentOutOfRangeException("CrosslinkAxes: supplied values (which are supposed to be standard deviations) cannot be negative!");
                return _apply?.Invoke(inputs);
            }

            static CrosslinkAxesImpl()
            {
                Type typeT = typeof(T);
                Type wrappedType = Datapoint.From(default(T)).GetType();

                // First (and lowest-level of recursion) case: if T is not something which implements IDatapoint at all, 
                // hopefully it's a basic type like a scalar or the like; for all such, return the highest single scaling value, in all slots.
                if (!typeT.Implements<IDatapoint>())
                {
                    var dimensions = wrappedType.GetStaticProperty<int>("Dimensions");
                    _apply = (double[] inputs) =>
                    {
                        return Enumerable
                            .Repeat(inputs.Max(), dimensions)
                            .ToList().ToArray(); // Stupid frickin' portable Accord.Math with busted ToArray...
                    };
                    return;
                }

                // If it does implement IDatapoint, then (by construction) it has at least one generic argument, possibly several.
                var _applyFuncs = new List<Func<double[], double[]>>();
                var numberToTake = new List<int>();
                var numberToSkip = new List<int>() { 0 };
                foreach (Type t in typeT.GetGenericArguments())
                {
                    // How many dimensions does this one have?
                    int nToTake;
                    if (t.Implements<IDatapoint>())
                    {
                        nToTake = t.GetStaticProperty<int>("Dimensions");
                    }
                    else
                    {
                        var wrapped_t = typeof(Datapoint<>).MakeGenericType(t);
                        nToTake = wrapped_t.GetStaticProperty<int>("Dimensions");
                    }
                    numberToTake.Add(nToTake);
                    numberToSkip.Add(numberToSkip.Last() + nToTake);

                    // And what is *its* Apply() function?
                    var crosslinker = typeof(CrosslinkAxesImpl<>).MakeGenericType(t);
                    //_applyFuncs.Add((double[] inputs) =>
                    //{
                    //    return crosslinker.InvokeStaticMethod<double[]>("Apply", inputs);
                    //});
                    var cFunc = (Func<double[], double[]>)crosslinker.GetField("_apply").GetValue(null);
                    _applyFuncs.Add(cFunc);
                }

                _apply = (inputs) =>
                {
                    var outputs = new List<double>();
                    foreach (int i in Enumerable.Range(0, typeT.GetGenericArguments().Length))
                    {
                        var inputsSubset = inputs
                            .Skip(numberToSkip[i])
                            .Take(numberToTake[i])
                            .ToList().ToArray(); // Stupid Accord.Math issues again...


                        var _func = _applyFuncs[i];
                        outputs.AddRange(_func.Invoke(inputsSubset));
                    }
                    return outputs.ToArray();
                };
            }
        }
    }
}