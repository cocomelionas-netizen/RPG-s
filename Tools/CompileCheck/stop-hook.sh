#!/usr/bin/env bash
# Claude Code Stop hook: before a turn ends, look for APIs obsolete in Unity 6.6 (deprecations.sh). Blocks the stop
# (exit 2, the hits on stderr) only when there are real hits, and never twice in a row or when the script is missing.
input=$(cat)
case "$input" in *'"stop_hook_active":true'*|*'"stop_hook_active": true'*) exit 0 ;; esac
script="$(dirname "$0")/deprecations.sh"
[ -f "$script" ] || exit 0
bash "$script" >&2
[ $? -eq 1 ] && exit 2
exit 0
