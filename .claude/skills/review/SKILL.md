---
name: review
description: Review the project (or a part of it) and list the findings without changing anything. Use when the owner asks for a review, an audit, a list of problems or "what should be fixed".
---

# Review: findings only

1. **Do not edit, create or delete any file, and do not commit or push.** This is a report.
2. Split the review across parallel agents by area (each told the same: read-only), for example:
   - gameplay and networking (server authority, spawn/despawn, persistence);
   - UI and WebGL performance (per-frame allocations, canvas rebuilds, layout);
   - scene builder and data generators (`Assets/_Project/Scripts/Editor`, `Tools/`);
   - Unity 6.6 compatibility (`bash Tools/CompileCheck/deprecations.sh`, APIs newer than the reference DLLs);
   - the rule that the source game's name appears nowhere but the data URL in CLAUDE.md (`git grep -i`).
3. Keep only findings verified by reading the code paths; drop speculation.
4. Answer the owner in Lithuanian with one numbered list, grouped by severity (high / medium / low), each with
   `file:line`, what is wrong, when it fails, and the suggested fix. Also list what was checked and found fine.
5. End by asking which findings to fix. Fix nothing until the owner says so.
