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
        public static string GetUiSidecarName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (fileName.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.GetFileName(fileName);

            if (!fileName.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
                return null;

            return System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(fileName), ".ui.stl");
        }

        public static string GetUiSidecarPath(string stlPath)
        {
            if (string.IsNullOrWhiteSpace(stlPath))
                return null;

            if (stlPath.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                return stlPath;

            return System.IO.Path.ChangeExtension(stlPath, ".ui.stl");
        }

        public static void PairUiSidecars(Folder folder)
        {
            if (folder == null)
                return;

            if (folder.Subdirs != null)
            {
                foreach (var subdir in folder.Subdirs)
                {
                    PairUiSidecars(subdir);
                }
            }

            if (folder.Files == null || folder.Files.Count == 0)
                return;

            var fileNames = folder.Files
                .Where(file => file != null && !string.IsNullOrWhiteSpace(file.Name))
                .ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var file in folder.Files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.Name))
                    continue;

                if (file.Name.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(file.UiName))
                    continue;

                var uiName = GetUiSidecarName(file.Name);
                if (!string.IsNullOrWhiteSpace(uiName) && fileNames.ContainsKey(uiName))
                {
                    file.UiName = uiName;
                    continue;
                }

                var uiPath = GetUiSidecarPath(file.FullPath);
                if (!string.IsNullOrWhiteSpace(uiPath)
                    && System.IO.Path.IsPathRooted(file.FullPath)
                    && System.IO.File.Exists(uiPath))
                {
                    file.UiName = System.IO.Path.GetFileName(uiPath);
                }
            }
        }

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
    }
}
