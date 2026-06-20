# Sample Local Repo

Use this folder with the Unity app's `Add a repository...` flow and choose `Local directory`.

The generated `catalog.json` is already in place, and the STL files are arranged to exercise the category tabs.

To test HTTP repository loading, start the local server from this folder with any Python 3 interpreter you have installed:

```powershell
python serve_repo.py
```

To intentionally slow the STL downloads and make the queue/progress bar easier to see, add a small delay:

```powershell
python serve_repo.py --delay-seconds 1.5
```

To rebuild the catalog, run:
```
python ..\blender_module_loading\jsonBuilder.py . "Sample Local Repo"
```

The generated `Hash` and `UiHash` values are based on file contents, so changing the mesh updates the catalog hashes.

If `python` is not on your PATH, `py -3 serve_repo.py` works on many Windows setups too.

Then paste the printed `catalog.json` URL into the Unity app's repository URL dialog.
