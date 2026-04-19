# Architecture

Three layers, each independently testable.

## 1. Runtime (in-game BepInEx plugin)

Lives inside the Unity process. Responsibilities:

- Boot a local-only request listener (default: HTTP on 127.0.0.1 ephemeral port).
- Marshal every request onto the Unity main thread via a frame-tick dispatcher. **Unity APIs are not thread-safe — this is non-negotiable.**
- Walk the scene graph, serialize via reflection, return JSON.
- Maintain a snapshot store keyed by stable node IDs so `/diff` can compute deltas.
- Expose a type-handler registry so games can plug in custom extractors (e.g. Blackout Rugby's `BlackoutText`/`BlackoutButton`/`BlackoutDropdown` from blackout-access become a small adapter package).

Project: `src/UnityScope.Runtime/`

## 2. Transport

Pluggable. Two implementations planned:

- **HttpTransport** (default) — `HttpListener` on `127.0.0.1:0`. OS picks port. Discovery file written to `%LOCALAPPDATA%\UnityScope\<process>_<pid>.json` containing port, auth token, version, started timestamp. Clients read the discovery file.
- **NamedPipeTransport** (fallback) — `NamedPipeServerStream` named `unity-scope-<pid>`. No port involved. Use when corporate AV blocks even loopback HTTP, or when running headless with no socket support.

Same JSON request/response wire format on both.

See [TRANSPORT.md](TRANSPORT.md) for the discovery-file format and security model.

## 3. Clients

- **UnityScope.Mcp** (TypeScript) — MCP server. Exposes tools: `unity_scene`, `unity_tree`, `unity_node`, `unity_find`, `unity_diff`, `unity_invoke`. Drops into Claude Code / Codex / Cursor.
- **UnityScope.Cli** (planned) — `unityscope find ...`, `unityscope tree --depth 3`. For shell-based agent loops without MCP.

## Endpoint catalog

| Endpoint | Purpose | Why it exists |
|---|---|---|
| `GET /ping` | Liveness + version handshake | First call any client makes |
| `GET /scene` | Active scene name + root canvases | Cheap orientation |
| `GET /tree?root=<path>&depth=N&include=<filters>` | Partial hierarchy | Full dumps OOM agent context — always paginate |
| `GET /node/<id>` | Full component list, field values, screen rect | Drill-down after `/tree` |
| `GET /find?selector=...` | CSS-like selector queries | `Canvas Root > BlackoutButton[text*="Continue"]` |
| `GET /diff?since=<snapshotId>` | What changed since last snapshot | **Killer feature** — lets agents observe action effects |
| `GET /types?assembly=Assembly-CSharp` | Enumerate game types | Replaces dnSpy for "what classes exist" |
| `GET /events` (SSE) | Live stream of mutations + button clicks | Watch instead of poll |
| `POST /invoke` | Call method or set field | Gated behind `--allow-invoke` flag |
| `POST /snapshot?label=...` | Persist current scene to disk | Agent uses normal `Read` tool to consume |

## Phasing

1. **Spike** — `/ping`, `/scene`, `/tree`, `/node`. MCP wraps them. Validate it feels better than reading log dumps. *(~1 day of work)*
2. `/diff` and `/find` — the queries that actually save context.
3. `/invoke` + safety gating + audit log of every invocation.
4. Generality test — port to a second game; extract Blackout adapters as a separate package.

## Non-goals

- Not a debugger. No breakpoints, no stepping.
- Not a hot-reload system. No DLL swapping.
- Not multi-user. One agent, one game process, local machine only.
