#!/bin/bash
# Usage: tools/itch_push.sh ; pushes the last official builds to itch.io with butler (the user logs in to butler himself).
# Channels: linux, windows, android. The version comes from the build settings.
cd "$(dirname "$0")/.."; B=~/work/_tools/butler/butler; T=israelb014/kfitsa-gdola
V=$(grep -o 'bundleVersion = "[0-9.]*"' Assets/Scripts/Editor/BuildTools.cs | head -1 | grep -o '[0-9.]*')
set -e
timeout 1800 $B push Builds/linux $T:linux --userversion "$V" --ignore '*BackUpThisFolder*' --ignore 'verification'
timeout 1800 $B push Builds/windows $T:windows --userversion "$V" --ignore '*BackUpThisFolder*'
timeout 1800 $B push Builds/android/KfitsaGdola-android.apk $T:android --userversion "$V"
timeout 120 $B status $T
