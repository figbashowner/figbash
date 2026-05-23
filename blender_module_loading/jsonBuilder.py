import json
import os
from pathlib import Path
import sys

outputdir = sys.argv[1]

def getRelative(subPath, parent):
    return '/'.join(os.path.relpath(subPath, parent).split('\\'))

def getFolderObj(path):
    dirEntry = {}
    dirEntry["Name"] = os.path.basename(path)
    with os.scandir(path) as it:
        for entry in it:
            if not entry.name.startswith('.'):
                if os.path.isdir(entry.path):
                    if not "Subdirs" in dirEntry:
                        dirEntry["Subdirs"] = []
                    dirEntry["Subdirs"].append(getFolderObj(entry.path))
                elif os.path.isfile(entry.path) and not entry.name.split(".")[-1] == "json" and not entry.name.split(".")[-1] == "breadcrumb":
                    if not "Files" in dirEntry:
                        dirEntry["Files"] = []
                    dirEntry["Files"].append({
                        "Name": entry.name,
                        "FullPath": getRelative(entry.path, outputdir)
                        })
    return dirEntry

rootObj = getFolderObj(outputdir)
rootObj["Name"] = sys.argv[2]
jsonfile = os.path.join(outputdir, "catalog.json")
with open(jsonfile, 'w') as f:
    json.dump(rootObj, f, indent=4)

"""
pathsToRoot = [outputdir]
for subdir, dirs, files in os.walk(outputdir):
    if subdir == outputdir:
        continue
    mostRecentPath = pathsToRoot[0]
    relative = getRelative(subdir, mostRecentPath);
    print(relative + "," + mostRecentPath)
    if mostRecentPath != relative:
        pathsToRoot.insert(0, subdir);
    while relative[0] == '.':
        pathsToRoot.pop();
        relative = '/'.join(os.path.relpath(subdir, pathsToRoot[0]).split('\\'))
    print(relative)
    for file in files:
        sourcefile = os.path.join(subdir, file)
        print("\t" + file)
"""