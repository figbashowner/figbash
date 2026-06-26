"""Build a repository catalog JSON from a folder tree.

Usage:
    python create_repo_catalog.py ROOT_DIR [REPOSITORY_NAME] [-o CATALOG_PATH]

The generated JSON mirrors the Unity Folder/StlFile tree structure:
- folders are preserved recursively
- regular STL assets are catalogued, with SHA256 hashes of the file contents for both the main STL and any matching .ui.stl sidecar recorded on the regular entry
- generated catalog/breadcrumb files are ignored
- paths are written relative to ROOT_DIR using forward slashes
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys


IGNORED_DIR_NAMES = {"tempClears", "__pycache__"}
IGNORED_FILE_SUFFIXES = {".json", ".breadcrumb"}
INCLUDED_FILE_SUFFIXES = {".stl"}


def to_posix_relative(path, root):
    relative = os.path.relpath(str(path), str(root))
    if relative == ".":
        return ""
    return Path(relative).as_posix()


def regular_name_for_ui_sidecar(file_name):
    lower = file_name.lower()
    if not lower.endswith(".ui.stl"):
        return None
    return file_name[:-7] + ".stl"


def sha256_file_hex(path):
    digest = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def build_folder(path, root):
    folder = {
        "Name": path.name,
        "FullPath": to_posix_relative(path, root),
    }

    subdirs = []
    files = []
    ui_sidecars = {}
    regular_files = []

    with os.scandir(path) as scan:
        entries = sorted(scan, key=lambda entry: entry.name.lower())

    for entry in entries:
        if entry.name.startswith("."):
            continue

        if entry.is_dir(follow_symlinks=False):
            if entry.name in IGNORED_DIR_NAMES:
                continue

            child = build_folder(Path(entry.path), root)
            if child is not None:
                subdirs.append(child)
            continue

        if not entry.is_file(follow_symlinks=False):
            continue

        if entry.name.lower().endswith(".ui.stl"):
            regular_name = regular_name_for_ui_sidecar(entry.name)
            if regular_name is not None:
                ui_sidecars[regular_name.lower()] = entry
            continue

        suffix = Path(entry.name).suffix.lower()
        if suffix not in INCLUDED_FILE_SUFFIXES:
            continue
        if suffix in IGNORED_FILE_SUFFIXES:
            continue
        if entry.name.lower().startswith("cuts_"):
            continue

        regular_files.append(
            (
                entry,
                {
                    "Name": entry.name,
                    "FullPath": to_posix_relative(Path(entry.path), root),
                    "SelectionCanChange": entry.name.lower() != "base.stl",
                    "Hash": sha256_file_hex(Path(entry.path)),
                },
            )
        )

    for entry, file_entry in regular_files:
        ui_entry = ui_sidecars.get(entry.name.lower())
        if ui_entry is not None:
            file_entry["UiName"] = ui_entry.name
            file_entry["UiHash"] = sha256_file_hex(Path(ui_entry.path))

        files.append(file_entry)

    if subdirs:
        folder["Subdirs"] = subdirs
    if files:
        folder["Files"] = files

    if path == root or subdirs or files:
        return folder
    return None


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description="Build a repository catalog JSON from a folder tree."
    )
    parser.add_argument("root_dir", help="Folder to scan for repository assets.")
    parser.add_argument(
        "repo_name",
        nargs="?",
        help="Display name for the catalog root. Defaults to the root folder name.",
    )
    parser.add_argument(
        "-o",
        "--output",
        dest="output_path",
        help="Output catalog path. Defaults to <root_dir>/catalog.json.",
    )
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)

    root_dir = Path(args.root_dir).expanduser().resolve()
    if not root_dir.exists() or not root_dir.is_dir():
        raise SystemExit(
            f"Root directory does not exist or is not a directory: {root_dir}"
        )

    repo_name = args.repo_name or root_dir.name
    output_path = (
        Path(args.output_path).expanduser().resolve()
        if args.output_path
        else root_dir / "catalog.json"
    )

    catalog = build_folder(root_dir, root_dir)
    catalog["Name"] = repo_name
    catalog["FullPath"] = ""

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=4, ensure_ascii=False)
        f.write("\n")

    return output_path


if __name__ == "__main__":
    main()
