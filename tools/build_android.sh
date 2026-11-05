#!/bin/bash
# Usage: tools/build_android.sh ; passwords are read from outside the repository.
cd "$(dirname "$0")/.."
set -a; . ~/work/_secrets/kfitsa-gdola-android.env; set +a
exec tools/run.sh Android BuildAndroid -buildTarget Android
