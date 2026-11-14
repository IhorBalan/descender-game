# Shared by tools/hooks/pre-push and tools/unity/run-editor.sh: finds the Unity
# editor for this project, starts it without it ever taking the foreground on
# Windows, stops it, and takes the machine-wide lock that keeps to one editor run
# at a time. Set unity_editor_dir to the folder holding this file, then source
# it with '. "$unity_editor_dir/unity-editor.sh"' from the project root. Nothing
# runs until a function is called.
#
# On Windows the editor is always started through unity-background.ps1 (next to
# this file): on a desktop of its own that nobody looks at, with its windows
# hidden as well. Started with a plain "&" it opens a console window that takes
# the keyboard and mouse from whatever is in use, a game for one, and any dialog
# it or a helper process shows would do the same. UNITY_FOREGROUND=1 (or the
# older PRE_PUSH_FOREGROUND=1) starts it directly instead, for debugging only.

: "${unity_editor_dir:?set unity_editor_dir before sourcing unity-editor.sh}"
unity=""
unity_pid=""
unity_lock_taken=""

# find_unity: sets $unity to the editor named in ProjectSettings/ProjectVersion.txt
# under the Unity Hub's install folder, or to UNITY_EDITOR. Fails with a message.
find_unity() {
    version=$(sed -n 's/^m_EditorVersion: *//p' ProjectSettings/ProjectVersion.txt | tr -d '\r')
    if [ -z "$version" ]; then
        echo "unity: cannot read the editor version from ProjectSettings/ProjectVersion.txt." >&2
        return 1
    fi
    unity="${UNITY_EDITOR:-}"
    if [ -z "$unity" ]; then
        for candidate in \
            "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity" \
            "$HOME/Unity/Hub/Editor/$version/Editor/Unity" \
            "/opt/unity/editors/$version/Editor/Unity" \
            "/c/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe" \
            "C:/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"
        do
            if [ -x "$candidate" ]; then
                unity="$candidate"
                break
            fi
        done
    fi
    if [ -z "$unity" ] || [ ! -x "$unity" ]; then
        echo "unity: Unity $version was not found. Install it through Unity Hub or set UNITY_EDITOR." >&2
        return 1
    fi
}

# start_unity <unity arguments...>: starts the editor in the background and sets
# unity_pid. On Windows that is the pid of unity-background.ps1, which exits with
# the editor's exit code and takes the editor and its helpers down when killed.
start_unity() {
    case "$(uname -s)" in
        MINGW* | MSYS* | CYGWIN*) direct="${UNITY_FOREGROUND:-${PRE_PUSH_FOREGROUND:-}}" ;;
        *) direct=1 ;;
    esac
    if [ -n "$direct" ]; then
        "$unity" "$@" &
    else
        powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass \
            -File "$unity_editor_dir/unity-background.ps1" "$unity" "$@" &
    fi
    unity_pid=$!
}

# stop_unity: stops the editor at once and clears what a stopped editor leaves
# behind: its lock, which the next run would take for an open editor, and the
# PlayMode bootstrap scene. On Windows kill alone only sends the editor a Ctrl-C,
# after which it spends up to a minute shutting down (and crashing on the way),
# so its process tree is ended with taskkill first.
stop_unity() {
    if [ -z "$unity_pid" ]; then
        return
    fi
    if [ -r "/proc/$unity_pid/winpid" ]; then
        taskkill //F //T //PID "$(cat "/proc/$unity_pid/winpid")" >/dev/null 2>&1
    fi
    kill "$unity_pid" 2>/dev/null
    wait "$unity_pid" 2>/dev/null
    unity_pid=""
    # A killed editor on Windows holds its lock for a moment longer.
    for attempt in 1 2 3 4 5; do
        rm -f Temp/UnityLockfile Assets/InitTestScene*.unity Assets/InitTestScene*.unity.meta 2>/dev/null && break
        sleep 1
    done
}

# The machine-wide lock: one editor run at a time, whichever checkout it is in.
unity_lock_dir() {
    echo "${TEMP:-${TMPDIR:-/tmp}}/descender-unity.lock"
}

# take_unity_lock <who>: waits for the lock (checking every 30 s, for at most
# UNITY_LOCK_WAIT seconds, 2400 by default) and takes it. A lock older than two
# hours is taken to be left behind by a run that died, and is taken over.
take_unity_lock() {
    lock=$(unity_lock_dir)
    waited=0
    limit="${UNITY_LOCK_WAIT:-2400}"
    while ! mkdir "$lock" 2>/dev/null; do
        if [ -n "$(find "$lock" -maxdepth 0 -mmin +120 2>/dev/null)" ]; then
            echo "unity: the lock at $lock is over two hours old; taking it over." >&2
            rm -rf "$lock"
            continue
        fi
        if [ "$waited" -ge "$limit" ]; then
            echo "unity: another Unity run still holds $lock after $waited s ($(cat "$lock/owner.txt" 2>/dev/null))." >&2
            return 1
        fi
        if [ "$waited" -eq 0 ]; then
            echo "unity: waiting for the Unity run holding $lock ($(cat "$lock/owner.txt" 2>/dev/null))."
        fi
        sleep 30
        waited=$((waited + 30))
    done
    echo "$1 (pid $$, $(date '+%Y-%m-%d %H:%M:%S'))" >"$lock/owner.txt"
    unity_lock_taken=1
}

# release_unity_lock: removes the lock if this shell took it.
release_unity_lock() {
    if [ -n "$unity_lock_taken" ]; then
        rm -rf "$(unity_lock_dir)"
        unity_lock_taken=""
    fi
}
