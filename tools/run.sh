#!/bin/bash
# Usage: tools/run.sh <step> <Method> [Unity args] ; runs one Unity batch step through the heavy-job lock.
cd "$(dirname "$0")/.."
~/work/_tools/heavy.sh /home/ib/Unity/Hub/Editor/6000.6.8f1/Editor/Unity -batchmode -quit -projectPath . -executeMethod Kfitsa.EditorTools.BuildTools.$2 -logFile Logs/$1.log "${@:3}"
r=$?; echo "$1 exit=$r"; grep -E "error CS|Exception|SETUP OK|BUILD |AUDIO " Logs/$1.log | sort -u | head -20 | cut -c1-220; exit $r
