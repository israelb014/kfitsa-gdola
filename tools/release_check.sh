#!/bin/bash
# Usage: tools/release_check.sh ; every test on the final Linux build, after the three platform builds.
cd "$(dirname "$0")/.."; V=$PWD/verification/v1.3; B=Builds/linux/KfitsaGdola.x86_64; export PULSE_SERVER=/nonexistent
sed 's#Builds/test-linux#Builds/linux#' tools/playtest.sh > /tmp/claude-1000/playtest_final.sh
cp /tmp/claude-1000/playtest_final.sh tools/.playtest_final.sh; bash tools/.playtest_final.sh | cut -c1-120; rm -f tools/.playtest_final.sh
timeout -s KILL 120 $B -batchmode -nographics -selftest -captureDir $V -logFile $V/self.log > /dev/null 2>&1; grep -E "^FAIL|ALL PASS|FAILED" $V/selftest.txt | cut -c1-110 | head
for s in 1920x1080 2340x1080 2400x1080; do
  w=${s%x*}; h=${s#*x}; mkdir -p $V/t$w
  timeout -s KILL 170 $B -touchtest -screen-fullscreen 0 -screen-width $w -screen-height $h -captureDir $V/t$w -logFile $V/touch.log > /dev/null 2>&1
  echo "touch $s: pass $(grep -c ^PASS $V/t$w/touch_test.txt) fail $(grep -c ^FAIL $V/t$w/touch_test.txt)"; grep ^FAIL $V/t$w/touch_test.txt | cut -c1-110 | head -4
done
timeout -s KILL 60 $B -inputtest -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -captureDir $V/i -logFile $V/input.log > /dev/null 2>&1; sed -n 3,12p $V/i/input_test.txt | cut -c1-80
