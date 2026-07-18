# Task 0 — MCP SDK spike findings

**Package pinned:** `ModelContextProtocol.AspNetCore` **2.0.0-preview.3** (net10.0 target lib; transitively
pulls `ModelContextProtocol` 2.0.0-preview.3, `ModelContextProtocol.Core` 2.0.0-preview.3,
`Microsoft.Extensions.AI.Abstractions` 10.5.2). Resolved via
`dotnet add package ModelContextProtocol.AspNetCore --prerelease` from `Timinute/Server`; pinned exactly
(no wildcard) in `Timinute/Server/Timinute.Server.csproj`.

**Method:** Cloned `github.com/modelcontextprotocol/csharp-sdk` at tag `v2.0.0-preview.3` (exact commit
`0d34048e5fc0a2e5a579a2d48a17308083a50028`, matching the resolved NuGet build) and read the actual
implementation (not just XML docs, which are ambiguous on filter scope — see fact (a) below). Where the
SDK's own test suite already exercises the exact behavior we need, at the exact pinned commit, that is cited
as first-party runtime proof instead of re-deriving a WebApplicationFactory harness. A throwaway console
project (`.superpowers/sdd/spike/`, deleted before this task closes) compiled the exact snippets below
against the resolved package to prove the API shape.

---

## Fact (a) — call-tool filter registration API

**Verdict: TRUE (with a correction to the plan's phrasing).**

The registration API is exactly the two candidates named in R0, used **together**, not as alternatives:

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithRequestFilters(rf => rf.AddCallToolFilter(next => async (request, cancellationToken) =>
    {
        var toolName = request.Params?.Name ?? string.Empty;   // tool name is known here
        var scoped = request.Services!.GetRequiredService<ISomeScopedService>();

        if (/* should short-circuit */ false)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = "Refused." }],
            };
        }

        return await next(request, cancellationToken);        // otherwise call through
    }));
```

- `WithRequestFilters(this IMcpServerBuilder, Action<IMcpRequestFilterBuilder> configure)` — extension on
  `IMcpServerBuilder` (`McpServerBuilderExtensions.cs`).
- `AddCallToolFilter(this IMcpRequestFilterBuilder, McpRequestFilter<CallToolRequestParams, CallToolResult> filter)`
  — extension on `IMcpRequestFilterBuilder` (`McpRequestFilterBuilderExtensions.cs`). Internally it does
  `builder.Services.Configure<McpServerOptions>(o => o.Filters.Request.CallToolFilters.Add(filter))` — a
  plain delegate added to a list, **not** a DI-activated type.
- Delegate shapes (both are delegates, confirmed no interface exists):
  ```csharp
  public delegate McpRequestHandler<TParams, TResult> McpRequestFilter<TParams, TResult>(
      McpRequestHandler<TParams, TResult> next);
  public delegate ValueTask<TResult> McpRequestHandler<TParams, TResult>(
      RequestContext<TParams> request, CancellationToken cancellationToken);
  ```
  For call-tool, `TParams = CallToolRequestParams` (has `.Name`, the tool name) and `TResult = CallToolResult`
  (has `.IsError` / `.Content` for a short-circuit response).
- **Confirmed: there is no `IMcpServerToolInvocationFilter` interface anywhere in the SDK**
  (`grep -rn IMcpServerToolInvocationFilter` across the full cloned source at this tag: zero matches).

**Correction to the plan's assumption:** the XML doc comment on `McpRequestFilters.CallToolFilters` says
these filters "wrap handlers that are invoked when a client makes a call to a tool that isn't found in the
`McpServerTool` collection" — read in isolation this sounds like CallToolFilters are a fallback path that
**skips** DI-registered `[McpServerToolType]` tools. That reading is wrong. Reading
`McpServerImpl.ConfigureTools` (`src/ModelContextProtocol.Core/Server/McpServerImpl.cs:1402-1428`) shows the
actual wiring: the DI-tool-dispatch handler (`if (request.MatchedPrimitive is McpServerTool tool) return
tool.InvokeAsync(...)`) is the **innermost** handler; the registered `CallToolFilters` wrap *around* it; and
`BuildInitialCallToolFilter` (which sets `request.MatchedPrimitive` from the tool-name lookup) wraps
*around all of that* as the true outermost layer. So a registered `AddCallToolFilter` **does** run for
every `tools/call`, including calls that resolve to an attribute-based DI tool — it is a true global
call-tool middleware, not a fallback-only hook. This is what Task 8's activity-log + scope-gate filter needs,
and it works as originally hoped.

---

## Fact (b) — `WithToolsFromAssembly()` + scoped DI lifetimes

**Verdict: TRUE.**

`WithToolsFromAssembly()` → `WithTools(types, ...)` → for each `[McpServerTool]` instance method, registers:
```csharp
services => McpServerTool.Create(toolMethod, static r => CreateTarget(r.Services, typeof(TToolType)),
    new() { Services = services, SerializerOptions = serializerOptions })
```
(`McpServerBuilderExtensions.cs:47-49`). The factory `r => CreateTarget(r.Services, type)` runs **on every
invocation** (`r` is the per-call `RequestContext<CallToolRequestParams>`), and
`CreateTarget` is `ActivatorUtilities.CreateInstance(services, type)` — i.e. a fresh instance is constructed
per tool call using `r.Services`, not a cached singleton.

`r.Services` (`RequestContext<TParams>.Services`, inherited from `MessageContext.Services`) is populated per
HTTP request. In the **default** `HttpServerTransportOptions.Stateless = true` mode (the mode Task 7 uses —
no override), `StreamableHttpHandler.CreateSessionAsync` does
`mcpServerServices = context.RequestServices;` and `mcpServerOptions.ScopeRequests = false;`
(`StreamableHttpHandler.cs:490-495`) — i.e. the MCP server for that one-shot request runs directly inside
the **same ASP.NET Core per-request DI scope** the framework already created for the HTTP request. So a
constructor-injected scoped service (an `IProjectAppService`, `ApplicationDbContext`, or
`IHttpContextAccessor`) resolves to the same scoped instance as everything else touched during that HTTP
request.

**First-party runtime proof at this exact pinned commit** (not just source reading): the SDK's own test
`ScopedServices_Resolve_FromRequestScope`
(`tests/ModelContextProtocol.AspNetCore.Tests/StatelessServerTests.cs:184-194`) sets a scoped service's
state from request middleware (`context.RequestServices.GetRequiredService<ScopedService>().State = "From
request middleware!"`) and asserts a tool method resolving the same scoped type
(`StatelessServerTests.cs:440-441`) observes that exact value — proving the tool invocation and the HTTP
request share one DI scope in Stateless mode.

Compiled tool-class skeleton (DI + `[McpServerTool(Name = "...")]`):

```csharp
[McpServerToolType]
public class ProjectTools(IScopedUserContext user)
{
    [McpServerTool(Name = "list_projects"), Description("List the current user's projects.")]
    public Task<object> ListProjects()
        => Task.FromResult<object>(new[] { user.UserId });

    [McpServerTool(Name = "create_project"), Description("Create a project. Requires a read_write token.")]
    public Task<object> CreateProject([Description("Project name")] string name)
        => Task.FromResult<object>(new { name, owner = user.UserId });
}
```

**`[McpServerTool(Name = "list_projects")]` does set the wire tool name** — confirmed in source:
```csharp
// AIFunctionMcpServerTool.cs:75
Name = options?.Name ?? method.GetCustomAttribute<McpServerToolAttribute>()?.Name ?? DeriveName(method),
```
`DeriveName` only strips non-ASCII-letter/digit characters and trims underscores — it does **not**
PascalCase→snake_case convert. So a literal C# method named `list_projects()` would *also* produce the wire
name `"list_projects"` without an explicit `Name=`, but that relies on writing non-idiomatic snake_case C#
identifiers. The explicit `[McpServerTool(Name = "list_projects")]` on a normal PascalCase C# method (as
above) is the correct, idiomatic way to pin the wire name independent of the C# identifier — this is what
R8 already says, and Task 7 Step 5's literal code sample (which uses snake_case C# method names with no
`Name=`) should be corrected to match (see plan edit below).

---

## Fact (c) — `IHttpContextAccessor.HttpContext?.User` inside a tool/filter sees PAT claims

**Verdict: TRUE.**

Because fact (b) established that tool construction and filter execution happen inside the same DI scope as
the originating HTTP request (`context.RequestServices`), and `IHttpContextAccessor` is populated by the
ASP.NET Core hosting layer for the lifetime of that same request/async-flow, `.HttpContext.User` inside a
tool or inside a registered `AddCallToolFilter` delegate reflects whatever principal
`MapMcp("/mcp").RequireAuthorization(...)` caused the auth middleware to attach — which runs **before** the
MCP endpoint delegate executes, and before `StreamableHttpHandler.HandlePostRequestAsync` awaits
`session.Transport.HandlePostRequestAsync(...)` (the call that ultimately invokes the tool) — all on the same
awaited async chain, no detached `Task.Run`.

**First-party runtime proof at this exact pinned commit:** `Can_UseIHttpContextAccessor_InTool`
(`tests/ModelContextProtocol.AspNetCore.Tests/MapMcpTests.cs:54-85`) sets `context.User` in a middleware
(`app.Use(...)`, equivalent in effect to what an `[Authorize]`/`RequireAuthorization` scheme populates), then
calls a tool (`EchoHttpContextUserTools`, `MapMcpTests.cs:523-533`) that reads
`IHttpContextAccessor.HttpContext.User.Identity.Name` via constructor injection, and asserts the tool saw
the correct user name. This is exactly the PAT-claims-in-a-tool shape Task 7/8 need.

Compiled claims-access pattern:

```csharp
public sealed class ScopedUserContext(IHttpContextAccessor accessor) : IScopedUserContext
{
    public string UserId => accessor.HttpContext?.User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("No PAT user.");

    public bool CanWrite => accessor.HttpContext?.User.FindFirstValue("pat_scope") == "read_write";
}
```

This matches the plan's existing `McpUserContext` (Task 7 Step 2) essentially verbatim — no change needed
there.

---

## Net effect on Tasks 7 & 8

- **Task 7 Step 2 (`McpUserContext`)** — unchanged; the pattern is confirmed correct as written.
- **Task 7 Step 5 (tool skeleton)** — cosmetic correction only: use PascalCase C# method names with an
  explicit `[McpServerTool(Name = "...")]` instead of snake_case C# method identifiers relying on
  `DeriveName`. See plan edit.
- **Task 8 Step 5 (interceptor wiring)** — this is where the plan had a real gap: it said "wire the
  interceptor into the MCP pipeline via the SDK's filter registration (`.WithToolInvocationFilter(...)` or
  the equivalent in the pinned version)" with a fallback of manually calling `interceptor.RunAsync(...)`
  from each tool. We now know the concrete API is `.WithRequestFilters(rf => rf.AddCallToolFilter(...))`,
  it **does** run for every DI tool call (fact (a) correction above), and scoped services (the interceptor,
  `McpUserContext`, `McpActivitySink`) must be resolved from `request.Services` **inside** the filter
  delegate body — not via constructor injection of the filter itself, since `AddCallToolFilter` registers a
  plain delegate via `Configure<McpServerOptions>`, which is never DI-activated. See plan edit for the exact
  registration code and the small signature adjustment to `McpActivityInterceptor.RunAsync` this implies.

## Cleanup

`.superpowers/sdd/spike/` (throwaway `Spike.csproj` + `Program.cs`) is deleted before this task is marked
done — nothing under it is committed. The clone of `csharp-sdk` used for source reading lived under the
session scratchpad (`C:\Users\...\scratchpad\mcp-sdk-src`), outside the repo, and is not part of this
repo's working tree.
