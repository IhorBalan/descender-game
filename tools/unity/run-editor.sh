#!/bin/sh
# Runs the Unity editor in batch mode on this checkout, the one way to start
# Unity from a script or an agent apart from the pre-push hook.
#
#   sh tools/unity/run-editor.sh <unity arguments...>
#   sh tools/unity/run-editor.sh -executeMethod Descender.EditorTools.Foo.Run
#   sh tools/unity/run-editor.sh -runTests -testPlatform EditMode -testResults Logs/edit.xml
#
# It finds the editor the way the pre-push hook does (unity-editor.sh), adds
# -batchmode, -projectPath <this checkout>, -logFile Logs/run-editor.log and
# --burst-disable-compilation unless they are given (or another --burst-
# option is): Burst would compile the packages' jobs to unsigned DLLs, and on
# Windows Smart App Control blocks each one with a notification and its sound.
# It waits for the machine-wide Unity lock and holds it for
# the run, and exits with the editor's exit code. On Windows the editor runs on a
# desktop of its own with its windows hidden, so it cannot take the keyboard and
# mouse from a game being played; never start Unity.exe directly there.
#
# UNITY_LOCK_HELD=1 skips the lock when the caller already holds it.
# UNITY_LOCK_WAIT=<seconds> is how long to wait for it (2400 by default).

set -u

root=$(git rev-parse --show-toplevel) || exit 1
cd "$root" || exit 1

unity_editor_dir="$root/tools/hooks"
# shellcheck source=../hooks/unity-editor.sh
. "$unity_editor_dir/unity-editor.sh"

if [ $# -eq 0 ]; then
    echo "usage: sh tools/unity/run-editor.sh <unity arguments...>" >&2
    exit 2
fi

find_unity || exit 1

batchmode=1
project=1
logfile=1
burst=1
for arg in "$@"; do
    case "$arg" in
        -batchmode) batchmode="" ;;
        -projectPath | -projectpath) project="" ;;
        -logFile | -logfile) logfile="" ;;
        --burst-*) burst="" ;;
    esac
done

mkdir -p Logs
if [ -n "$logfile" ]; then
    set -- "$@" -logFile Logs/run-editor.log
fi
if [ -n "$project" ]; then
    set -- -projectPath "$root" "$@"
fi
if [ -n "$burst" ]; then
    set -- --burst-disable-compilation "$@"
fi
if [ -n "$batchmode" ]; then
    set -- -batchmode "$@"
fi

if [ -z "${UNITY_LOCK_HELD:-}" ]; then
    take_unity_lock "run-editor.sh in $root" || exit 1
fi
trap 'stop_unity; release_unity_lock; exit 1' INT TERM
trap 'release_unity_lock' EXIT

if [ -f Temp/UnityLockfile ]; then
    echo "run-editor: the project is open in the Unity editor; close it first." >&2
    exit 1
fi

echo "run-editor: $unity $*"
start_unity "$@"
wait "$unity_pid"
code=$?
unity_pid=""
# An editor that aborted leaves its lock behind; ours has exited.
rm -f Temp/UnityLockfile
echo "run-editor: the editor exited with $code."
exit $code
