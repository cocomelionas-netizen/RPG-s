#!/usr/bin/env bash
# Finds Unity APIs that are obsolete in Unity 6.6 (the CI's version), which CI fails on but the reference DLLs used by
# check.sh / editor-check.sh still accept. Prints each hit with its replacement; exits 1 when there is any.
cd "$(dirname "$0")/../.."
status=0
check() {
  # $1: regex, $2: what to use instead
  local hits
  hits=$(grep -rnE --include=*.cs "$1" Assets/_Project/Scripts Assets/_Project/Tests 2>/dev/null | grep -vE '^\S+:[0-9]+:\s*//')
  if [ -n "$hits" ]; then
    echo "Obsolete in Unity 6.6 — use $2:"
    echo "$hits" | sed 's/^/  /'
    status=1
  fi
}
check '\.GetInstanceID\(\)' 'GetEntityId() or GetHashCode()'
check '\bFindObjectsOfType\b|\bFindObjectOfType\b' 'FindObjectsByType / FindFirstObjectByType'
check '\.usedByComposite\b' 'compositeOperation'
check '\.isKinematic\b' 'bodyType = RigidbodyType2D.Kinematic'
check '\bbody\.velocity\b|\.velocity\s*=' 'linearVelocity'
check '\.angularDrag\b|\.drag\s*=' 'linearDamping / angularDamping'
check '\.spritesheet\b' 'the Sprite Editor data provider (or Sprite.Create sub-assets)'
check 'InstanceIDToObject\b' 'EntityIdToObject'
[ $status -eq 0 ] && echo "No obsolete Unity 6.6 APIs."
exit $status
