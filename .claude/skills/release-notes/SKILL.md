---
name: release-notes
description: Use when the user runs /release-notes <version> to draft release notes for a Timinute release from the git history, EF migrations, and specs since the previous tag.
disable-model-invocation: true
---

# Draft Timinute release notes

Produce release notes for a tagged Timinute release. The argument is the target version (e.g. `v2.4`). If none was given, ask — never invent it.

Timinute releases are tagged `v*`; the tag triggers `release.yml` (platform packages) and `latest`/semver Docker images. Notes must call out anything an operator upgrading a live instance has to do — that is the whole point of the "migration note" convention in the README.

## Steps

1. **Find the range.** Previous tag: `git describe --tags --abbrev=0` (or `git tag --list "v*" | sort -V | tail`). The range is `<prevTag>..HEAD` (or `..<targetTag>` if it already exists). State the range you used.

2. **Gather the raw material** across that range:
   - Merged PRs / commits: `git log <prevTag>..HEAD --no-merges --pretty="%s"` and `git log <prevTag>..HEAD --merges --pretty="%s"`. Group by Conventional-Commit prefix (`feat`, `fix`, `chore`, `docs`).
   - **New EF migrations:** `git diff --name-only <prevTag>..HEAD -- Timinute/Server/Data/Migrations/`. Every new migration is a potential operator action — open each and note table/column changes, especially anything touching `AspNetUsers` or dropping/renaming tables.
   - **Specs shipped:** new files in `docs/superpowers/specs/` in range give the intent behind features; the roadmap header (`docs/superpowers/plans/feature-roadmap.md`) records what the last release was.
   - Package bumps: `git diff <prevTag>..HEAD -- "*.csproj"` for notable dependency changes (Duende, EF Core, Radzen).

3. **Draft the notes** in this shape (Markdown, ready for a GitHub Release body):
   - One-line summary of the release theme.
   - **Highlights** — user-facing `feat`s in plain language, not commit subjects.
   - **Fixes** — notable `fix`es.
   - **⚠️ Upgrade / migration notes** — REQUIRED whenever a migration alters `AspNetUsers`, drops/renames tables, or a config/env var changed (`IdentityServer__Authority`, `/keys` volume, connection string). Match the format of the existing `v2.0`/`v2.1` migration notes in the README "Production deployment" section. Docker auto-migrates via `DatabaseMigrationOnStartup=true` — say so, and spell out anything manual. If there are genuinely no operator actions, state that explicitly.
   - **Dependencies** — notable bumps.

4. **Cross-check the README.** If the release includes a migration touching `AspNetUsers` or table drops/renames and the README has no matching migration note yet, offer to add one (same format as the v2.0/v2.1 notes).

5. **Output only the draft** for the user to review — do not create a git tag or a GitHub release. Note that `git log`/PR titles are the source, so a thin commit history yields thin notes; flag anything you couldn't determine from the range.
