#!/bin/bash
# Usage: tools/audio_android.sh ; plays the built APK on the emulator and fails if sound does not work there.
cd "$(dirname "$0")/.."; V=$PWD/verification/v1.2.1; mkdir -p "$V"; R=$V/audio_android.txt
export PATH=~/work/_tools/android-emu/platform-tools:$PATH
P=com.ibfix.kfitsagdola; A=$P/.GameMain
if [ "$(adb get-state 2>/dev/null)" != device ]; then
  export ANDROID_SDK_ROOT=~/work/_tools/android-emu ANDROID_AVD_HOME=~/work/_tools/android-emu/avd
  setsid nohup ~/work/_tools/heavy.sh $ANDROID_SDK_ROOT/emulator/emulator -avd s24 -no-window -no-boot-anim -no-snapshot -gpu host > ~/work/_tools/android-emu/emu.log 2>&1 &
  STARTED=1
  timeout 240 adb wait-for-device || exit 1
  until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 3; done; sleep 15
fi
fail() { echo "FAIL $1" | tee -a "$R"; bad=1; }
: > "$R"; bad=0
adb install -r Builds/android/KfitsaGdola-android.apk > /dev/null || fail "install"

# 1. The same audio test as on the desktop, inside the APK
adb logcat -c
adb shell am start -n $A -e unity "-audiotest" > /dev/null
for i in $(seq 90); do sleep 1; adb logcat -d -s Unity:I | grep -qE "AUDIO (ALL PASS|[0-9]+ FAILED)" && break; done
adb logcat -d -s Unity:I | grep -E " AUDIO " | sed 's/.* AUDIO //' > "$V/audio_android_test.txt"
grep -vE "^PASS (clip|effect|music)" "$V/audio_android_test.txt" | cut -c1-90 | tee -a "$R"
grep -q "^ALL PASS" "$V/audio_android_test.txt" || fail "audio test in the APK"
sleep 3

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
adb shell cmd media_session volume --stream 3 --set 5 > /dev/null; sleep 2
v1=$(vol); adb shell input keyevent KEYCODE_VOLUME_UP; sleep 2; v2=$(vol)
echo "media volume $v1, after the volume up key $v2" | tee -a "$R"
[ "$v2" -gt "$v1" ] || fail "volume key does not change the media volume"
adb shell input keyevent KEYCODE_BACK; sleep .3
[ -n "$v0" ] && adb shell cmd media_session volume --stream 3 --set "$v0" > /dev/null

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

[ -n "$STARTED" ] && { adb shell input keyevent KEYCODE_HOME; adb emu kill > /dev/null; }
[ $bad = 0 ] && echo "ANDROID AUDIO OK" | tee -a "$R" || echo "ANDROID AUDIO FAILED" | tee -a "$R"
exit $bad
