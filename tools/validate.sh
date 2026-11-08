#!/bin/bash
# Usage: tools/validate.sh [level] ; checks the level files on disk with the last test build, no Unity rebuild needed.
cd "$(dirname "$0")/.."
python3 tools/make_levels.py Assets/Resources/Levels > /dev/null || exit 1
timeout -s KILL 900 Builds/test-linux/KfitsaGdola.x86_64 -batchmode -nographics -validate ${1:+-level $1} ${2:+-seconds $2} -levelsDir Assets/Resources/Levels -captureDir $PWD/verification/v1.7 -logFile $PWD/verification/v1.7/validate.log > verification/v1.7/validate.out
r=$?; grep -E "^(FAIL|ALL|[0-9]+ PROBLEMS)|cannot reach" verification/v1.7/reach_report.txt | cut -c1-110; echo "exit $r"
