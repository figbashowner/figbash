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
    public static uiEvents Instance { get; private set; }

    private Label _clearLabel;
    private DropdownField _clearDropdown;
    private MultiColumnTreeView _tree;
    private TextField _outputTextField;
    private ProgressBar _progress;
    private Button _saveButton;
    public static string previousFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public static string previousFolderStl = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
    private List<string> outputStrings = new List<string>();
    private Button _loadButton;
    private ImportTab _impotTab;
    private GenerateFigureDialog _generateFigureDialog;
    private SelectorTab _allSelectorTab;
    private RepoTab _repoTab;
    private TabView _tabView;
    private Button _showOutputButton;
    public static Vector3 CameraLookAtPos = Vector3.zero;
    private bool _downloadProgressVisible;
    private VisualElement _progressIndeterminateOverlay;
    private VisualElement _progressIndeterminateBar;
    private bool _progressIndeterminate;
    private float _progressIndeterminatePhase;


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

    public static string folderRoot { get; private set; }
    public static string tempClearsPath { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeStoragePaths()
    {
        if (!string.IsNullOrWhiteSpace(folderRoot) && !string.IsNullOrWhiteSpace(tempClearsPath))
            return;

        folderRoot = Application.persistentDataPath;
        tempClearsPath = Path.Combine(folderRoot, "tempClears");
        Directory.CreateDirectory(folderRoot);
        Directory.CreateDirectory(tempClearsPath);
    }
    
    private StringBuilder outputBuilder = new StringBuilder();
    private void addLogFromMainThread(string msg)
    {
        outputBuilder.AppendLine(msg);
        _outputTextField.value = outputBuilder.ToString();
    }
    private void addLogFromBg(string msg)
    {
        outputBuilder.AppendLine(msg);
        if (outputBuilder.Length <= 0)
            return;
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.SwitchToMainThread();
            _outputTextField.value = outputBuilder.ToString();
            await UniTask.SwitchToThreadPool();
        }, configureAwait: false);
    }

    private async void OnEnable()
    {
        Instance = this;
        InitializeStoragePaths();

        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        //_tree = root.Q<MultiColumnTreeView>("TreeControl");
        _outputTextField = root.Q<TextField>("outputText");
        UiInputCaptureState.TrackTextInput(_outputTextField);
        //_clearDropdown = root.Q<DropdownField>("ClearDropdown");
        //_clearLabel = root.Q<Label>("ClearLabel");
        _exitButton = root.Q<Button>("Exit");
        _progress = root.Q<UnityEngine.UIElements.ProgressBar>("Progress");
        DownloadManager.ProgressChanged += HandleDownloadProgressChanged;
        _saveButton = root.Q<Button>("SaveFigure");
        _loadButton = root.Q<Button>("LoadFigure");
        _impotTab = root.Q<ImportTab>("ImportTabControl");
        _generateFigureDialog = root.Q<GenerateFigureDialog>("GenerateFigureDialogControl");
        _allSelectorTab = root.Q<SelectorTab>("AllSelectorTab");
        _repoTab = root.Q<RepoTab>("RepoTabControl");
        _tabView = root.Q<TabView>("tabView");
        UiInputCaptureState.TrackPointerHover(_tabView);
        UiInputCaptureState.TrackPointerHover(_allSelectorTab);
        InitializeProgressOverlay();
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


        _outputTextField.style.whiteSpace = WhiteSpace.Normal;


        var openModalButton = root.Q<Button>("OpenModalButton");
        openModalButton.RegisterCallback<ClickEvent>(e => _generateFigureDialog?.ShowDialog());
        if (_generateFigureDialog != null)
        {
            _generateFigureDialog.GenerateRequested += HandleExportStlClick;
            _generateFigureDialog.HideDialog();
        }
       

        Directory.CreateDirectory(folderRoot);
        await RunInBackground(() =>
        {
            CopyBundledStlsFromStreamingAssets(folderRoot, addLogFromBg);
        }, "Seeding object cache");
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
        _saveButton.RegisterCallback<ClickEvent>(HandleSaveButtonClick);
        

        _loadButton.RegisterCallback<ClickEvent>(HandleLoadButtonClick);
        var basePath = Path.Combine(folderRoot, "base.stl");
        var baseUiPath = Utils.GetUiSidecarPath(basePath);
        var stl = new StlFile()
        {
            Name = Path.GetFileName(basePath),
            FullPath = basePath,
            SelectionCanChange = false,
            UiName = File.Exists(baseUiPath) ? Path.GetFileName(baseUiPath) : null,
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
        PromptDialog.ShowOrientation(root);

        if (_repoTab != null)
            await _repoTab.LoadSavedRepositoriesAsync();
    }

    private void OnDisable()
    {
        DownloadManager.ProgressChanged -= HandleDownloadProgressChanged;
        if (_generateFigureDialog != null)
        {
            _generateFigureDialog.GenerateRequested -= HandleExportStlClick;
        }

        if (Instance == this)
            Instance = null;
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

    private void HandleDownloadProgressChanged(DownloadProgressState state)
    {
        if (_progress == null)
            return;

        if (!state.Visible)
        {
            _downloadProgressVisible = false;
            if (progressStack <= 0)
            {
                _progress.visible = false;
                _progress.style.display = DisplayStyle.None;
            }
            return;
        }

        _downloadProgressVisible = true;
        _progress.visible = true;
        _progress.style.display = DisplayStyle.Flex;
        _progress.highValue = Math.Max(state.Total, 1);
        _progress.value = Math.Min(state.Completed, state.Total);
        _progress.title = string.IsNullOrWhiteSpace(state.Label)
            ? $"Downloading ({state.Completed}/{state.Total})"
            : $"{state.Label} ({state.Completed}/{state.Total})";
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
    private async void HandleExportStlClick()
    {
        UnityEngine.Debug.Log($"HandleExportStlClick starting");

        var export = getExports();
       
        var sixinch = _generateFigureDialog != null && _generateFigureDialog.GenerateSix;
        var nineinch = _generateFigureDialog != null && _generateFigureDialog.GenerateNine;
        var twoinch = _generateFigureDialog != null && _generateFigureDialog.GenerateTwo;
        var threeinch = _generateFigureDialog != null && _generateFigureDialog.GenerateThree;
        List<CutConfig> cuts = new List<CutConfig>();
        Func<string, string, string> createCutsFilePath = (sizeString, toleranceString) =>
        {
            return Path.Combine(folderRoot, $"cuts_{sizeString}_{toleranceString}.stl");
        };

        var toleranceString = _generateFigureDialog?.SelectedTolerance ?? "0.25";

        Action<string, double> addCut = (sizeString, scaleFactor) =>
        {
            cuts.Add(new CutConfig()
            {
                CutsFileFullPath = createCutsFilePath(sizeString, toleranceString),
                ScaleFactor = scaleFactor
            });
        };

        
        if (twoinch)
        {
            if (toleranceString == "0.25")
                addCut("2.75", 0.7333333);
        }
        if (threeinch)
        {
            addCut("3.75", 1);
        }
        if (sixinch)
        {
            addCut("6", 1.6);
        }
        if (nineinch)
        {
            addCut("9", 2.4);
        }

        var newFileName = $"{lastFileName}";
        var path = StandaloneFileBrowser.SaveFilePanel(
          "Save figure",
          previousFolder,
          newFileName + ".stl",
          "stl");

        
        if (path.Length != 0)
        {
            foreach (var cut in cuts)
            {
                cut.OutputFileFullPath = Path.Combine(Path.GetDirectoryName(path), 
                                                    Path.GetFileNameWithoutExtension(path) 
                                                    + Path.GetFileName(cut.CutsFileFullPath).Substring(4));
            }

            if (!await EnsureFigureDownloadsReady(export))
                return;

            var tempFile = $"{uiEvents.tempClearsPath}/{lastFileName}_{toleranceString}.json";
            var exportFile = new ExportFile() { children = export.ToArray() };
            exportFile.cutConfigs = cuts.ToArray();

            var output = JsonUtility.ToJson(exportFile);
            File.WriteAllText(tempFile, output);

            var task = RunInBackground(() =>
            {
                CreateFinalFigure(tempFile, path, addLogFromBg);
            }, $"Creating figure for {lastFileName}", indeterminate: true);
            await task;
        }
    }

    private async UniTask<bool> EnsureFigureDownloadsReady(IEnumerable<BareMinimumStlFile> exportFiles)
    {
        var requests = new List<DownloadRequest>();
        var unresolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in exportFiles ?? Enumerable.Empty<BareMinimumStlFile>())
        {
            CollectDownloadRequest(file?.FullPath, requests, unresolved, seen);
            CollectDownloadRequest(file?.ClearToApplyFullPath, requests, unresolved, seen);
        }

        if (unresolved.Count > 0)
        {
            PromptDialog.ShowAlert(GetRootOwner(), "Missing figure files", string.Join("\n", unresolved.Take(8)));
            return false;
        }

        if (requests.Count == 0)
            return true;

        var completion = new UniTaskCompletionSource<DownloadBatchResult>();
        DownloadManager.DownloadAsync(
            requests,
            "Downloading figure assets",
            onSuccess: result => completion.TrySetResult(result),
            onFailure: result => completion.TrySetResult(result));

        var result = await completion.Task;
        if (!result.Success)
        {
            ShowDownloadFailure("Figure download failed", result);
            return false;
        }

        return true;
    }

    private VisualElement GetRootOwner()
    {
        var document = GetComponent<UIDocument>();
        return document != null ? document.rootVisualElement : null;
    }

    private void CollectDownloadRequest(string fullPath, List<DownloadRequest> requests, List<string> unresolved, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || File.Exists(fullPath))
            return;

        if (!DataManager.Instance.TryCreateDownloadRequest(fullPath, out var request))
        {
            unresolved.Add(fullPath);
            return;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.LocalPath))
            return;

        if (!seen.Add(request.LocalPath))
            return;

        requests.Add(request);
    }

    private void ShowDownloadFailure(string title, DownloadBatchResult result)
    {
        var failureLines = result.Failures
            .Take(5)
            .Select(f => $"{Path.GetFileName(f.LocalPath)}: {f.Error}")
            .ToList();

        var message = failureLines.Count == 0
            ? "One or more downloads failed."
            : string.Join("\n", failureLines);

        if (result.Failures.Count > failureLines.Count)
            message += $"\n...and {result.Failures.Count - failureLines.Count} more.";

        PromptDialog.ShowAlert(GetRootOwner(), title, message);
    }

    int progressStack = 0;
    private Button _exitButton;
    private GameObject _baseObject;
    private Vector3 _baseSize;
    
    private void StartProgress(string label = "Working", bool indeterminate = false)
    {
        _progress.visible = true;
        _progress.title = label;
        _progress.style.display = DisplayStyle.Flex;
        outputBuilder.Clear();
        progressStack++;
        SetIndeterminateProgress(indeterminate);
        if (!indeterminate && _progress.highValue <= 0)
            _progress.highValue = 100;
        _tabView.SetEnabled(false);
        _generateFigureDialog?.SetEnabled(false);
        _loadButton.SetEnabled(false);
        _saveButton.SetEnabled(false);
        _exitButton.SetEnabled(false);
        //_tree.SetEnabled(false);
        UniTask.RunOnThreadPool(async () =>
        {
            while (progressStack > 0)
            {
                await UniTask.SwitchToThreadPool();
                await UniTask.WaitForSeconds(_progressIndeterminate ? .05f : .2f);
                await UniTask.SwitchToMainThread();
                if (_progressIndeterminate)
                {
                    _progressIndeterminatePhase = (_progressIndeterminatePhase + 0.04f) % 1f;
                    UpdateIndeterminateProgress();
                }
                else
                {
                    _progress.value = (_progress.value + 5) % Math.Max(_progress.highValue, 1);
                }
            }
            await UniTask.SwitchToMainThread();
            SetIndeterminateProgress(false);
            _progress.visible = false;
            _progress.style.display = DisplayStyle.None;
            _generateFigureDialog?.SetEnabled(true);
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

    private void InitializeProgressOverlay()
    {
        if (_progress == null || _progressIndeterminateOverlay != null)
            return;

        var progressBackground = _progress.Q<VisualElement>(className: ProgressBar.backgroundUssClassName) ?? _progress;

        _progressIndeterminateOverlay = new VisualElement
        {
            name = "ProgressIndeterminateOverlay",
            pickingMode = PickingMode.Ignore
        };
        _progressIndeterminateOverlay.style.position = Position.Absolute;
        _progressIndeterminateOverlay.style.left = 0;
        _progressIndeterminateOverlay.style.right = 0;
        _progressIndeterminateOverlay.style.top = 0;
        _progressIndeterminateOverlay.style.bottom = 0;
        _progressIndeterminateOverlay.style.overflow = Overflow.Hidden;
        _progressIndeterminateOverlay.style.display = DisplayStyle.None;

        _progressIndeterminateBar = new VisualElement
        {
            name = "ProgressIndeterminateBar",
            pickingMode = PickingMode.Ignore
        };
        _progressIndeterminateBar.style.position = Position.Absolute;
        _progressIndeterminateBar.style.top = 0;
        _progressIndeterminateBar.style.bottom = 0;
        _progressIndeterminateBar.style.left = Length.Percent(-30);
        _progressIndeterminateBar.style.width = Length.Percent(30);
        _progressIndeterminateBar.style.backgroundColor = new Color(0.11f, 0.65f, 0.64f, 0.9f);
        _progressIndeterminateBar.style.borderTopLeftRadius = 8;
        _progressIndeterminateBar.style.borderBottomLeftRadius = 8;
        _progressIndeterminateBar.style.borderTopRightRadius = 8;
        _progressIndeterminateBar.style.borderBottomRightRadius = 8;

        _progressIndeterminateOverlay.Add(_progressIndeterminateBar);
        progressBackground.Add(_progressIndeterminateOverlay);
    }

    private void SetIndeterminateProgress(bool indeterminate)
    {
        _progressIndeterminate = indeterminate;
        if (_progressIndeterminateOverlay == null || _progressIndeterminateBar == null)
            return;

        _progressIndeterminateOverlay.style.display = indeterminate ? DisplayStyle.Flex : DisplayStyle.None;
        if (indeterminate)
        {
            _progressIndeterminatePhase = 0f;
            _progress.value = 0;
            _progress.highValue = 1;
            UpdateIndeterminateProgress();
        }
    }

    private void UpdateIndeterminateProgress()
    {
        if (_progressIndeterminateBar == null)
            return;

        const float segmentWidth = 0.30f;
        var travel = 1f + segmentWidth;
        var left = (_progressIndeterminatePhase * travel) - segmentWidth;
        _progressIndeterminateBar.style.left = Length.Percent(left * 100f);
        _progressIndeterminateBar.style.width = Length.Percent(segmentWidth * 100f);
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

    private async UniTask RunInBackground(Func<UniTask> action, string label, bool indeterminate)
    {
        StartProgress(label, indeterminate);
        await UniTask.SwitchToThreadPool();
        await action();
        await UniTask.SwitchToMainThread();
        StopProgress();
    }

    private async UniTask RunInBackground(Action action, string label, bool indeterminate)
    {
        StartProgress(label, indeterminate);
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
            var fileContent = File.ReadAllText(path);
            var output = JsonUtility.FromJson<ExportFile>(fileContent);
            if (output == null)
            {
                PromptDialog.ShowAlert(GetRootOwner(), "Load figure failed", "The selected figure file could not be read.");
                return;
            }

            NormalizeLoadedFigurePaths(output);
            if (!await EnsureFigureRepositoriesLoadedAsync(output))
                return;

            lastFileName = Path.GetFileNameWithoutExtension(path);
            DataManager.Instance.RemoveAllObjects();
            ClearLoadedFiles();
            FileInfo fileInfo = new FileInfo(path);
            previousFolder = fileInfo.DirectoryName;
            addLogFromMainThread("Starting load...");
            await RunInBackground(async () =>
            {
                await recursiveImport(DataManager.Instance.StlTree, output);
                var loadedChildren = output.children ?? Array.Empty<BareMinimumStlFile>();
                var allImports = loadedChildren.Where(c => c.IsImport).ToList();
                foreach (var item in allImports)
                {
                    DataManager.Instance.ApplyObject(new StlFile()
                    {
                        Name = Path.GetFileName(item.FullPath),
                        FullPath = item.FullPath,
                        IsImport = item.IsImport,
                        SelectionCanChange = true,
                        ClearToApply = item.ClearToApplyFullPath,
                        RepositorySource = item.RepositorySource,
                        ClearRepositorySource = item.ClearRepositorySource,
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

    private async UniTask<bool> EnsureFigureRepositoriesLoadedAsync(ExportFile figure)
    {
        if (figure?.children == null)
            return true;

        var sources = figure.children
            .Select(child => child?.RepositorySource)
            .Concat(figure.children.Select(child => child?.ClearRepositorySource))
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sources.Count == 0)
            return true;

        if (_repoTab == null)
            return sources.All(source => DataManager.Instance.HasRepositorySource(source));

        foreach (var source in sources)
        {
            if (DataManager.Instance.HasRepositorySource(source))
                continue;

            if (!await _repoTab.EnsureRepositoryLoadedAsync(source))
                return false;
        }

        return true;
    }

    private async UniTask recursiveImport(IEnumerable<TreeViewItemData<ITreeItem>> hierarchy, ExportFile importedData)
    {
        if (hierarchy == null || importedData?.children == null)
            return;

        foreach (var imported in importedData.children)
        {
            var stl = FindMatchingTreeItem(hierarchy, imported);
            if (stl == null)
                continue;

            var fileName = Path.GetFileName(stl.FullPath);
            addLogFromBg($"loading {fileName}");
            stl.Selected = true;
            var clearFileName = Path.GetFileName(imported.ClearToApplyFullPath);
            if (!string.IsNullOrWhiteSpace(clearFileName))
                addLogFromBg($"applying modifier {clearFileName}");
            await UniTask.SwitchToMainThread();
            //stl.ClearToApply = DataManager.Instance.AllClears.FirstOrDefault(clear => Path.GetFileName(clear.FullPath) == clearFileName)?.FullPath;
            DataManager.Instance.ApplyClear(stl, clearFileName, false);
            await UniTask.SwitchToThreadPool();
        }
    }

    private static StlFile FindMatchingTreeItem(IEnumerable<TreeViewItemData<ITreeItem>> hierarchy, BareMinimumStlFile imported)
    {
        StlFile fallback = null;
        var exactMatch = FindMatchingTreeItem(hierarchy, imported, ref fallback);
        return exactMatch ?? fallback;
    }

    private static StlFile FindMatchingTreeItem(IEnumerable<TreeViewItemData<ITreeItem>> hierarchy, BareMinimumStlFile imported, ref StlFile fallback)
    {
        if (hierarchy == null || imported == null || string.IsNullOrWhiteSpace(imported.FullPath))
            return null;

        var targetPath = imported.FullPath;
        var targetFileName = Path.GetFileName(targetPath);

        foreach (var hierarchyItem in hierarchy)
        {
            if (hierarchyItem.children != null && hierarchyItem.children.Count() > 0)
            {
                var childMatch = FindMatchingTreeItem(hierarchyItem.children, imported, ref fallback);
                if (childMatch != null)
                    return childMatch;
            }

            var stl = hierarchyItem.data as StlFile;
            if (stl == null)
                continue;

            if (PathsEqual(stl.FullPath, targetPath) || PathsEqual(stl.UiPath, targetPath))
                return stl;

            if (fallback == null && !string.IsNullOrWhiteSpace(targetFileName))
            {
                var fileName = Path.GetFileName(stl.FullPath);
                if (!string.IsNullOrWhiteSpace(fileName) && string.Equals(fileName, targetFileName, StringComparison.OrdinalIgnoreCase))
                    fallback = stl;
                else if (!string.IsNullOrWhiteSpace(stl.UiName) && string.Equals(stl.UiName, targetFileName, StringComparison.OrdinalIgnoreCase))
                    fallback = stl;
            }
        }

        return fallback;
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
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
            var exportFile = new ExportFile()
            {
                children = getExports()
                    .Select(CreatePortableExportChild)
                    .Where(child => child != null)
                    .ToArray()
            };
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

    private static void NormalizeLoadedFigurePaths(ExportFile exportFile)
    {
        if (exportFile?.children != null)
        {
            foreach (var child in exportFile.children)
            {
                if (child == null)
                    continue;

                child.FullPath = Utils.ResolvePortablePath(child.FullPath, folderRoot);
                child.ClearToApplyFullPath = Utils.ResolvePortablePath(child.ClearToApplyFullPath, folderRoot);
                child.RepositorySource = Utils.ResolvePortableRepositorySource(child.RepositorySource, folderRoot);
                child.ClearRepositorySource = Utils.ResolvePortableRepositorySource(child.ClearRepositorySource, folderRoot);
            }
        }

        if (exportFile?.cutConfigs == null)
            return;

        foreach (var cutConfig in exportFile.cutConfigs)
        {
            if (cutConfig == null)
                continue;

            cutConfig.CutsFileFullPath = Utils.ResolvePortablePath(cutConfig.CutsFileFullPath, folderRoot);
            cutConfig.OutputFileFullPath = Utils.ResolvePortablePath(cutConfig.OutputFileFullPath, folderRoot);
        }
    }

    private static BareMinimumStlFile CreatePortableExportChild(BareMinimumStlFile child)
    {
        if (child == null)
            return null;

        return new BareMinimumStlFile()
        {
            FullPath = Utils.MakePortablePath(child.FullPath, folderRoot),
            ClearToApplyFullPath = Utils.MakePortablePath(child.ClearToApplyFullPath, folderRoot),
            RepositorySource = Utils.MakePortableRepositorySource(child.RepositorySource, folderRoot),
            ClearRepositorySource = Utils.MakePortableRepositorySource(child.ClearRepositorySource, folderRoot),
            transforms = child.transforms,
            IsImport = child.IsImport,
            OriginalSize = child.OriginalSize,
        };
    }

    
    private async UniTask LoadNewObjectForClear(StlFile treeItem)
    {
        await UniTask.SwitchToMainThread();
        stlImport.Unload(treeItem.SceneKey);
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

}
