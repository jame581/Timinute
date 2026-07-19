# Connecting an AI assistant over MCP

Timinute hosts a [Model Context Protocol](https://modelcontextprotocol.io/) server alongside the web app, so an AI assistant — Claude Code, Claude Desktop, or any other MCP-capable client — can read and (optionally) log your time directly, using the same account data you see in the app. No separate service to run: it's the same container, a different endpoint.

## The endpoint

```
https://<your-host>/mcp
```

For a local dev run that's `https://localhost:7047/mcp`; for a self-hosted instance, your own domain. The endpoint uses the stateless Streamable HTTP transport and speaks plain MCP over that single URL — there's no separate SSE endpoint to configure.

## 1. Create a token

Open **API Tokens** (under **Account** in the sidebar, `/settings/tokens`) and click **New token**. Give it a name (e.g. "Claude Code"), pick a scope, and optionally an expiry (30 days / 90 days / 1 year / no expiry), then create it.

The token is shown **once**, in full, right after creation — copy it immediately. Timinute never stores or displays the plaintext again; only a prefix (for identifying the token in the list) and its SHA-256 hash are kept. If you lose it, revoke it and create a new one.

Token format: `tmn_pat_` followed by a random string, e.g. `tmn_pat_AbCdEf01234...`.

## 2. Connect a client

### Claude Code

```bash
claude mcp add --transport http timinute https://your-host/mcp --header "Authorization: Bearer tmn_pat_..."
```

### Claude Desktop / other HTTP-transport clients

Clients that support a declarative config file for remote MCP servers generally use a shape like this — check your specific client's docs for the exact field names it expects:

```json
{
  "mcpServers": {
    "timinute": {
      "type": "http",
      "url": "https://your-host/mcp",
      "headers": {
        "Authorization": "Bearer tmn_pat_..."
      }
    }
  }
}
```

Either way, the token goes in a static `Authorization: Bearer <token>` header. There's no interactive OAuth login flow — the token you copied in step 1 is the entire credential.

## Scopes

Every token is either:

- **`read`** — can call the read-only tools (list projects, search time entries, analytics summary).
- **`read_write`** — can also call the write tools (create a project, log/update/delete a time entry).

Pick `read` if you only want an assistant to look at your data. If a `read`-scoped token calls a write tool, the server refuses the call before it runs, with a clean message: **"This token is read-only."** No partial writes, no stack trace back to the client.

## The seven tools

| Tool | Scope | What it does |
|------|-------|---------------|
| `list_projects` | read | List the current user's projects (id, name, color). |
| `search_time_entries` | read | Search the current user's time entries, newest first, optionally filtered by date range, project, and name. |
| `get_analytics_summary` | read | Get a summary of the current user's tracked time over a date range (total, task count, active days, target). |
| `create_project` | read_write | Create a project for the current user. |
| `log_time` | read_write | Log a new time entry for the current user. |
| `update_time_entry` | read_write | Update an existing time entry owned by the current user. |
| `delete_time_entry` | read_write | Delete (soft-delete) a time entry owned by the current user. |

All seven only ever see and touch the data belonging to the account the token was created under — there's no way for a token to reach another user's projects or entries. Deleted time entries land in the same 30-day Trash as anything deleted from the UI.

## AI activity log

Every tool call — success or failure — is recorded and viewable on the **AI activity** page (linked from the tokens page, `/settings/ai-activity`): timestamp, tool name, a one-line summary, a Success/Failed pill, and (for failures) a short detail message. This is the audit trail for anything an assistant did on your behalf. Rows older than 90 days are purged automatically by a background job that runs once a day.

## Security notes

- **Shown once, hashed at rest.** The plaintext token is displayed only immediately after creation. Timinute stores a SHA-256 hash plus an 8-character prefix — never the plaintext.
- **Revocation is immediate.** Revoking a token (from the API Tokens page) takes effect on its very next request — every call re-validates the token against the database, there's no caching window to wait out.
- **Optional expiry.** Set a token to expire in 30 days, 90 days, or a year, or leave it with no expiry.
- **PAT works only at `/mcp`.** A personal access token authenticates nowhere else — not the REST API, not the web login. Sending one to any other endpoint is rejected the same as no credential at all.
- **Off switch.** Setting `Mcp__Enabled=false` removes the `/mcp` endpoint and the MCP tool/audit registrations entirely. The AI-activity purge job keeps running regardless (it's registered unconditionally) — harmless with MCP off, since the activity table just stays empty or shrinks toward empty. See [`docs/DOCKER.md`](DOCKER.md#mcp-server) for the full configuration reference.
- **Revoked tokens disappear from the list, but aren't deleted.** A revoked token vanishes from your token list immediately — the list only ever shows active tokens, and there's no history view. The row itself is retained internally (it can never authenticate again) for audit and referential integrity, but it's invisible in the UI.
- **Use TLS in production.** The bearer token crosses the wire on every request; over plain HTTP it's sent in cleartext. Put a TLS-terminating reverse proxy in front of any internet-facing deployment (see [`docs/DOCKER.md`](DOCKER.md)).
