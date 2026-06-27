using Assets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
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

    public static string CopyImportedStlToDataFolder(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return sourcePath;

        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedSourcePath))
            return normalizedSourcePath;

        if (string.IsNullOrWhiteSpace(uiEvents.folderRoot))
            throw new InvalidOperationException("The data folder has not been initialized yet.");

        var importsRoot = Path.Combine(uiEvents.folderRoot, "imports");
        Directory.CreateDirectory(importsRoot);

        var destinationPath = GetImportedDestinationPath(importsRoot, normalizedSourcePath);
        CopyFileIfNeeded(normalizedSourcePath, destinationPath);

        var sourceUiPath = GetUiSidecarPath(normalizedSourcePath);
        if (!string.IsNullOrWhiteSpace(sourceUiPath) && File.Exists(sourceUiPath))
        {
            var destinationUiPath = GetUiSidecarPath(destinationPath);
            CopyFileIfNeeded(sourceUiPath, destinationUiPath);
        }

        return destinationPath;
    }

    public static bool TryLoadImportState(string importPath, out BareMinimumStlFile state)
    {
        state = null;

        var statePath = GetImportStatePath(importPath);
        if (string.IsNullOrWhiteSpace(statePath) || !File.Exists(statePath))
            return false;

        try
        {
            var exportFile = JsonUtility.FromJson<ExportFile>(File.ReadAllText(statePath));
            var child = exportFile?.children?.FirstOrDefault(item => item != null);
            if (child == null)
                return false;

            ResolvePortableChild(child);
            state = child;
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning(ex);
            return false;
        }
    }

    public static void SaveImportState(StlFile stlFile)
    {
        if (stlFile == null || !stlFile.IsImport || stlFile.Transforms == null)
            return;

        try
        {
            var statePath = GetImportStatePath(stlFile.FullPath);
            if (string.IsNullOrWhiteSpace(statePath))
                return;

            var exportFile = new ExportFile()
            {
                children = new[] { CreatePortableImportChild(stlFile) }
            };

            var stateDirectory = Path.GetDirectoryName(statePath);
            if (!string.IsNullOrWhiteSpace(stateDirectory))
                Directory.CreateDirectory(stateDirectory);

            File.WriteAllText(statePath, JsonUtility.ToJson(exportFile));
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning(ex);
        }
    }

    private static string GetImportStatePath(string importPath)
    {
        if (string.IsNullOrWhiteSpace(importPath))
            return string.Empty;

        return Path.ChangeExtension(Path.GetFullPath(importPath), ".json");
    }

    private static BareMinimumStlFile CreatePortableImportChild(StlFile stlFile)
    {
        var child = stlFile.Export(preferUiPath: true);
        child.FullPath = Utils.MakePortablePath(child.FullPath, uiEvents.folderRoot);
        child.ClearToApplyFullPath = Utils.MakePortablePath(child.ClearToApplyFullPath, uiEvents.folderRoot);
        child.RepositorySource = Utils.MakePortableRepositorySource(child.RepositorySource, uiEvents.folderRoot);
        child.ClearRepositorySource = Utils.MakePortableRepositorySource(child.ClearRepositorySource, uiEvents.folderRoot);
        return child;
    }

    private static void ResolvePortableChild(BareMinimumStlFile child)
    {
        if (child == null)
            return;

        child.FullPath = Utils.ResolvePortablePath(child.FullPath, uiEvents.folderRoot);
        child.ClearToApplyFullPath = Utils.ResolvePortablePath(child.ClearToApplyFullPath, uiEvents.folderRoot);
        child.RepositorySource = Utils.ResolvePortableRepositorySource(child.RepositorySource, uiEvents.folderRoot);
        child.ClearRepositorySource = Utils.ResolvePortableRepositorySource(child.ClearRepositorySource, uiEvents.folderRoot);
    }

    private static string GetImportedDestinationPath(string importsRoot, string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidDataException($"Could not determine a file name for {sourcePath}");

        var hash = ComputeSourcePathHash(sourcePath);
        if (fileName.EndsWith(".ui.stl", StringComparison.OrdinalIgnoreCase))
        {
            var baseName = fileName.Substring(0, fileName.Length - ".ui.stl".Length);
            return Path.Combine(importsRoot, $"{baseName}_{hash}.ui.stl");
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(importsRoot, $"{stem}_{hash}{extension}");
    }

    private static string ComputeSourcePathHash(string sourcePath)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(Path.GetFullPath(sourcePath));
            var hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }
    }

    private static void CopyFileIfNeeded(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            return;

        var normalizedSource = Path.GetFullPath(sourcePath);
        var normalizedDestination = Path.GetFullPath(destinationPath);
        if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
            return;

        var destinationDirectory = Path.GetDirectoryName(normalizedDestination);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        if (!ShouldCopyFile(normalizedSource, normalizedDestination))
            return;

        File.Copy(normalizedSource, normalizedDestination, true);
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
