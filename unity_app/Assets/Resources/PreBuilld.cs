#if UNITY_EDITOR
using System.Diagnostics;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Assets;
class MyCustomBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    private bool checkIfNewer(string sourceFile, string destinationFile)
    {
        var fiSource = new FileInfo(sourceFile);
        var fiDestination = new FileInfo(destinationFile);
        if (fiDestination.Exists == false ||
            fiSource.LastWriteTimeUtc > fiDestination.LastWriteTimeUtc)
            return true;
        else
            return false;
    }
    private void copyIfNewer(string sourceFile, string destinationFile)
    {
        if (checkIfNewer(sourceFile, destinationFile))
            File.Copy(sourceFile, destinationFile, true);
    }
    private void regenIfNeeded(string sourceFile, string outputFile, string regenCommand, string regenArgs)
    {
        if (checkIfNewer(sourceFile, outputFile))
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(regenCommand);
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.Arguments = regenArgs;

            try
            {
                UnityEngine.Debug.Log($"starting process {regenCommand} {regenArgs}");
                var p = Process.Start(startInfo);
                p.WaitForExit();
                if (p.ExitCode != 0 )
                {
                    throw new Exception($"regen exited with code {p.ExitCode}");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.ToString());
                Console.WriteLine(e.ToString());
            }
        }

    }
    public void OnPreprocessBuild(BuildReport report)
    {
        var sourceFiles = "../blender_source_files";
        var streamingAssets = Application.streamingAssetsPath;
        string[] blendFiles = new string[] { "100.clear.blend", "base.blend" };
        foreach (var f in blendFiles)
        {
            copyIfNewer(Path.Combine(sourceFiles, f), Path.Combine(streamingAssets, f));
        }
        foreach (var f in DataManager.hiddenFiles)
        {
            //Copy the cut files always. Sometimes I revert to an older copy.
            File.Copy(Path.Combine(sourceFiles, f), Path.Combine(streamingAssets, f), true);
        }

        var pythonFiles = "../blender_module_loading";

        string[] pyscripts = new string[] { "figureTool.py"};
        foreach (var f in pyscripts)
        {
            var exeDestination = Path.Combine(streamingAssets, Path.GetFileNameWithoutExtension(f) + ".exe");
            regenIfNeeded(Path.Combine(pythonFiles, f), 
                exeDestination , 
                "py", 
                $" -3.13 -m PyInstaller --onefile --collect-submodules \"bpy\" --collect-all \"bpy\"  --hidden-import __future__ --hidden-import future --hidden-import numpy --hidden-import tomllib --distpath=\"{streamingAssets}\" \"{Path.Combine(pythonFiles, f)}\" ");
        }
    }
}
#endif