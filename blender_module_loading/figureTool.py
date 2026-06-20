#!/usr/bin/python3
import json
import bpy
import sys
import math
from mathutils import Euler, Matrix
import os
from pathlib import Path

UI_STL_MAX_BYTES = 5 * 1024 * 1024

def myPrint(msg):
    print(msg, end="\r\n", flush=True);

def maybe_delete(obj, keep):
  if obj != None:
    bpy.context.view_layer.objects.active = obj
    if keep == False:
      myPrint("deleting " +  obj.name)
      # deselect all objects
      bpy.ops.object.select_all(action='DESELECT')
      # select the object 
      bpy.data.objects[obj.name].select_set(True)
      # delete all selected objects
      bpy.ops.object.delete()
      ##bpy.ops.object.delete()

def get_hidden_status(ob):
    return ob.hide_viewport

def set_hidden_status(ob, value):
    ob.hide_viewport = value
    try:
        bpy.data.collections[ob.name].hide_viewport = value
    except:
        myPrint("blah");
    try:
        bpy.context.view_layer.layer_collection.children[ob.name].hide_viewport = value
    except:
        myPrint("blah");

def export_all_stl(some_collection, dirPathSoFar):
    for a_collection in some_collection.children:
        if a_collection.library != None:
            continue

        if a_collection.name == "base" or a_collection.name == "reference" or a_collection.name == "cuts" or a_collection.name == "clear_reference":
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
    
        os.makedirs(dirPathSoFar, exist_ok =True)
        stl_path = f"{dirPathSoFar}/{filePath}"

        myPrint(stl_path)
        bpy.ops.wm.stl_export(
                filepath=str(stl_path),
                export_selected_objects=True)
        stl_size = os.path.getsize(stl_path)
        if stl_size > UI_STL_MAX_BYTES:
            target_ratio = UI_STL_MAX_BYTES / stl_size
            export_ui_stl_sidecar(ob, stl_path, target_ratio)
        if orig_hide_state:
            ob.hide_set(True)
        ob.select_set(False)

def export_ui_stl_sidecar(source_object, stl_path, target_ratio):
    sidecar_path = Path(stl_path).with_suffix(".ui.stl")
    bpy.ops.object.select_all(action='DESELECT')
    source_object.select_set(True)
    bpy.context.view_layer.objects.active = source_object

    decimate_object(source_object, keep=True, target_ratio=target_ratio)

    myPrint(str(sidecar_path))
    bpy.ops.wm.stl_export(
            filepath=str(sidecar_path),
            export_selected_objects=True)

def unhide_all_collections(children_collection):
    for collection in children_collection:
        #myPrint("unhiding " + collection.name)
        collection.hide_viewport = False
        unhide_all_collections(collection.children)

def export_from_one_blend_file(blendFile, outputDir):
    #myPrint(blendFile)
    bpy.ops.wm.open_mainfile(filepath=blendFile)
    unhide_all_collections(bpy.context.view_layer.layer_collection.children)

    #bpy.context.area.type = '?'
    bpy.ops.object.select_all(action='DESELECT')

    path = Path(outputDir)
    export_all_stl( bpy.context.scene.collection, path)     

def apply_all_transforms(ob):
    mb = ob.matrix_basis
    if hasattr(ob.data, "transform"):
        ob.data.transform(mb)
    for c in ob.children:
        c.matrix_local = mb @ c.matrix_local
    
    ob.matrix_basis.identity()

##Apply a single cut.
def  apply_cut(obj_base, apply_obj, isCut, keep):
  bpy.context.view_layer.objects.active = obj_base

  bpy.ops.object.modifier_add(type = 'BOOLEAN')
  modIndex = len(bpy.context.object.modifiers) - 1
  myPrint("configuring modifier " + apply_obj.name + ", isCut: " + str(isCut))
  bpy.context.object.modifiers[modIndex].show_viewport = False
  bpy.context.object.modifiers[modIndex].object = apply_obj
  if isCut == False:
    bpy.context.object.modifiers[modIndex].operation = 'UNION'
    #bpy.context.object.modifiers[modIndex].solver = 'FAST'
  else:
    bpy.context.object.modifiers[modIndex].operation = 'DIFFERENCE'
    bpy.context.object.modifiers[modIndex].use_hole_tolerant = True

  bpy.context.object.modifiers[modIndex].name = ('mod' + str(modIndex))
  if keep == False:
    bpy.ops.object.modifier_apply(modifier=('mod' + str(modIndex)))
  #myPrint("modifier applied" + apply_obj.name)

def  remesh_object(obj_base, keep):
  bpy.context.view_layer.objects.active = obj_base

  bpy.ops.object.modifier_add(type = 'REMESH')
  modIndex = len(bpy.context.object.modifiers) - 1
  bpy.context.object.modifiers[modIndex].show_viewport = False
  bpy.context.object.modifiers[modIndex].mode = 'VOXEL'
  bpy.context.object.modifiers[modIndex].voxel_size = 0.075

  bpy.context.object.modifiers[modIndex].name = ('mod' + str(modIndex))
  if keep == False:
    bpy.ops.object.modifier_apply(modifier=('mod' + str(modIndex)))
  #myPrint("modifier applied" + apply_obj.name)

def  decimate_object(obj_base, keep, target_ratio):
  bpy.context.view_layer.objects.active = obj_base
  myPrint("decimating with ratio " + str(target_ratio))
  bpy.ops.object.modifier_add(type = 'DECIMATE')
  modIndex = len(bpy.context.object.modifiers) - 1
  bpy.context.object.modifiers[modIndex].show_viewport = keep
  bpy.context.object.modifiers[modIndex].show_render = keep
  bpy.context.object.modifiers[modIndex].decimate_type = 'COLLAPSE'
  bpy.context.object.modifiers[modIndex].ratio = target_ratio

  bpy.context.object.modifiers[modIndex].name = ('mod' + str(modIndex))
  if keep == False:
    bpy.ops.object.modifier_apply(modifier=('mod' + str(modIndex)))
  #myPrint("modifier applied" + apply_obj.name)

def handle_one_json_child(stlfile, combining = False, keep = False):
    #print(stlfile["FullPath"])
    #print(stlfile["ClearToApplyFullPath"])
    #print(output)
    bpy.ops.wm.stl_import(filepath = stlfile["FullPath"])
    obj_base = bpy.context.selected_objects[0]
    origSize = obj_base.dimensions
    newPosition = None
    if "transforms" in stlfile and "PositionRelativeToOriginalSize" in stlfile["transforms"] and len(stlfile["transforms"]["PositionRelativeToOriginalSize"]) > 0:
        newPosition = (origSize.x * (stlfile["transforms"]["PositionRelativeToOriginalSize"][2] / 100),
                                        origSize.y * (stlfile["transforms"]["PositionRelativeToOriginalSize"][0] / -100),
                                        origSize.z * (stlfile["transforms"]["PositionRelativeToOriginalSize"][1] / 100))
        
    #print(newPosition)
    if "transforms" in stlfile and "Scale" in stlfile["transforms"] and len(stlfile["transforms"]["Scale"]) > 0:
        bpy.ops.transform.resize(value=(stlfile["transforms"]["Scale"][2] / 100,
                                        stlfile["transforms"]["Scale"][0] / 100,
                                        stlfile["transforms"]["Scale"][1] / 100))
        apply_all_transforms(obj_base)

    if "transforms" in stlfile and "Rotations" in stlfile["transforms"] and len(stlfile["transforms"]["Rotations"]) > 0:
        bpy.ops.transform.rotate(value=math.radians(stlfile["transforms"]["Rotations"][1]), orient_axis='Z')
        bpy.ops.transform.rotate(value=math.radians(stlfile["transforms"]["Rotations"][2]), orient_axis='X')
        bpy.ops.transform.rotate(value=math.radians(stlfile["transforms"]["Rotations"][0] * -1), orient_axis='Y')
        apply_all_transforms(obj_base)

    if newPosition is not None:
        print(origSize)
        bpy.ops.transform.translate(value=newPosition)
        apply_all_transforms(obj_base)

    if "IsImport" in stlfile and stlfile["IsImport"] == True and combining == True:
        remesh_object(obj_base, keep)
    if "ClearToApplyFullPath" in stlfile and stlfile["ClearToApplyFullPath"] is not None and stlfile["ClearToApplyFullPath"] != "":
        bpy.ops.wm.stl_import(filepath = stlfile["ClearToApplyFullPath"])
        obj_b = bpy.context.selected_objects[0]
        apply_cut(obj_base, obj_b, True, False)

    bpy.ops.object.select_all(action = 'DESELECT')      
    return obj_base

command = sys.argv[1]

""" elif command == "applyClear":
    file_a = sys.argv[2]
    file_b = sys.argv[3]
    output = sys.argv[4]
    bpy.ops.object.select_all(action = 'SELECT')
    bpy.ops.object.delete()

    bpy.ops.wm.stl_import(filepath = file_a)
    obj_base = bpy.context.selected_objects[0]
    bpy.ops.wm.stl_import(filepath = file_b)
    obj_b = bpy.context.selected_objects[0]

    apply_cut(obj_base, obj_b, True, False)
    bpy.ops.object.select_all(action = 'DESELECT')
    obj_base.select_set(True)
    bpy.ops.wm.stl_export(filepath = output,
                            export_selected_objects=True)
elif command == "combineAll":
    index = 1 #sys.argv.index("--")
    file_a = sys.argv[index + 1]
    keep = False
    bpy.ops.object.select_all(action = 'SELECT')
    bpy.ops.object.delete()

    primary_obj = None
    bpy.ops.wm.stl_import(filepath = file_a)
    obj_base = bpy.context.selected_objects[0]
    special_cut = None
    last_file = ""
    for x in range(index + 2, len(sys.argv) - 1):
        currentfile = sys.argv[x]
        myPrint('AGUMENT ' + currentfile)
        if currentfile == '--keep':
            keep = True
            continue
        file_b = currentfile
        isCut = False
        isCuttingNext = False
        if currentfile[0] == '!':
            if currentfile[1] == '!': #!!file.stl means that this is a cutting object that will be applied to the next file before it is applied to the main object
                isCuttingNext = True
                file_b = currentfile[2:]
            else:
                isCut = True
                file_b = currentfile[1:]
        myPrint("importing " + file_b + " isCut: " + str(isCut) + " keep: " + str(keep))
        last_file = os.path.basename(file_b)
        bpy.ops.wm.stl_import(filepath = file_b)
        obj_b = bpy.context.selected_objects[0]
        if isCuttingNext:
            special_cut = obj_b
            continue
        if special_cut != None:
            apply_cut(obj_b, special_cut, True, keep)
    
        apply_cut(obj_base, obj_b, isCut, keep)
        maybe_delete(obj_b, keep)
        if special_cut != None:
            maybe_delete(special_cut, keep)
            special_cut = None

    bpy.ops.object.select_all(action = 'DESELECT')
    file_out = sys.argv[len(sys.argv) - 1]
    myPrint("outputting to " + file_out)
    obj_base.select_set(True)
    if keep == False:
        if last_file == "cuts_0.16.stl" or last_file == "cuts_0.19.stl" or last_file == "cuts_0.22.stl" :
            obj_base.scale = 1.6, 1.6, 1.6
        obj_base.rotation_euler[0] = math.radians(270)
    else:
        bpy.ops.wm.save_as_mainfile(filepath=file_out + '.blend')
    bpy.ops.wm.stl_export(filepath = file_out,
                            export_selected_objects=True)

 """
if command == "exportAll":

    rootdir = sys.argv[2]
    outputdir = sys.argv[3]

    for subdir, dirs, files in os.walk(rootdir):
        for file in files:
            if file.endswith(".blend"):
                sourcefile = os.path.join(subdir, file)
                breadcrumbfile = os.path.join(outputdir, Path(file).stem) + ".breadcrumb"
                if os.path.isfile(breadcrumbfile) == False or (os.path.getmtime(sourcefile) > os.path.getmtime(breadcrumbfile)):
                    #myPrint(os.path.join(subdir, Path(file).stem))
                    export_from_one_blend_file(sourcefile, os.path.join(outputdir, Path(file).stem) )
                    Path(breadcrumbfile).touch()
    

elif command == "applyClearJson":
    jsonfile = sys.argv[2]
    output = sys.argv[3]

    with open(jsonfile, 'r') as file:
        data = json.load(file)

    #print(data)
    bpy.ops.object.select_all(action = 'SELECT')
    bpy.ops.object.delete()
    for stlfile in data["children"]:
        obj_base = handle_one_json_child(stlfile)
        obj_base.select_set(True)
    
    bpy.ops.wm.stl_export(filepath = output,
                            export_selected_objects=True)

elif command == "combineAllJson":
    jsonfile = sys.argv[2]

    with open(jsonfile, 'r') as file:
        data = json.load(file)
    keep = "keep" in data and data["keep"]
    bpy.ops.object.select_all(action = 'SELECT')
    bpy.ops.object.delete()

    primary_obj = None
    for stlfile in data["children"]:
        obj_base = handle_one_json_child(stlfile, combining = True, keep = keep)
        if primary_obj == None:
            primary_obj = obj_base
        else:
            apply_cut(primary_obj, obj_base, isCut = False, keep = keep)
        primary_obj.select_set(True)

    for cuts in data["cutConfigs"]:
        bpy.ops.object.select_all(action = 'DESELECT')
        primary_obj.select_set(True)
        bpy.ops.object.duplicate()
        obj_target = bpy.context.selected_objects[0]
        if "CutsFileFullPath" in cuts and cuts["CutsFileFullPath"] is not None:
            bpy.ops.wm.stl_import(filepath = cuts["CutsFileFullPath"])
            obj_cuts = bpy.context.selected_objects[0]
            apply_cut(obj_target, obj_cuts, isCut=True, keep=keep)

        bpy.ops.object.select_all(action = 'DESELECT')
        
        file_out = sys.argv[len(sys.argv) - 1]
        if "OutputFileFullPath" in cuts and cuts["OutputFileFullPath"] is not None:
            file_out = cuts["OutputFileFullPath"]

        myPrint("outputting to " + file_out)
        obj_target.select_set(True)
        if keep == True:
            bpy.ops.wm.save_as_mainfile(filepath=file_out + '.blend')

        if "ScaleFactor" in cuts and cuts["ScaleFactor"] is not None:
            scale = cuts["ScaleFactor"]
            myPrint("scaling output by " + str(scale))
            obj_target.scale = scale, scale, scale
        obj_target.rotation_euler[0] = math.radians(270)
        bpy.ops.wm.stl_export(filepath = file_out,
                                export_selected_objects=True)
