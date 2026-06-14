"""Build a repository catalog JSON from a folder tree.

Usage:
    python jsonBuilder.py ROOT_DIR [REPOSITORY_NAME] [-o CATALOG_PATH]

The generated JSON mirrors the Unity Folder/StlFile tree structure:
- folders are preserved recursively
- only STL assets are catalogued
- generated catalog/breadcrumb files are ignored
- paths are written relative to ROOT_DIR using forward slashes
"""

import argparse
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


def build_folder(path, root):
    folder = {
        "Name": path.name,
        "FullPath": to_posix_relative(path, root),
    }

    subdirs = []
    files = []

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

        suffix = Path(entry.name).suffix.lower()
        if suffix not in INCLUDED_FILE_SUFFIXES:
            continue
        if suffix in IGNORED_FILE_SUFFIXES:
            continue
        if entry.name.lower().startswith("cuts_"):
            continue

        files.append(
            {
                "Name": entry.name,
                "FullPath": to_posix_relative(Path(entry.path), root),
                "SelectionCanChange": entry.name.lower() != "base.stl",
            }
        )

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
