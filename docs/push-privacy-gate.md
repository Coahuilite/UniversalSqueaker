# Push privacy gate

The scanner is adapted from the series' Mwah gate as of 2026-10-08. It scans
tracked text and names, commit/tag messages and author/committer identities,
reachable historical text, structured binary metadata and runtime-derived
personal tokens. Repository-specific history exemptions are the independently
reviewed 55 blob+path+`personal-path` tuples in
`scripts/privacy-history-exceptions.json`. A matching filename is not enough:
the scanner resolves each historical revision/file to its blob before accepting
it, and compares blob, path and pattern with case-sensitive literal equality
(Git paths are exact identities; a case-only path spelling is a different
path). Current-tree text, filenames, messages, identities, credentials,
metadata and runtime anonymity stay fully checked. A modified blob, different
path or different pattern is not covered. Git resolution failures fail the scan.

Run once in each clone: pwsh -NoProfile -File scripts/install-hooks.ps1.
The pre-push hook automatically runs privacy-audit.ps1 -FullHistory. A dedicated
GitHub workflow runs the same audit with fetch-depth: 0 on every push and PR,
including version branches. Scanner success is a privacy result, not gameplay,
build or release acceptance. Existing build checks retain their own scope.

For a local review: pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory.
Stage newly added files before review so tracked-file scans include them; the
hook checks the resulting commit again before transmission. Default current-tree
success is not FullHistory success. -PrePush adds release-oriented
clean-tree/main/tag reporting and is not required for a normal version-branch
push. Output suppresses matched sensitive values. Historical exceptions never
waive current-tree scanning. A newly produced blob is not covered by the
accepted inventory. The 2026-10-08 maintainer decision preserves the existing
history and continues development; history rewrite remains a separate choice.

Image pixels and compressed/opaque binary content are not fully covered by
metadata scanning. Inspect tracked images before pushing. Historical binary
metadata is not covered by the historical text vector. Findings do not authorize
history rewriting; resolve genuine leaks and classify false positives explicitly.

Scope for this change: privacy automation only. No product payload, release,
tag or settings data is produced or changed by the scanner or hook installer.
