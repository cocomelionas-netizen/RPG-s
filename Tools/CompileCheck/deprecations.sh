#!/usr/bin/env bash
# Finds Unity APIs that are obsolete in Unity 6.6 (the CI's version), which CI fails on but the reference DLLs used by
# check.sh / editor-check.sh still accept. Comments are ignored. Prints each hit with its replacement; exits 1 when there
# is any, 0 when there is none.
cd "$(dirname "$0")/../.."
exec python3 -I - <<'PY'
import os, re, sys

RULES = [
    (r'(?<![\w.])GetInstanceID\s*\(|\.GetInstanceID\s*\(', 'GetEntityId() or GetHashCode()'),
    (r'\bFindObjectsOfType\b|\bFindObjectOfType\b', 'FindObjectsByType / FindFirstObjectByType'),
    (r'\.usedByComposite\b', 'compositeOperation'),
    (r'\.isKinematic\b', 'bodyType = RigidbodyType2D.Kinematic'),
    (r'\.velocity\b', 'linearVelocity (Rigidbody2D)'),
    (r'\.angularDrag\b|\b(?:body|rb|rigidbody|Rigidbody2D)\.drag\b', 'linearDamping / angularDamping'),
    (r'\.spritesheet\b', 'the Sprite Editor data provider (or Sprite.Create sub-assets)'),
    (r'\bInstanceIDToObject\b', 'EntityIdToObject'),
]
compiled = [(re.compile(p), fix) for p, fix in RULES]

def code_only(text):
    """The text without // and /* */ comments or string literals (kept as empty strings), line breaks kept."""
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        if text.startswith('//', i):
            while i < n and text[i] != '\n':
                i += 1
        elif text.startswith('/*', i):
            end = text.find('*/', i + 2)
            end = n if end < 0 else end + 2
            out.append('\n' * text.count('\n', i, end))
            i = end
        elif c == '"' or c == "'":
            quote = c
            out.append(quote)
            i += 1
            while i < n and text[i] != quote and text[i] != '\n':
                i += 2 if text[i] == '\\' else 1
            out.append(quote)
            i += 1
        else:
            out.append(c)
            i += 1
    return ''.join(out)

hits = {}
for root in ('Assets/_Project/Scripts', 'Assets/_Project/Tests'):
    for folder, _, files in os.walk(root):
        for name in files:
            if not name.endswith('.cs'):
                continue
            path = os.path.join(folder, name)
            lines = code_only(open(path, encoding='utf-8', errors='replace').read()).split('\n')
            for number, line in enumerate(lines, 1):
                for pattern, fix in compiled:
                    if pattern.search(line):
                        hits.setdefault(fix, []).append(f'{path}:{number}: {line.strip()}')
for fix, places in hits.items():
    print(f'Obsolete in Unity 6.6 - use {fix}:')
    for place in places:
        print('  ' + place)
if not hits:
    print('No obsolete Unity 6.6 APIs.')
sys.exit(1 if hits else 0)
PY
