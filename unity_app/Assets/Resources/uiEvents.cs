using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static stlImport;
using Cysharp.Threading.Tasks;
using SFB;
using System.Threading;
using System.Text.RegularExpressions;
using System.Text;
using Assets;
using UnityEngine.Rendering;
using Parabox.Stl;

public class uiEvents : MonoBehaviour
{
    private Label _clearLabel;
    private DropdownField _clearDropdown;
    private MultiColumnTreeView _tree;
    private TextField _outputTextField;
    private ProgressBar _progress;
    private Button _generateButton;
    private Button _saveButton;
    public static string previousFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public static string previousFolderStl = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
    private List<string> outputStrings = new List<string>();
    private Button _loadButton;
    private ImportTab _impotTab;
    private RadioButtonGroup _toleranceRadio;
    private RadioButtonGroup _sizeRadio;
    private VisualElement _toleranceDialog;
    private SelectorTab _allSelectorTab;
    private TabView _tabView;
    private Button _showOutputButton;
    public static Vector3 CameraLookAtPos = Vector3.zero;


    private static void OnToggleValueChanged(ChangeEvent<bool> evt)
    {
        //int tmp = 4;
        
    }
/*
    private void HandleSelectedToggleCallback(ChangeEvent<bool> evt)
    {
        Debug.Log("HandleCallback invoked with value " + evt.newValue);
        var item = (evt.target as Toggle)?.userData as StlFile;
        _tree.ClearSelection();
        _tree.AddToSelection(item.TreeIndex);
        if (item != null)
        {
            SetClearDropdownLabel($"Clears to apply to {Path.GetFileName(item.FullPath)}");
            item.Selected = evt.newValue;
            if (evt.newValue)
            {
                stlImport.LoadOne(item.AfterClearsAppliedFullPath ?? item.FullPath);
            }
            else
            {
                stlImport.Unload(item.AfterClearsAppliedFullPath ?? item.FullPath);
            }
        }
    }
*/
    public static Vector3 importLocation = new Vector3(0f, 1.2f, 0f);

    Vector3 headPos = new Vector3(0.244f, 1.070f, -0.024f);
    Vector3 headLookAtPos = new Vector3(0f, 1.070f, 0f);

    Vector3 torsoPos = new Vector3(0.545f, 0.9f, -0.580f);
    Vector3 torsoLookAtPos = new Vector3(0f, 0.9f, 0f);

    Vector3 legsPos = new Vector3(0.545f, 0.5f, -0.580f);
    Vector3 legsLookAtPos = new Vector3(0f, 0.5f, 0f);

    Vector3 fullPos = new Vector3(0.894f, 0.855f, -0.332f);
    Vector3 fullLookAtPos = new Vector3(0f, 0.855f, 0f);

    private CameraTarget previousTarget;

    public static Vector3 BaseSize = Vector3.zero;
    public static float BaseScale = 0;
    private enum CameraTarget
    {
        Head,
        Torso,
        Legs,
        Full,
    }

    private void ZoomCameraToTarget(CameraTarget target)
    {
        if (previousTarget != target)
        {
            var camera = Camera.main;
            var newPos = headPos;
            switch (target)
            {
                case CameraTarget.Head:
                    CameraLookAtPos = headLookAtPos;
                    newPos = headPos;
                    break;
                case CameraTarget.Torso:
                    CameraLookAtPos = torsoLookAtPos;
                    newPos = torsoPos;
                    break;

                case CameraTarget.Legs:
                    CameraLookAtPos = legsLookAtPos;
                    newPos = legsPos;
                    break;

                case CameraTarget.Full:
                    CameraLookAtPos = fullLookAtPos;
                    newPos = fullPos;
                    break;
            }
//            addLogFromMainThread($"moving camera to {newPos}");
            camera.transform.position = newPos;
            camera.transform.LookAt(CameraLookAtPos);
            previousTarget = target;
        }
    }

    public static string folderRoot = Path.Combine(Path.GetTempPath(), "action_figure_builder"); 
    public static string tempClearsPath = Path.Combine(Path.GetTempPath(), "action_figure_builder/tempClears"); 
    
    private StringBuilder outputBuilder = new StringBuilder();
    private void addLogFromMainThread(string msg)
    {
        outputBuilder.AppendLine(msg);
        _outputTextField.value = outputBuilder.ToString();
    }
    private void addLogFromBg(string msg)
    {
        outputBuilder.AppendLine(msg);
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.SwitchToMainThread();
            _outputTextField.value = outputBuilder.ToString();
            await UniTask.SwitchToThreadPool();
        }, configureAwait: false);
    }

    private async void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        //_tree = root.Q<MultiColumnTreeView>("TreeControl");
        _outputTextField = root.Q<TextField>("outputText");
        //_clearDropdown = root.Q<DropdownField>("ClearDropdown");
        //_clearLabel = root.Q<Label>("ClearLabel");
        _exitButton = root.Q<Button>("Exit");
        _progress = root.Q<UnityEngine.UIElements.ProgressBar>("Progress");
        _generateButton = root.Q<Button>("Generate");
        _saveButton = root.Q<Button>("SaveFigure");
        _loadButton = root.Q<Button>("LoadFigure");
        _impotTab = root.Q<ImportTab>("ImportTabControl");
        _toleranceRadio = root.Q<RadioButtonGroup>("ToleranceRadio");
        _sizeRadio = root.Q<RadioButtonGroup>("SizeRadio");
        _toleranceDialog = root.Q<VisualElement>("Modal1");
        _allSelectorTab = root.Q<SelectorTab>("AllSelectorTab");
        _tabView = root.Q<TabView>("tabView");
        _tabView.activeTabChanged += _tabView_activeTabChanged;
        _showOutputButton = root.Q<Button>("showOutput");
        _showOutputButton.RegisterCallback<ClickEvent>((evt) =>
        {
            if (_showOutputButton.text == "Open Log")
            {
                _outputTextField.style.display = DisplayStyle.Flex;
                _showOutputButton.text = "Close";
            }
            else
            {
                _outputTextField.style.display = DisplayStyle.None;
                _showOutputButton.text = "Open Log";
            }
        });


        _toleranceDialog.style.display = DisplayStyle.None;
        _outputTextField.style.whiteSpace = WhiteSpace.Normal;


        var openModalButton = root.Q<Button>("OpenModalButton");
        openModalButton.RegisterCallback<ClickEvent>(e => OnOpenModal());
        _toleranceDialog.RegisterCallback<PointerDownEvent>((e) =>
        {
            e.StopImmediatePropagation();
            CloseModal();
        });

        Directory.CreateDirectory(folderRoot);
        await RunInBackground(() =>
        {
            ExportStlsFromBlender(folderRoot, addLogFromBg);
        }, "Rebuilding object cache");
        Directory.CreateDirectory(tempClearsPath);

        DataManager.Instance.Load(folderRoot);

        _exitButton.RegisterCallback<ClickEvent>(e =>
        {
            Application.Quit();
        });
        /*
        _progress.value = 0;
        _progress.visible = false;
        _progress.style.display = DisplayStyle.None;
        */
        _generateButton.RegisterCallback<ClickEvent>(HandleExportStlClick);

        _saveButton.RegisterCallback<ClickEvent>(HandleSaveButtonClick);
        

        _loadButton.RegisterCallback<ClickEvent>(HandleLoadButtonClick);
        var basePath = Path.Combine(folderRoot, "base.stl");
        var stl = new StlFile()
        {
            Name = Path.GetFileName(basePath),
            FullPath = basePath,
            SelectionCanChange = false,
        };
        _baseObject = stlImport.LoadOne(stl);

        var meshSize = _baseObject.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        var baseScale = _baseObject.GetComponent<Transform>().localScale[0];
        _baseSize = new Vector3(meshSize[0] * baseScale, meshSize[1] * baseScale, meshSize[2] * baseScale);
        uiEvents.BaseSize = _baseSize;
        uiEvents.BaseScale = baseScale;

        importLocation = new Vector3(0f, Utils.reversePercentage(_baseSize[1], 120), 0f);

        var headPercent = 110;
        headPos = new Vector3(Utils.reversePercentage(_baseSize[0], 150), Utils.reversePercentage(_baseSize[1], headPercent), Utils.reversePercentage(_baseSize[2], -25));
        headLookAtPos = new Vector3(0f, Utils.reversePercentage(_baseSize[1], headPercent), 0f);

        var torsoPercent = 90;
        torsoPos = new Vector3(Utils.reversePercentage(_baseSize[0], 300), Utils.reversePercentage(_baseSize[1], torsoPercent), Utils.reversePercentage(_baseSize[2], -25));
        torsoLookAtPos = new Vector3(0f, Utils.reversePercentage(_baseSize[1], torsoPercent), 0f);

        var legsPercent = 50;
        legsPos = new Vector3(Utils.reversePercentage(_baseSize[0], 300), Utils.reversePercentage(_baseSize[1], legsPercent), Utils.reversePercentage(_baseSize[2], -25));
        legsLookAtPos = new Vector3(0f, Utils.reversePercentage(_baseSize[1], legsPercent), 0f);
        
        var fullPercent = 90;
        fullPos = new Vector3(Utils.reversePercentage(_baseSize[0], 700), Utils.reversePercentage(_baseSize[1], fullPercent), Utils.reversePercentage(_baseSize[2], -15));
        fullLookAtPos = new Vector3(0f, Utils.reversePercentage(_baseSize[1], fullPercent - 10), 0f);

        DataManager.Instance.OnAppliedChanged += OnAppliedChanged;
    
        ZoomCameraToTarget(CameraTarget.Full);
    }

    private void _tabView_activeTabChanged(Tab arg1, Tab arg2)
    {
        if (arg2.name == "HeadTab")
            ZoomCameraToTarget(CameraTarget.Head);
        else if (arg2.name == "TorsoTab")
            ZoomCameraToTarget(CameraTarget.Torso);
        else if (arg2.name == "LegsTab")
            ZoomCameraToTarget(CameraTarget.Legs);
        else
            ZoomCameraToTarget(CameraTarget.Full);
    }

    private async void OnAppliedChanged()
    {
        //Remove any objects that have been removed.
        var obj = GameObject.FindGameObjectsWithTag("stl");
        foreach (var obj2 in obj)
        {
            if (DataManager.Instance.AllApplied.Any(s => s.UiKey == obj2.name) == false)
            {
                stlImport.Unload(obj2.name);
            }
        }
        //Add any objects
        foreach (var stl in DataManager.Instance.AllApplied)
        {
            var go = GameObject.Find(stl.UiKey);
            if (go == null)
            {
                await LoadNewObjectForClear(stl);
                go = GameObject.Find(stl.UiKey);
            }
            if (go != null && stl.Transforms != null && stl.Transforms.Position.Length > 0 && (stl.ClearToApply == null || stl.ClearToApply == string.Empty))
                ApplyTransformsToObject(stl.Transforms, go);
        }
    }


    public void ApplyTransformsToObject(Transforms t, GameObject g)
    {

        g.GetComponent<Transform>().localScale = new Vector3(Utils.reversePercentage(uiEvents.BaseScale, t.Scale[0]),
                                Utils.reversePercentage(uiEvents.BaseScale, t.Scale[1]),
                                Utils.reversePercentage(uiEvents.BaseScale, t.Scale[2]));

        g.transform.position = new Vector3(Utils.reversePercentage(uiEvents.BaseSize[0], t.Position[0]),
                                Utils.reversePercentage(uiEvents.BaseSize[1], t.Position[1]),
                                Utils.reversePercentage(uiEvents.BaseSize[2], t.Position[2]));

        g.transform.rotation = Quaternion.identity;
        g.transform.Rotate(new Vector3(t.Rotations[0],
                                t.Rotations[1],
                                t.Rotations[2]), Space.Self);
    }

    private bool matchesFolder(string fullPath, IEnumerable<string> numberFolders)
    {
        var clearFolders = decomposePath(fullPath);
        return numberFolders.Any(f => clearFolders.Any(c => c == f));

    }

    private List<string> decomposePath(string fullPath)
    {
        var myParentsPath = Path.GetDirectoryName(fullPath);
        if (myParentsPath == null)
            return new List<string>();
        var parentsDecomposed = decomposePath(myParentsPath);
        if (Directory.Exists(fullPath))
            parentsDecomposed.Add(new DirectoryInfo(fullPath).Name);
        return parentsDecomposed;
    }

    private string lastFileName = "figure";
    private async void HandleExportStlClick(ClickEvent evt)
    {
        UnityEngine.Debug.Log($"HandleExportStlClick starting");

        var export = getExports();
       
        var cutsFileName = "";
        var sixinch = _sizeRadio.value == 2;
        var twoinch = _sizeRadio.value == 0;
        var sizeString = _sizeRadio.value switch
        {
            0 => "2.75",
            1 => "3.75",
            2 => "6",
            _ => "WHAA"
        };
        var postScale = _sizeRadio.value switch
        {
            0 => 0.7333333,
            1 => 1.0,
            2 => 1.6,
            _ => 1
        };
        var toleranceString = _toleranceRadio.value switch
        {
            0 => "0.25",
            1 => "0.30",
            2 => "0.35",
            _ => "WHAA"
        };
        cutsFileName = $"cuts_{sizeString}_{toleranceString}.stl";
        
        var tolerance = Path.GetFileNameWithoutExtension(cutsFileName).Substring(4);
        var newFileName = $"{lastFileName}_{sizeString}in_{tolerance}";
        var path = StandaloneFileBrowser.SaveFilePanel(
          "Save figure",
          previousFolder,
          newFileName + ".stl",
          "stl");

        if (path.Length != 0)
        {
            var tempFile = $"{uiEvents.tempClearsPath}/{lastFileName}_{sizeString}in_{tolerance}.json";
            var exportFile = new ExportFile() { children = export.ToArray() };
            exportFile.CutsFileFullPath = Path.Combine(folderRoot, cutsFileName);
            exportFile.ScaleFactor = postScale;

            var output = JsonUtility.ToJson(exportFile);
            File.WriteAllText(tempFile, output);

            var task = RunInBackground(() =>
            {
                CreateFinalFigure(tempFile, path, addLogFromBg);
            }, $"Creating figure for {lastFileName}");
            CloseModal();
            await task;
        }
    }

    int progressStack = 0;
    private Button _exitButton;
    private GameObject _baseObject;
    private Vector3 _baseSize;
    
    private void StartProgress(string label = "Working")
    {
        _progress.visible = true;
        _progress.title = label;
        _progress.style.display = DisplayStyle.Flex;
        outputBuilder.Clear();
        progressStack++;
        _tabView.SetEnabled(false);
        _generateButton.SetEnabled(false);
        _loadButton.SetEnabled(false);
        _saveButton.SetEnabled(false);
        _exitButton.SetEnabled(false);
        //_tree.SetEnabled(false);
        UniTask.RunOnThreadPool(async () =>
        {
            while(progressStack > 0)
            {
                await UniTask.SwitchToThreadPool();
                await UniTask.WaitForSeconds(.2f);
                await UniTask.SwitchToMainThread();
                _progress.value = (_progress.value + 5) % _progress.highValue;
            }
            await UniTask.SwitchToMainThread();
            _progress.visible = false;
            _progress.style.display = DisplayStyle.None;
            _generateButton.SetEnabled(true);
            _loadButton.SetEnabled(true);
            _saveButton.SetEnabled(true);
            _exitButton.SetEnabled(true);
            _tabView.SetEnabled(true);
            // _tree.SetEnabled(true);
        });
    }

    private void StopProgress()
    {
        progressStack--;
    }

    private async UniTask RunInBackground(Func<UniTask> action, string label = "Working")
    {
        StartProgress(label);
        await UniTask.SwitchToThreadPool();
        await action();
        await UniTask.SwitchToMainThread();
        StopProgress();
    }

    private async UniTask RunInBackground(Action action, string label = "Working")
    {
        StartProgress(label);
        await UniTask.SwitchToThreadPool();
        action();
        await UniTask.SwitchToMainThread();
        StopProgress();
    }



    private async void HandleLoadButtonClick(ClickEvent evt)
    {
        var results = StandaloneFileBrowser.OpenFilePanel("Load figure", previousFolder, "json", false);
        if (results == null || results.Length == 0)
            return;
        string path = results[0];
        if (path.Length != 0)
        {
            lastFileName = Path.GetFileNameWithoutExtension(path);
            DataManager.Instance.RemoveAllObjects();
            ClearLoadedFiles();
            FileInfo fileInfo = new FileInfo(path);
            previousFolder = fileInfo.DirectoryName;
            var fileContent = File.ReadAllText(path);
            var output = JsonUtility.FromJson<ExportFile>(fileContent);
            addLogFromMainThread("Starting load...");
            await RunInBackground(async () =>
            {
                await recursiveImport(DataManager.Instance.StlTree, output);
                var allImports = output.children.Where(c => c.IsImport).ToList();
                foreach (var item in allImports)
                {
                    DataManager.Instance.ApplyObject(new StlFile()
                    {
                        Name = Path.GetFileName(item.FullPath),
                        FullPath = item.FullPath,
                        IsImport = item.IsImport,
                        SelectionCanChange = true,
                        ClearToApply = item.ClearToApplyFullPath,
                        originalSize = item.OriginalSize,
                        Transforms = item.transforms
                    }, false);
                }
                await UniTask.SwitchToMainThread();
                if (allImports.Any())
                    _impotTab.SetSelectedImport(DataManager.Instance.AllApplied.First(s => s.IsImport));
                DataManager.Instance.NotifiyAppliedChanged();

            }, "Loading figure...");
            ZoomCameraToTarget(CameraTarget.Full);
        }
    }

    private async UniTask recursiveImport(IEnumerable<TreeViewItemData<ITreeItem>> hierarchy, ExportFile importedData)
    {
        foreach (var hierarchyItem in hierarchy)
        {
            if (hierarchyItem.children.Count() > 0)
                await recursiveImport(hierarchyItem.children, importedData);
            else
            {
                var stl = hierarchyItem.data as StlFile;
                if (stl != null)
                {
                    var fileName = Path.GetFileName(stl.FullPath);
                    var imported = importedData.children.FirstOrDefault(c => Path.GetFileName(c.FullPath) == fileName);
                    if (imported != null)
                    {
                        addLogFromBg($"loading {fileName}");
                        stl.Selected = true;
                        var clearFileName = Path.GetFileName(imported.ClearToApplyFullPath);
                        if (clearFileName != null && clearFileName != string.Empty)
                            addLogFromBg($"applying modifier {clearFileName}");
                        await UniTask.SwitchToMainThread();
                        //stl.ClearToApply = DataManager.Instance.AllClears.FirstOrDefault(clear => Path.GetFileName(clear.FullPath) == clearFileName)?.FullPath;
                        DataManager.Instance.ApplyClear(stl, clearFileName, false);
                        await UniTask.SwitchToThreadPool();
                    }
                }
            }
        }
    }

    private void HandleSaveButtonClick(ClickEvent evt)
    {
        var path = StandaloneFileBrowser.SaveFilePanel(
          "Save figure setup",
          previousFolder,
          lastFileName + ".json",
          "json");

        if (path.Length != 0)
        {
            lastFileName = Path.GetFileNameWithoutExtension(path);
            FileInfo fileInfo = new FileInfo(path);
            previousFolder = fileInfo.DirectoryName;
            var exportFile =  new ExportFile() { children = getExports().ToArray() };
            var output = JsonUtility.ToJson(exportFile);
            File.WriteAllText(path, output);
        }
    }
    private int getSeparatorCount(string fullPath) => fullPath.Count(c => c == Path.DirectorySeparatorChar
                                                     || c == Path.AltDirectorySeparatorChar);
    private List<BareMinimumStlFile> getExports()
    {

        var exports = DataManager.Instance.AllApplied.OrderBy(bm => bm.FullPath.Count(c => c == Path.DirectorySeparatorChar
                                                     || c == Path.AltDirectorySeparatorChar)).ToList();
        exports.Sort((a1, a2) =>
        {
            if (a1.Name == "base.stl") return -1;
            if (a2.Name == "base.stl") return  1;
            //Sort all imports to the end.
            if (a1.IsImport && a2.IsImport == false) return 1;
            if (a1.IsImport ==false && a2.IsImport ) return -1;
            var separatorCountDiff = getSeparatorCount(a1.FullPath) - getSeparatorCount(a2.FullPath);
            if (separatorCountDiff != 0) return separatorCountDiff;
            return a1.Name.CompareTo(a2.Name);
        });
        return exports.Select(stl => stl.Export()).ToList();
    }

    
    private async UniTask LoadNewObjectForClear(StlFile treeItem)
    {
        await UniTask.SwitchToMainThread();
        stlImport.Unload(treeItem.FullPath);
        stlImport.Unload(treeItem.AfterClearsAppliedFullPath);
        if (treeItem.ClearToApply != null && File.Exists(treeItem.ClearToApply))
        {
            treeItem.AfterClearsAppliedFullPath = $"{tempClearsPath}/{Path.GetFileName(treeItem.FullPath)}_minus_{Path.GetFileName(treeItem.ClearToApply)}";
            if (!File.Exists(treeItem.AfterClearsAppliedFullPath))
            {
                await RunInBackground(() =>
                {
                    stlImport.ApplyClearJson(treeItem, addLogFromBg);
                }, $"Loading modified object for {treeItem.Name}");
            }
            await UniTask.SwitchToMainThread();
            stlImport.LoadOne(treeItem);
        }
        else
        {
            treeItem.AfterClearsAppliedFullPath = null;
            stlImport.LoadOne(treeItem);
        }
    }

    private void OnOpenModal()
    {
        _toleranceDialog.style.display = DisplayStyle.Flex;
    }

    private void OnCloseModal()
    {
        CloseModal();
    }

    private void CloseModal()
    {
        _toleranceDialog.style.display = DisplayStyle.None;
    }
}