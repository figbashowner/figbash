using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets
{

    public class ITreeItem
    {
        public int TreeIndex { get; set; }
        public string Name;

        public string FullPath;

        public bool Selected
        {
            get; set;
        }
        public bool SelectionCanChange
        {
            get;
            set;
        }
        public bool IsImport
        {
            get;
            set;
        }

    }
    
    [System.Serializable]
    public class Folder : ITreeItem
    {
        public Folder()
        {
            IsImport = false;
            SelectionCanChange = false;
            Selected = false;
        }
        public List<Folder> Subdirs = new List<Folder>();
        public List<StlFile> Files = new List<StlFile>();
    }
    
    [System.Serializable]
    public class StlFile : ITreeItem
    {
        public StlFile()
        {
            Selected = false;
            IsImport = false;
        }
        public Vector3 originalSize { get; set; }
        public string AfterClearsAppliedFullPath { get; set; }
        public bool IsClear => FullPath.ContainsInsensitive("clear");
        public string ClearToApply { get; set; }

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
    public class CutConfig
    {
        public string CutsFileFullPath = "";
        public string OutputFileFullPath = "";
        public double ScaleFactor = 1.0f;
    }

    [Serializable]
    public class ExportFile
    {
        public bool keep = false;
        public CutConfig[] cutConfigs = null;
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
