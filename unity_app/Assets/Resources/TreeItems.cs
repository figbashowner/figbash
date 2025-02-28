using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets
{

    public interface ITreeItem
    {
        public int TreeIndex { get; set; }
        public string Name
        {
            get;
        }

        public string FullPath
        {
            get;
        }

        public bool Selected
        {
            get; set;
        }
        public bool SelectionCanChange
        {
            get;
        }
        public bool IsImport
        {
            get;
            set;
        }

    }

    public class Folder : ITreeItem
    {
        public int TreeIndex { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool Selected { get; set; }
        public bool SelectionCanChange { get => false; }
        public bool IsImport { get => false; set { } }
    }

    public class StlFile : ITreeItem
    {
        public int TreeIndex { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public Vector3 originalSize { get; set; }
        public string AfterClearsAppliedFullPath { get; set; }
        public bool IsClear => FullPath.ContainsInsensitive("clear");
        public bool Selected { get; set; } = false;
        public bool SelectionCanChange { get; set; }
        public string ClearToApply { get; set; }
        public bool IsImport { get; set; } = false;
        public Transforms Transforms { get; set; }

        public Guid Guid = Guid.NewGuid();
        public string UiKey {
            get 
            {
                if (IsImport)
                    return Guid.ToString();
                else
                    return FullPath + ClearToApply;
            }
        }


        public BareMinimumStlFile Export()
        {
            if (IsImport && (Transforms?.PositionRelativeToOriginalSize?.Length == null || Transforms?.PositionRelativeToOriginalSize?.Length == 0))
                throw new Exception("Import files need to have a PositionRelativeToOriginalSize set");
            return new BareMinimumStlFile()
            {
                FullPath = FullPath,
                OriginalSize = originalSize,
                ClearToApplyFullPath = ClearToApply,
                transforms = Transforms,
                IsImport = IsImport
            };
        }
    }

    [Serializable]
    public class ExportFile
    {
        public bool keep = false;
        public string CutsFileFullPath = "";
        public double ScaleFactor = 1.0f;
        public BareMinimumStlFile[] children;
    }

    [Serializable]
    public class Transforms
    {
        internal static Transforms zero = new Transforms() {  Rotations = new float[] { 0,0,0}, Position = new float[] {0,0,0 }, Scale = new float[] { 100, 100, 100 } };
        public bool UniformScale = true;
        //A 3-number array of rotations from 0 to 360
        //Axes are: Front-to-Back, Around Neck, Around Arms
        public float[] Rotations;
        //A 3-number array of numbers interpreted as percentages of the original object's size
        //Axes are: Front-to-Back, Around Neck, Around Arms
        public float[] Scale;
        //A 3-number array of numbers interpreted as percentages of the base.stl size
        //Axes are: Front-to-Back, Around Neck, Around Arms
        public float[] Position;
        //A 3-number array of numbers interpreted as percentages of the imported object's original size
        //Axes are: Front-to-Back, Around Neck, Around Arms
        public float[] PositionRelativeToOriginalSize;
    }

    [Serializable]
    public class BareMinimumStlFile
    {
        public string FullPath;
        public string ClearToApplyFullPath;
        public Transforms transforms;
        public bool IsImport = false;

        public Vector3 OriginalSize;
    }
}
