#!/bin/bash
# Usage: tools/audio_android.sh ; plays the built APK on the emulator and fails if sound does not work there.
cd "$(dirname "$0")/.."; V=$PWD/verification/v2.4; mkdir -p "$V"; R=$V/audio_android.txt
export PATH=~/work/_tools/android-emu/platform-tools:$PATH
ADB=$(command -v adb); P=com.ibfix.kfitsagdola; A=$P/.GameMain
export ANDROID_SDK_ROOT=~/work/_tools/android-emu ANDROID_AVD_HOME=~/work/_tools/android-emu/avd
# The emulator's host GPU sometimes crashes when the game is launched again; then it is booted afresh
boot() {
  ps -eo pid,comm | awk '$2 ~ /^qemu-system/ {print $1}' | xargs -r kill; sleep 3
  setsid nohup ~/work/_tools/heavy.sh $ANDROID_SDK_ROOT/emulator/emulator -avd s24 -no-window -no-boot-anim -no-snapshot -gpu host > ~/work/_tools/android-emu/emu.log 2>&1 &
  STARTED=1
  timeout 240 "$ADB" wait-for-device || exit 1
  for i in $(seq 80); do [ "$(timeout 10 "$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ] && break; sleep 3; done
  [ "$(timeout 10 "$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ] || { echo "FAIL emulator did not boot"; exit 1; }
  sleep 15
}
[ "$("$ADB" get-state 2>/dev/null)" != device ] && boot
# No adb call may wait forever if the emulator dies mid-test
adb() { timeout 30 "$ADB" "$@"; }
"$ADB" get-state > /dev/null 2>&1 || { echo "FAIL emulator not running"; exit 1; }
alive() { "$ADB" get-state > /dev/null 2>&1; }
# Before each part: the game fully stopped, and a fresh emulator (with the game installed) if the last one died
fresh() {
  if ! "$ADB" get-state > /dev/null 2>&1; then
    echo "(the emulator died; booting it again)" | tee -a "$R"; boot
    timeout 180 "$ADB" install Builds/android/KfitsaGdola-android.apk > /dev/null || fail "install"
  fi
  adb shell am force-stop $P; sleep 2
}
fail() { echo "FAIL $1" | tee -a "$R"; bad=1; }
: > "$R"; bad=0
adb uninstall $P > /dev/null 2>&1; timeout 180 "$ADB" install Builds/android/KfitsaGdola-android.apk > /dev/null || fail "install"

# 1. The same audio test as on the desktop, inside the APK
adb logcat -c
adb shell am start -n $A -e unity "-audiotest" > /dev/null
for i in $(seq 90); do alive || break; sleep 1; adb logcat -d -s Unity:I | grep -qE "AUDIO (ALL PASS|[0-9]+ FAILED)" && break; done
adb logcat -d -s Unity:I | grep -E " AUDIO " | sed 's/.* AUDIO //' > "$V/audio_android_test.txt"
grep -vE "^PASS (clip|effect|music)" "$V/audio_android_test.txt" | cut -c1-90 | tee -a "$R"
grep -q "^ALL PASS" "$V/audio_android_test.txt" || fail "audio test in the APK"
sleep 3

fresh
# 1b. The sandstorm test inside the APK, with pictures of the real screen while it runs
# The emulator has died during this part; a death gets one more try on a freshly booted emulator
storm() {
  adb logcat -c
  adb shell am start -n $A -e unity "-stormtest" > /dev/null
  for i in $(seq 1 18); do alive || return; sleep 2; adb exec-out screencap -p > "$V/android_storm_$i.png"; done
  for i in $(seq 120); do alive || return; adb logcat -d -s Unity:I | grep -qE "STORM (ALL PASS|[0-9]+ FAILED)" && break; sleep 1; done
}
storm; alive || { fresh; storm; }
adb logcat -d -s Unity:I | grep -E " STORM " | sed 's/.* STORM //' > "$V/storm_android_test.txt"
grep -vE "^PASS shelter" "$V/storm_android_test.txt" | cut -c1-110 | tee -a "$R"
grep -q "^ALL PASS" "$V/storm_android_test.txt" || fail "storm test in the APK"
sleep 3

fresh
# 2. A normal launch: the phone must see the game playing on the media stream, and it must stop and come back
players() { adb shell dumpsys audio | grep -E "AudioPlaybackConfiguration.*u/pid:$U/" | grep -v "state:idle" | grep -v "state:released"; }
U=$(adb shell cmd package list packages -U $P | sed 's/.*uid://' | tr -d '\r')
adb shell input keyevent KEYCODE_HOME; sleep 2
adb shell am start -n $A > /dev/null; sleep 14
adb shell dumpsys audio | grep -E "u/pid:$U/" | cut -c1-170 | tee -a "$R"
adb shell dumpsys audio | grep -E "u/pid:$U/" | grep -q "state:started" || fail "no playing audio stream at the title screen"
adb shell dumpsys audio | grep -E "u/pid:$U/" | grep "state:started" | grep -qE "usage=USAGE_(MEDIA|GAME)|stream=STREAM_MUSIC" || fail "stream is not media"

vol() { adb shell cmd media_session volume --stream 3 --get | grep -oE "volume is [0-9]+" | grep -oE "[0-9]+"; }
v0=$(vol)
adb shell cmd audio set-volume 3 5 > /dev/null; sleep 2     # media_session --set stopped taking effect on this emulator
v1=$(vol); adb shell input keyevent KEYCODE_VOLUME_UP; sleep 2; v2=$(vol)
echo "media volume $v1, after the volume up key $v2" | tee -a "$R"
[ "$v2" -gt "$v1" ] || fail "volume key does not change the media volume"
adb shell input keyevent KEYCODE_BACK; sleep .3
[ -n "$v0" ] && adb shell cmd audio set-volume 3 "$v0" > /dev/null

adb logcat -c
adb shell input keyevent KEYCODE_HOME; sleep 4
echo "--- in the background" | tee -a "$R"
adb shell dumpsys audio | grep -E "u/pid:$U/" | cut -c1-170 | tee -a "$R"
adb shell dumpsys audio | grep -E "u/pid:$U/" | grep -q "state:started" && fail "still playing in the background"
adb logcat -d -s Unity:I | grep -q "AUDIO paused" || fail "no pause logged"
adb shell am start -n $A > /dev/null; sleep 6
echo "--- back in the game" | tee -a "$R"
adb shell dumpsys audio | grep -E "u/pid:$U/" | cut -c1-170 | tee -a "$R"
adb shell dumpsys audio | grep -E "u/pid:$U/" | grep -q "state:started" || fail "did not resume"
adb logcat -d -s Unity:I | grep -q "AUDIO resumed" || fail "no resume logged"

# 3. A new game through the real menus, on a clean install: new game, easy, the intro, the map
adb exec-out screencap -p > "$V/android_menu_title.png"
tap() { adb shell input tap "$1" "$2"; sleep 2; }
W=$(adb shell wm size | tail -1 | sed 's/.*: //;s/\r//' | cut -dx -f1); H=$(adb shell wm size | tail -1 | sed 's/.*: //;s/\r//' | cut -dx -f2)
[ "$W" -lt "$H" ] && { t=$W; W=$H; H=$t; }
S=$((H*1000/1080))
row() { tap $((W/2)) $((H/2-$1*S/1000)); }
row 90;  adb exec-out screencap -p > "$V/android_menu_difficulty.png"
row 40;  adb exec-out screencap -p > "$V/android_menu_intro.png"
row -300; adb exec-out screencap -p > "$V/android_menu_map.png"



[ -n "$STARTED" ] && { adb shell input keyevent KEYCODE_HOME; adb emu kill > /dev/null; }
[ $bad = 0 ] && echo "ANDROID AUDIO OK" | tee -a "$R" || echo "ANDROID AUDIO FAILED" | tee -a "$R"
exit $bad
