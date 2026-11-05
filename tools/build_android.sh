#!/bin/bash
# Usage: tools/build_android.sh ; passwords are read from outside the repository. Fails if sound does not work on the emulator.
cd "$(dirname "$0")/.."
set -a; . ~/work/_secrets/kfitsa-gdola-android.env; set +a
tools/run.sh Android BuildAndroid -buildTarget Android || exit 1
unset KFITSA_KEYSTORE_PASS
exec tools/audio_android.sh
