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
        var exportFile = new ExportFile() { children = new List<BareMinimumStlFile>() { stlFile.Export(preferUiPath: true) }.ToArray() };
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

    public static void CopyBundledStlsFromStreamingAssets(string outputFullPath, Action<string> logCallback)
    {
        var sourceRoot = Application.streamingAssetsPath;
        if (string.IsNullOrWhiteSpace(sourceRoot) || Directory.Exists(sourceRoot) == false)
            throw new DirectoryNotFoundException($"StreamingAssets folder not found: {sourceRoot}");

        Directory.CreateDirectory(outputFullPath);
        CopyBundledStlFiles(sourceRoot, outputFullPath, logCallback);
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
    private static void CopyBundledStlFiles(string sourceRoot, string destinationRoot, Action<string> logCallback)
    {
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            if (!ShouldCopyFile(file, destinationPath))
                continue;

            File.Copy(file, destinationPath, true);
            logCallback?.Invoke($"copying {relativePath}");
        }
    }

    private static bool ShouldCopyFile(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            return false;

        var sourceInfo = new FileInfo(sourcePath);
        if (!sourceInfo.Exists)
            return false;

        var destinationInfo = new FileInfo(destinationPath);
        if (!destinationInfo.Exists)
            return true;

        if (sourceInfo.Length != destinationInfo.Length)
            return true;

        return sourceInfo.LastWriteTimeUtc > destinationInfo.LastWriteTimeUtc;
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
        if (string.IsNullOrWhiteSpace(path))
            return;

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
        var loadKeyPath = ResolveLoadPath(stl);
        stl.LoadedPath = loadKeyPath;

        var sceneKey = stl.UiKey;
        stl.SceneKey = sceneKey;

        var g = GameObject.Find(sceneKey);
        if (g == null || ReferenceEquals(g, null) || g.IsDestroyed() || g.activeSelf == false)
        {
            var loadPath = stl.AfterClearsAppliedFullPath;
            if (!string.IsNullOrWhiteSpace(loadPath) && File.Exists(loadPath))
            {
                var afterClearUiPath = stlImport.GetUiSidecarPath(loadPath);
                if (!string.IsNullOrWhiteSpace(afterClearUiPath) && File.Exists(afterClearUiPath))
                    loadPath = afterClearUiPath;
            }
            else 
            {
                UnityEngine.Debug.LogWarning($"Loading object uiPath {stl.UiPath}");
                var uiPath = stl.UiPath;
                if (!string.IsNullOrWhiteSpace(uiPath) && File.Exists(uiPath))
                    loadPath = uiPath;
            }

            if (string.IsNullOrWhiteSpace(loadPath) || File.Exists(loadPath) == false)
            {
                loadPath = stl.FullPath;
            }
            UnityEngine.Debug.LogWarning($"Loading object with path {loadPath}");

            var mesh = Parabox.Stl.Importer.Import(
                loadPath,
                Parabox.Stl.CoordinateSpace.Right,
                Parabox.Stl.UpAxis.Y,
                false,
                UnityEngine.Rendering.IndexFormat.UInt32
                ).First();
            var result = new GameObject();
            result.name = sceneKey;
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

    private static string ResolveLoadPath(StlFile stl)
    {
        if (stl == null)
            return null;

        if (stl.IsImport)
        {
            var importUiPath = GetUiSidecarPath(stl.FullPath);
            if (!string.IsNullOrWhiteSpace(importUiPath) && File.Exists(importUiPath))
                return importUiPath;

            return stl.FullPath;
        }

        if (!string.IsNullOrWhiteSpace(stl.AfterClearsAppliedFullPath) && File.Exists(stl.AfterClearsAppliedFullPath))
        {
            var afterClearUiPath = GetUiSidecarPath(stl.AfterClearsAppliedFullPath);
            if (!string.IsNullOrWhiteSpace(afterClearUiPath) && File.Exists(afterClearUiPath))
                return afterClearUiPath;

            return stl.AfterClearsAppliedFullPath;
        }

        var uiPath = stl.UiPath;
        if (!string.IsNullOrWhiteSpace(uiPath) && File.Exists(uiPath))
            return uiPath;

        return stl.FullPath;
    }

    private static string GetUiSidecarPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
            return path;

        return Path.ChangeExtension(path, ".ui.stl");
    }
}
