#!/usr/bin/python3
"""Export STL assets from Blender scene collections.

Usage:
    python blender_export.py ROOT_DIR OUTPUT_DIR
"""

import math
import os
from pathlib import Path
import sys

import bpy


UI_STL_MAX_BYTES = 5 * 1024 * 1024


def myPrint(msg):
    print(msg, end="\r\n", flush=True)


def set_hidden_status(ob, value):
    ob.hide_viewport = value
    try:
        bpy.data.collections[ob.name].hide_viewport = value
    except Exception:
        myPrint("blah")
    try:
        bpy.context.view_layer.layer_collection.children[ob.name].hide_viewport = value
    except Exception:
        myPrint("blah")


def export_ui_stl_sidecar(source_object, stl_path, target_ratio):
    sidecar_path = Path(stl_path).with_suffix(".ui.stl")
    bpy.ops.object.select_all(action="DESELECT")
    source_object.select_set(True)
    bpy.context.view_layer.objects.active = source_object

    decimate_object(source_object, keep=True, target_ratio=target_ratio)

    myPrint(str(sidecar_path))
    bpy.ops.wm.stl_export(
        filepath=str(sidecar_path),
        export_selected_objects=True,
    )


def export_all_stl(some_collection, dirPathSoFar):
    for a_collection in some_collection.children:
        if a_collection.library is not None:
            continue

        if (
            a_collection.name == "base"
            or a_collection.name == "reference"
            or a_collection.name == "cuts"
            or a_collection.name == "clear_reference"
        ):
            continue
        set_hidden_status(a_collection, False)

        nextPath = f"{dirPathSoFar}/{a_collection.name}"
        export_all_stl(a_collection, nextPath)

    for ob in some_collection.objects:
        if ob.type != "MESH":
            continue

        orig_hide_state = ob.hide_get()
        if orig_hide_state:
            ob.hide_set(False)
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        filePath = f"{ob.name}.stl"

        os.makedirs(dirPathSoFar, exist_ok=True)
        stl_path = f"{dirPathSoFar}/{filePath}"

        myPrint(stl_path)
        bpy.ops.wm.stl_export(
            filepath=str(stl_path),
            export_selected_objects=True,
        )

        stl_size = os.path.getsize(stl_path)
        if stl_size > UI_STL_MAX_BYTES:
            target_ratio = UI_STL_MAX_BYTES / stl_size
            export_ui_stl_sidecar(ob, stl_path, target_ratio)
        if orig_hide_state:
            ob.hide_set(True)
        ob.select_set(False)


def unhide_all_collections(children_collection):
    for collection in children_collection:
        collection.hide_viewport = False
        unhide_all_collections(collection.children)


def export_from_one_blend_file(blendFile, outputDir):
    bpy.ops.wm.open_mainfile(filepath=blendFile)
    unhide_all_collections(bpy.context.view_layer.layer_collection.children)

    bpy.ops.object.select_all(action="DESELECT")

    path = Path(outputDir)
    export_all_stl(bpy.context.scene.collection, path)


def apply_all_transforms(ob):
    mb = ob.matrix_basis
    if hasattr(ob.data, "transform"):
        ob.data.transform(mb)
    for c in ob.children:
        c.matrix_local = mb @ c.matrix_local

    ob.matrix_basis.identity()


def remesh_object(obj_base, keep):
    bpy.context.view_layer.objects.active = obj_base

    bpy.ops.object.modifier_add(type="REMESH")
    modIndex = len(bpy.context.object.modifiers) - 1
    bpy.context.object.modifiers[modIndex].show_viewport = False
    bpy.context.object.modifiers[modIndex].mode = "VOXEL"
    bpy.context.object.modifiers[modIndex].voxel_size = 0.075

    bpy.context.object.modifiers[modIndex].name = ("mod" + str(modIndex))
    if keep is False:
        bpy.ops.object.modifier_apply(modifier=("mod" + str(modIndex)))


def decimate_object(obj_base, keep, target_ratio):
    bpy.context.view_layer.objects.active = obj_base
    myPrint("decimating with ratio " + str(target_ratio))
    bpy.ops.object.modifier_add(type="DECIMATE")
    modIndex = len(bpy.context.object.modifiers) - 1
    bpy.context.object.modifiers[modIndex].show_viewport = keep
    bpy.context.object.modifiers[modIndex].show_render = keep
    bpy.context.object.modifiers[modIndex].decimate_type = "COLLAPSE"
    bpy.context.object.modifiers[modIndex].ratio = target_ratio

    bpy.context.object.modifiers[modIndex].name = ("mod" + str(modIndex))
    if keep is False:
        bpy.ops.object.modifier_apply(modifier=("mod" + str(modIndex)))


def _get_script_args(argv):
    args = argv if argv is not None else sys.argv
    if "--" in args:
        args = args[args.index("--") + 1 :]
    else:
        args = args[1:]

    if args and args[0] == "exportAll":
        args = args[1:]

    return args


def main(argv=None):
    args = _get_script_args(sys.argv if argv is None else argv)
    if len(args) != 2:
        raise SystemExit("Usage: blender_export.py ROOT_DIR OUTPUT_DIR")

    rootdir, outputdir = args

    for subdir, dirs, files in os.walk(rootdir):
        for file in files:
            if file.endswith(".blend"):
                sourcefile = os.path.join(subdir, file)
                breadcrumbfile = os.path.join(outputdir, Path(file).stem) + ".breadcrumb"
                if os.path.isfile(breadcrumbfile) is False or (
                    os.path.getmtime(sourcefile) > os.path.getmtime(breadcrumbfile)
                ):
                    export_from_one_blend_file(
                        sourcefile, os.path.join(outputdir, Path(file).stem)
                    )
                    Path(breadcrumbfile).touch()


if __name__ == "__main__":
    main()
