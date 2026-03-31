# Timinute Modernization Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade Timinute from .NET 8 to .NET 10, update all packages, verify functionality, expand test coverage, redesign UI, and plan new features.

**Architecture:** Blazor WebAssembly hosted app (Server + Client + Shared). Repository pattern with generic factory. IdentityServer for auth. Radzen component library for UI. xUnit + Moq + EF InMemory for tests.

**Tech Stack:** .NET 10, Blazor WASM, EF Core, SQL Server, Radzen.Blazor, Duende IdentityServer, xUnit, Moq

---

## Phase 1: Upgrade to .NET 10

> Branch: `feature/upgrade_to_net_10` off `develop`

The existing `feature/upgrade_project_to_net_9` branch has useful reference (Duende IdentityServer migration, async test fixes) but we'll do a clean upgrade from `develop` directly to .NET 10.

### Task 1.1: Upgrade Target Frameworks

**Files:**
- Modify: `Timinute/Shared/Timinute.Shared.csproj`
- Modify: `Timinute/Server/Timinute.Server.csproj`
- Modify: `Timinute/Client/Timinute.Client.csproj`
- Modify: `Timinute/Server.Tests/Timinute.Server.Tests.csproj`

- [ ] **Step 1: Create feature branch**

```bash
git checkout develop
git checkout -b feature/upgrade_to_net_10
```

- [ ] **Step 2: Update Shared project TFM**

In `Timinute/Shared/Timinute.Shared.csproj`, change:
```xml
<TargetFramework>net8.0</TargetFramework>
```
to:
```xml
<TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 3: Update Server project TFM**

In `Timinute/Server/Timinute.Server.csproj`, change:
```xml
<TargetFramework>net8.0</TargetFramework>
```
to:
```xml
<TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 4: Update Client project TFM**

In `Timinute/Client/Timinute.Client.csproj`, change:
```xml
<TargetFramework>net8.0</TargetFramework>
```
to:
```xml
<TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 5: Update Test project TFM**

In `Timinute/Server.Tests/Timinute.Server.Tests.csproj`, change:
```xml
<TargetFramework>net8.0</TargetFramework>
```
to:
```xml
<TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 6: Verify solution builds**

```bash
dotnet build Timinute.sln
```
Expected: Build errors related to outdated package versions (will fix in Task 1.2).

- [ ] **Step 7: Commit TFM changes**

```bash
git add Timinute/Shared/Timinute.Shared.csproj Timinute/Server/Timinute.Server.csproj Timinute/Client/Timinute.Client.csproj Timinute/Server.Tests/Timinute.Server.Tests.csproj
git commit -m "chore: update target framework to .NET 10"
```

### Task 1.2: Replace IdentityServer4 with Duende IdentityServer

`Microsoft.AspNetCore.ApiAuthorization.IdentityServer` is removed in .NET 10. Must migrate to Duende IdentityServer.

**Files:**
- Modify: `Timinute/Server/Timinute.Server.csproj`
- Modify: `Timinute/Server/Program.cs`
- Modify: `Timinute/Server/Data/ApplicationDbContext.cs`
- Modify: `Timinute/Server.Tests/Timinute.Server.Tests.csproj`
- Modify: `Timinute/Server.Tests/Helpers/ControllerTestBase.cs`

- [ ] **Step 1: Update Server.csproj packages**

Remove the old IdentityServer package reference:
```xml
<PackageReference Include="Microsoft.AspNetCore.ApiAuthorization.IdentityServer" Version="7.0.20" />
```

Add Duende packages:
```xml
<PackageReference Include="Duende.IdentityServer" Version="7.2.*" />
<PackageReference Include="Duende.IdentityServer.AspNetIdentity" Version="7.2.*" />
<PackageReference Include="Duende.IdentityServer.EntityFramework" Version="7.2.*" />
```

- [ ] **Step 2: Update Server Program.cs**

Replace `AddApiAuthorization` with Duende IdentityServer configuration. Replace:
```csharp
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
```
with:
```csharp
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
```

Update the IdentityServer registration in Program.cs to use Duende's API:
```csharp
builder.Services.AddIdentityServer()
    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();
```

- [ ] **Step 3: Update ApplicationDbContext**

Change base class from `ApiAuthorizationDbContext` to Duende's equivalent. Update using statements accordingly.

- [ ] **Step 4: Update Test project packages**

Add Duende packages to `Server.Tests.csproj` and update `ControllerTestBase.cs` to use Duende's operational store options.

- [ ] **Step 5: Build and verify**

```bash
dotnet build Timinute.sln
```
Expected: Successful build.

- [ ] **Step 6: Run tests**

```bash
dotnet test
```
Expected: All existing tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: migrate from IdentityServer4 to Duende IdentityServer"
```

---

## Phase 2: Update All NuGet Packages

### Task 2.1: Update Server Packages

**Files:**
- Modify: `Timinute/Server/Timinute.Server.csproj`

- [ ] **Step 1: Check latest package versions**

```bash
cd Timinute/Server && dotnet list package --outdated
```

- [ ] **Step 2: Update all packages to latest stable**

Update each `<PackageReference>` to the latest stable version. Key packages to update:
- `Microsoft.AspNetCore.Components.WebAssembly.Server` → 10.0.*
- `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore` → 10.0.*
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` → 10.0.*
- `Microsoft.AspNetCore.Identity.UI` → 10.0.*
- `Microsoft.EntityFrameworkCore.SqlServer` → 10.0.*
- `Microsoft.EntityFrameworkCore.Tools` → 10.0.*
- `Microsoft.VisualStudio.Web.CodeGeneration.Design` → latest
- `AutoMapper` → latest stable
- `Swashbuckle.AspNetCore` → latest stable
- `System.Linq.Dynamic.Core` → latest stable

- [ ] **Step 3: Build server project**

```bash
dotnet build Timinute/Server/Timinute.Server.csproj
```
Expected: Successful build. Fix any breaking API changes.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server/Timinute.Server.csproj
git commit -m "chore: update server NuGet packages"
```

### Task 2.2: Update Client Packages

**Files:**
- Modify: `Timinute/Client/Timinute.Client.csproj`

- [ ] **Step 1: Check latest package versions**

```bash
cd Timinute/Client && dotnet list package --outdated
```

- [ ] **Step 2: Update all packages**

Key packages:
- `Microsoft.AspNetCore.Components.WebAssembly` → 10.0.*
- `Microsoft.AspNetCore.Components.WebAssembly.DevServer` → 10.0.*
- `Microsoft.AspNetCore.Components.WebAssembly.Authentication` → 10.0.*
- `Microsoft.Extensions.Http` → 10.0.*
- `Radzen.Blazor` → latest stable (major version upgrade — check breaking changes)
- `Blazored.SessionStorage` → latest stable
- `System.Linq.Dynamic.Core` → latest stable

- [ ] **Step 3: Fix Radzen breaking changes**

Radzen 5.x → 6.x+ has breaking changes. Check all Razor files using Radzen components:
- `Components/Dashboard/DoughnutChart.razor`
- `Components/Dashboard/ProjectColumnChart.razor`
- `Components/TrackedTasks/TrackedTaskTable.razor` (RadzenPager)
- Any forms using Radzen input components

- [ ] **Step 4: Build client project**

```bash
dotnet build Timinute/Client/Timinute.Client.csproj
```

- [ ] **Step 5: Commit**

```bash
git add Timinute/Client/Timinute.Client.csproj
git commit -m "chore: update client NuGet packages"
```

### Task 2.3: Update Test Packages

**Files:**
- Modify: `Timinute/Server.Tests/Timinute.Server.Tests.csproj`

- [ ] **Step 1: Update test packages**

Key packages:
- `Microsoft.NET.Test.Sdk` → latest
- `xunit` → latest stable
- `xunit.runner.visualstudio` → latest stable
- `Moq` → latest stable
- `Microsoft.EntityFrameworkCore.InMemory` → 10.0.*
- `coverlet.collector` → latest stable

- [ ] **Step 2: Fix async test signatures**

Change `async void` to `async Task` in:
- `Repositories/ProjectRepositoryTest.cs` (lines 39, 54, 69)
- `Repositories/TrackedTaskRepositoryTest.cs` (lines 44, 58, 72, 86)

- [ ] **Step 3: Run tests**

```bash
dotnet test
```
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server.Tests/Timinute.Server.Tests.csproj Timinute/Server.Tests/Repositories/
git commit -m "chore: update test NuGet packages and fix async signatures"
```

---

## Phase 3: Verify Functionality

### Task 3.1: Build and Test Verification

- [ ] **Step 1: Clean build**

```bash
dotnet clean Timinute.sln && dotnet build Timinute.sln
```
Expected: 0 errors, 0 warnings (or known warnings only).

- [ ] **Step 2: Run full test suite**

```bash
dotnet test --verbosity normal
```
Expected: All tests pass.

- [ ] **Step 3: Check for runtime issues**

```bash
dotnet run --project Timinute/Server/Timinute.Server.csproj
```
Manually verify:
- App starts on https://localhost:7047
- Login page renders
- Swagger UI at /swagger loads
- No console errors

- [ ] **Step 4: Verify database connectivity**

Ensure Docker SQL Server is running, then verify EF migrations apply:
```bash
dotnet ef database update --project Timinute/Server/Timinute.Server.csproj
```

- [ ] **Step 5: Commit any fixes**

```bash
git add -A
git commit -m "fix: resolve .NET 10 upgrade compatibility issues"
```

---

## Phase 4: Extend Unit Test Coverage

> Branch: continue on `feature/upgrade_to_net_10` or create `feature/test_coverage` off it

### Task 4.1: Add Repository Delete Tests

**Files:**
- Modify: `Timinute/Server.Tests/Repositories/ProjectRepositoryTest.cs`
- Modify: `Timinute/Server.Tests/Repositories/TrackedTaskRepositoryTest.cs`

- [ ] **Step 1: Write failing delete test for ProjectRepository**

```csharp
[Fact]
public async Task Delete_Existing_Project_Test()
{
    var project = await _repository.Find("ProjectId1");
    Assert.NotNull(project);

    await _repository.Delete(project);

    var deleted = await _repository.Find("ProjectId1");
    Assert.Null(deleted);
}
```

- [ ] **Step 2: Run test to verify it passes**

```bash
dotnet test --filter "FullyQualifiedName~ProjectRepositoryTest.Delete_Existing_Project_Test"
```

- [ ] **Step 3: Write delete-by-id test**

```csharp
[Fact]
public async Task Delete_Project_By_Id_Test()
{
    await _repository.Delete("ProjectId1");

    var deleted = await _repository.Find("ProjectId1");
    Assert.Null(deleted);
}
```

- [ ] **Step 4: Write delete tests for TrackedTaskRepository**

Same pattern as above for TrackedTask entities.

- [ ] **Step 5: Run all repository tests**

```bash
dotnet test --filter "FullyQualifiedName~RepositoryTest"
```
Expected: All pass.

- [ ] **Step 6: Commit**

```bash
git add Timinute/Server.Tests/Repositories/
git commit -m "test: add repository delete tests"
```

### Task 4.2: Add Missing Analytics Controller Tests

**Files:**
- Modify: `Timinute/Server.Tests/Controllers/AnalyticsControllerTest.cs`

- [ ] **Step 1: Add test for GetWorkTimePerMonths endpoint**

This endpoint (AnalyticsController line 127) has no test coverage. Write a test that:
- Seeds tracked tasks across multiple months
- Calls GetWorkTimePerMonths
- Asserts correct monthly aggregation and "yyyy MMM" format

- [ ] **Step 2: Add test for empty data scenario**

Test analytics endpoints when user has no tracked tasks — should return empty collections, not errors.

- [ ] **Step 3: Run analytics tests**

```bash
dotnet test --filter "FullyQualifiedName~AnalyticsControllerTest"
```

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server.Tests/Controllers/AnalyticsControllerTest.cs
git commit -m "test: add missing analytics controller tests"
```

### Task 4.3: Add Input Validation Tests

**Files:**
- Modify: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`
- Modify: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Add null/empty input tests for ProjectController**

Test CreateProject with null name, UpdateProject with empty ID, etc.

- [ ] **Step 2: Add date/duration validation tests for TrackedTaskController**

Test CreateTrackedTask with:
- Zero duration
- Negative duration
- Future start dates (if disallowed)
- Missing project ID

- [ ] **Step 3: Run all controller tests**

```bash
dotnet test --filter "FullyQualifiedName~ControllerTest"
```

- [ ] **Step 4: Commit**

```bash
git add Timinute/Server.Tests/Controllers/
git commit -m "test: add input validation tests for controllers"
```

### Task 4.4: Add Authorization Edge Case Tests

**Files:**
- Modify: `Timinute/Server.Tests/Controllers/ProjectControllerTest.cs`
- Modify: `Timinute/Server.Tests/Controllers/TrackedTaskControllerTest.cs`

- [ ] **Step 1: Test accessing another user's resources**

Verify that User1 cannot update/delete User2's projects or tasks. ProjectController already has `Update_Project_Another_User_Test` — add equivalent for:
- Delete another user's project
- Update another user's tracked task
- Delete another user's tracked task

- [ ] **Step 2: Run tests**

```bash
dotnet test
```

- [ ] **Step 3: Commit**

```bash
git add Timinute/Server.Tests/Controllers/
git commit -m "test: add cross-user authorization tests"
```

---

## Phase 5: UI and Visual Redesign (Planning)

> This phase produces a design document, not code. Implementation will be a separate plan.

### Task 5.1: Audit Current UI

- [ ] **Step 1: Document current pages and layout**

Review and document:
- `Client/Shared/MainLayout.razor` — current layout structure
- `Client/Shared/NavMenu.razor` — navigation items
- `Client/Pages/` — all page routes and their purpose
- `Client/Components/` — all reusable components
- `Client/wwwroot/css/` — current styling approach

- [ ] **Step 2: Identify UI pain points**

List current limitations:
- Mobile responsiveness
- Theme/dark mode support
- Component consistency
- Navigation UX
- Dashboard information density
- Form design patterns

- [ ] **Step 3: Write UI redesign spec**

Create `docs/superpowers/plans/ui-redesign-spec.md` with:
- Current state screenshots/descriptions
- Proposed layout changes
- Component library upgrade plan (Radzen 6.x+ features)
- Color scheme / theming approach
- Mobile-first responsive design goals
- Accessibility improvements

- [ ] **Step 4: Commit spec**

```bash
git add docs/superpowers/plans/ui-redesign-spec.md
git commit -m "docs: add UI redesign specification"
```

---

## Phase 6: Feature Extension Planning

> This phase produces a feature roadmap document. Implementation will be separate plans per feature.

### Task 6.1: Audit Existing Features

- [ ] **Step 1: Map current feature set**

Document what currently exists:
- Project CRUD (create, list, edit, delete)
- Tracked task CRUD with timer
- Task scheduler/calendar view
- Dashboard with analytics (doughnut chart, column chart)
- Monthly work time analytics
- Per-project work time breakdown
- User authentication and authorization

- [ ] **Step 2: Identify gaps and improvement areas**

Review controllers, UI, and user flows for:
- Missing CRUD operations or incomplete flows
- Data export capabilities
- Reporting depth
- Multi-user / team features
- Notification system
- Search and filtering
- Settings/preferences

- [ ] **Step 3: Write feature roadmap**

Create `docs/superpowers/plans/feature-roadmap.md` with prioritized feature list:
- **P0 (Must-have):** Bug fixes, incomplete features, data integrity
- **P1 (Should-have):** User-requested improvements, productivity features
- **P2 (Nice-to-have):** Advanced analytics, integrations, team features

Each feature entry should include:
- Description
- User value
- Estimated complexity (S/M/L)
- Dependencies on other features

- [ ] **Step 4: Commit roadmap**

```bash
git add docs/superpowers/plans/feature-roadmap.md
git commit -m "docs: add feature extension roadmap"
```

---

## Execution Order

Phases 1-3 are sequential (upgrade → packages → verify). Phase 4 can start after Phase 3 passes. Phases 5 and 6 are independent research tasks that can run in parallel with Phase 4.

```
Phase 1 (TFM + IdentityServer) → Phase 2 (Packages) → Phase 3 (Verify)
                                                              ↓
                                                    ┌─────────┼─────────┐
                                                    ↓         ↓         ↓
                                                Phase 4    Phase 5   Phase 6
                                                (Tests)    (UI Spec) (Features)
```
