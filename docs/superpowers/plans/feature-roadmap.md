# Timinute Feature Roadmap

## Current Feature Set

- **Auth:** Registration, JWT auth via Duende IdentityServer, roles (Basic/Admin), lockout
- **Projects:** Full CRUD, user-scoped, pagination
- **Task Tracking:** Full CRUD, real-time stopwatch timer, session storage persistence, manual entry
- **Calendar:** Scheduler view (Day/Week/Month) with add/edit forms
- **Analytics:** Project work time, monthly aggregation, top project stats, donut + column charts
- **Data Access:** Generic repository pattern with paging, filtering, dynamic LINQ ordering

## P0: Must-Have (Security & Data Integrity)

| Feature | Description | Complexity |
|---------|-------------|------------|
| Data validation | Add Required/MaxLength/Range attributes to DTOs, validate EndDate > StartDate, duration > 0 | S |
| Analytics optimization | Replace LINQ-to-Objects with DB-side queries (Dapper or raw SQL) for analytics endpoints | M |
| Timer edge cases | Handle overlapping tasks, impossible durations, better session persistence | S |
| Soft delete | Add IsArchived flag to tasks/projects instead of hard delete, with recovery | M |

## P1: Should-Have (Productivity & UX)

| Feature | Description | Complexity | Dependencies |
|---------|-------------|------------|--------------|
| Data export | CSV/PDF export for tracked tasks and analytics | M | None |
| Advanced filtering | Date range API filters, full-text search on task names, filter persistence | M | None |
| Tags/Labels | Tag model with many-to-many Tag-Task relationship, UI for tagging | M | None |
| Enhanced analytics | Custom date ranges, daily/weekly summaries, productivity trends | M | Analytics optimization |
| Settings/Preferences | User profile page, timezone config, workday hours, dark mode preference | M | None |
| Notifications | Idle time warnings, task reminders via SignalR or browser push | M | None |
| Time tracking enhancements | Pomodoro timer, time estimates/goals, break tracking | L | None |

## P2: Nice-to-Have (Advanced & Team)

| Feature | Description | Complexity | Dependencies |
|---------|-------------|------------|--------------|
| Team workspaces | Project sharing, team dashboards, task assignment | L | Settings |
| Calendar integration | Google Calendar / Outlook sync | L | None |
| Jira/GitHub integration | Link tasks to external tickets | L | Tags |
| Audit logging | Track all changes for compliance | M | None |
| Reporting suite | Scheduled email reports, custom report builder | L | Data export |
| AI categorization | Automatic task categorization and productivity recommendations | L | Tags |

## Dependency Graph

```
P0 (Security/Integrity) ── foundation for everything
│
├─ P1 (UX)
│  ├─ Advanced Filtering (independent)
│  ├─ Data Export (independent)
│  ├─ Tags/Labels (independent)
│  ├─ Settings (independent)
│  ├─ Enhanced Analytics ← depends on Analytics optimization
│  ├─ Notifications (independent)
│  └─ Time Tracking Enhancements (independent)
│
└─ P2 (Advanced)
   ├─ Team Workspaces ← depends on Settings
   ├─ Integrations ← benefits from Tags
   ├─ Reporting ← depends on Data Export
   └─ AI Features ← depends on Tags
```

## Technical Debt to Address

- Constants class growing large — split per domain
- Add request/response logging middleware
- Add DB indexes on UserId, ProjectId for query performance
- Add composite indexes for common analytics queries
- Add unique constraint: project names per user
- API versioning for future breaking changes
- Extract common DataGrid logic into reusable component
