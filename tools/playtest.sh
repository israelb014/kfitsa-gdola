#!/bin/bash
# Usage: tools/playtest.sh [levels] ; plays every level with the last test build, one level at a time.
cd "$(dirname "$0")/.."; V=$PWD/verification/v1.6
for L in ${@:-w1l1 w1l2 w1l3 w2l1 w2l2 w2l3 boss w3l1}; do
  timeout -s KILL 900 Builds/test-linux/KfitsaGdola.x86_64 -batchmode -nographics -playtest -level $L -captureDir $V -logFile $V/play.log > /dev/null
  cp $V/playtest.txt $V/playtest_$L.txt
  echo "$L pass $(grep -c ^PASS $V/playtest.txt) fail $(grep -c ^FAIL $V/playtest.txt)"; grep -E "^FAIL" $V/playtest.txt | cut -c1-110; grep "played start" $V/playtest.txt | cut -c1-100
done
