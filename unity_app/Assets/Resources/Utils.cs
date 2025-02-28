using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets
{
    internal class Utils
    {
        public static float makePercent (float baseSize, float value, int digits = 1)
        {
            var val = MathF.Round((value / baseSize) * 100, digits);
            if (float.IsInfinity(val) || float.IsNaN(val))
                throw new Exception("something went wrong with the percentages...");
            return val;
        }
        public static void UpdatePositionRelativeToOriginalSize(StlFile stl)
        {
            if (stl.originalSize == Vector3.zero || stl.Transforms == null)
            { 
                return; 
            } 
            stl.Transforms.PositionRelativeToOriginalSize = new float[] 
            { 
                makePercent(stl.originalSize[0], reversePercentage(uiEvents.BaseSize[0], stl.Transforms.Position[0]), 5),
                makePercent(stl.originalSize[1], reversePercentage(uiEvents.BaseSize[1], stl.Transforms.Position[1]), 5),
                makePercent(stl.originalSize[2], reversePercentage(uiEvents.BaseSize[2], stl.Transforms.Position[2]), 5)
            };
        }
        public static float reversePercentage(float baseSize, float value)
        {
            return (value / 100) * baseSize;
        }
        public static float GetEntryValue(TextField t)
        {
            float v = 0f;
            float.TryParse(t.value, out v);
            return v;
        }

    }
}
