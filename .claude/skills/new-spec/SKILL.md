---
name: new-spec
description: Use when the user runs /new-spec <feature-name> to scaffold a Timinute design spec in docs/superpowers/specs/ following the house template and spec-first workflow.
disable-model-invocation: true
---

# Scaffold a Timinute design spec

Timinute is spec-first: every non-trivial feature gets a design doc in `docs/superpowers/specs/` **before** implementation, and the roadmap (`docs/superpowers/plans/feature-roadmap.md`) tracks scope. This skill creates that doc from the house template. The argument is a short feature name/slug (e.g. `pomodoro-timer`). If none was given, ask — never invent the feature.

## Steps

1. **Compute the filename.** Date prefix from `Get-Date -Format yyyy-MM-dd`; slug is the kebab-case feature name. Convention (match existing files in `docs/superpowers/specs/`):
   ```
   docs/superpowers/specs/<YYYY-MM-DD>-<slug>-design.md
   ```
   If the feature is part of a versioned release, include the version in the slug (e.g. `2026-07-17-v2.4-support-logging-design.md`). Check the directory first and don't clobber an existing file.

2. **Ask 2–3 scoping questions** before writing (don't guess these): the problem being solved, whether it touches auth/EF schema/the Aurora client, and the target release. Keep it short.

3. **Write the spec** using this template (mirrors the existing specs — a `# Title`, dated status line, then the sections):

   ```markdown
   # <Feature title>

   _Date: <YYYY-MM-DD>_
   _Status: draft — pending review_

   ## Purpose

   <What this delivers and why. One paragraph. If it's a patch/no-behaviour-change, say so.>

   ## Context

   <Current state, relevant existing code paths, constraints. Reference concrete
   files/paths the way the other specs do.>

   ## Scope

   ### In scope
   - <numbered/bulleted concrete changes, grouped by area: Server / Client / Shared / infra>

   ### Out of scope
   - <explicitly deferred items, with where they go instead>

   ## Design

   <The actual approach. Call out anything that intersects the known gotchas:
   ownership checks + FK trim, EF SQL-translation (InMemory vs SQLite), soft-delete
   query filter, dual-scheme auth / keys, Aurora tokens + dark-mode color-scheme,
   UTC → ToLocalTime on the client, AnalyticsService singleton cache.>

   ## Data / schema changes

   <EF entity/migration impact, or "none". If AspNetUsers or table drops are touched,
   note the operator/migration-note implication for the README.>

   ## Testing

   <What proves it works: which tests, and whether any query needs the SQLite test
   helper rather than InMemory. The `verify` skill covers end-to-end app driving.>

   ## Open questions

   - <anything unresolved>
   ```

4. **Offer to link it into the roadmap.** Ask whether to add the feature to the P1/P2 backlog table or tech-debt list in `feature-roadmap.md` — don't edit the roadmap without confirmation.

5. Tell the user the spec is a **draft pending their review**; the next step is an implementation plan in `docs/superpowers/plans/`, not code.
