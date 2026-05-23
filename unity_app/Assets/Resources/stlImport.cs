using Assets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static stlImport;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class stlImport : MonoBehaviour
{
    GameObject composite, left, right;




    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public static float scale = 0.05f;
   

    public static void ApplyClearJson(StlFile stlFile, Action<string> logCallback)
    {
        var tempFile = $"{uiEvents.tempClearsPath}/{Path.GetFileName(stlFile.FullPath)}_minus_{Path.GetFileName(stlFile.ClearToApply)}.json";
        Utils.UpdatePositionRelativeToOriginalSize(stlFile);
        var exportFile = new ExportFile() { children = new List<BareMinimumStlFile>() { stlFile.Export() }.ToArray() };
        var output = JsonUtility.ToJson(exportFile);
        File.WriteAllText(tempFile, output);
        executeApplyClearJsonScript($"\"{tempFile}\" \"{stlFile.AfterClearsAppliedFullPath}\"", logCallback);
    }
    public static void ApplyClear(string baseFullPath, string clearFullPath, string outputFullPath, Action<string> logCallback)
    {
        executeApplyClearScript($"\"{baseFullPath}\" \"{clearFullPath}\" \"{outputFullPath}\"", logCallback);
    }

    public static void CreateFinalFigure(string jsonExportFile, string outputFile, Action<string> logCallback)
    {
        executeCombineAllJsonScript($"\"{jsonExportFile}\" \"{outputFile}\"", logCallback);
    }
//    private static List<string> hiddenFiles = new List<string>() { "base.stl", "cuts_0.16.stl", "cuts_0.20.stl", "cuts_0.25.stl", "cuts_0.30.stl", "cuts_0.35.stl" };

    public static void ExportStlsFromBlender(string outputFullPath, Action<string> logCallback)
    {
        executeExportAllScript($"\"{Application.streamingAssetsPath}\" \"{outputFullPath}\"", logCallback);
        foreach (var file in DataManager.hiddenFiles)
        {
            File.Copy(Path.Combine(Application.streamingAssetsPath, file), Path.Combine(outputFullPath, file), true);
        }
    }
    private static void executeApplyClearScript(string args, Action<string> logCallback)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "figureTool.exe");
        executeProcess(fullPath, "applyClear " + args, logCallback);
    }
    private static void executeApplyClearJsonScript(string args, Action<string> logCallback)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "figureTool.exe");
        executeProcess(fullPath, "applyClearJson " + args, logCallback);
    }

    private static void executeCombineAllScript(string args, Action<string> logCallback)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "figureTool.exe");
        executeProcess(fullPath, "combineAll " + args, logCallback);
    }

    private static void executeCombineAllJsonScript(string args, Action<string> logCallback)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "figureTool.exe");
        executeProcess(fullPath, "combineAllJson " + args, logCallback);
    }
    private static void executeExportAllScript(string args, Action<string> logCallback)
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, "figureTool.exe");
        executeProcess(fullPath, "exportAll " + args, logCallback);
    }
    private static void executeProcess(string fullPath, string args, Action<string> logCallback)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo(fullPath);
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.Arguments = args;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        try
        {
            UnityEngine.Debug.Log($"starting process {fullPath} {args}");
            logCallback($"starting process {fullPath} {args}");
            var p = new Process();
            p.StartInfo = startInfo;
            StringBuilder b = new StringBuilder();
            DataReceivedEventHandler dataHandler = new DataReceivedEventHandler((sender, msg) =>
            {
                logCallback(msg.Data);
                b.AppendLine(msg.Data);
            });
            p.OutputDataReceived += dataHandler;
            p.ErrorDataReceived += dataHandler;
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            p.Close();
            logCallback($"process finished");
            UnityEngine.Debug.Log(b.ToString());
        }
        catch (Exception e) 
        {
            logCallback(e.ToString());
            UnityEngine.Debug.Log(e.ToString());
            Console.WriteLine(e.ToString());
        }
        
    }

    public static void Unload(string path)
    {
        var g = GameObject.Find(path);
        g?.SetActive(false);
        Destroy(g);
    }
    public static void ClearLoadedFiles()
    {
        object[] obj = GameObject.FindGameObjectsWithTag("stl");
        foreach (object o in obj)
        {
            GameObject g = (GameObject)o;
            if (g.name.StartsWith("C:\\"))
                Unload(g.name);
        }
    }
    public static GameObject LoadOne(StlFile stl, Vector3? maxSize = null)
    {
        var g = GameObject.Find(stl.UiKey);
        if (g == null || ReferenceEquals(g, null) || g.IsDestroyed() || g.activeSelf == false)
        {
            var mesh = Parabox.Stl.Importer.Import(
                stl.AfterClearsAppliedFullPath ?? stl.FullPath,
                Parabox.Stl.CoordinateSpace.Right,
                Parabox.Stl.UpAxis.Y,
                false,
                UnityEngine.Rendering.IndexFormat.UInt32
                ).First();
            var result = new GameObject();
            result.name = stl.UiKey;
            result.tag = "stl";
            var myScale = scale;
            result.AddComponent<MeshFilter>().sharedMesh = mesh;
            result.GetComponent<Transform>().localScale = new Vector3(myScale, myScale, myScale);
            result.AddComponent<MeshRenderer>().sharedMaterials = new List<Material>() { new Material(Shader.Find("Diffuse")) }.ToArray();
            if (stl.ClearToApply == null || stl.ClearToApply == String.Empty)
                stl.originalSize = mesh.bounds.size * scale;
            if (maxSize != null)
            {
                //Intentionally skipping the thickness of the figure, since that is the thinnest dimension
                for (int i=1; i < 3; i++)
                {
                    var currentSize = result.GetComponent<Renderer>().bounds.size;
                    var targetSize = maxSize.Value[i] / 2;
                    if (currentSize[i] > targetSize)
                    {
                        var currentScale = result.GetComponent<Transform>().localScale[0];
                        var newScale = (currentScale / currentSize[i]) * targetSize;
                        result.GetComponent<Transform>().localScale = new Vector3(newScale, newScale, newScale);
                    }
                }

            }
            return result;
        }
        else
        {
            return g;
        }
    }
}
