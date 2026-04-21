# UnityScope

Agent-native introspection runtime for Mono Unity games. Think Unity Explorer, but designed for AI coding agents (Claude Code, Codex, Cursor) instead of humans clicking through a tree view.

A BepInEx plugin exposes the running game's scene graph, components, types, and (gated) method invocation via a local-only HTTP API. An MCP server wraps that API as tools any modern AI coding agent can call: `unity_scene`, `unity_tree`, `unity_node`, `unity_find`, `unity_snapshot`, `unity_list_snapshots`, `unity_diff`, `unity_list_text_extractors`, `unity_register_text_extractor`, `unity_invoke`.

Spun out from patterns developed in [blackout-access](https://github.com/Orinks/blackout-access) — an in-development screen-reader accessibility mod for Blackout Rugby that needed deep, repeatable Unity UI introspection. The introspection patterns generalized; the accessibility mod itself is still being built.

## Why not just use Unity Explorer?

Unity Explorer is a human GUI: in-game windows, mouse-driven tree expansion, manual inspection. Agents need:

- **Structured output** — JSON, not log lines.
- **Queryable** — selectors and filters; agents OOM on full dumps.
- **Stable addressing** — instance IDs that survive scene reloads.
- **Diffs** — what changed between two snapshots is more valuable than any single snapshot. This is how an agent learns what a click *did*.
- **Remote invocation** — no human in the loop pressing F12.

## What you don't need

- **Unity Editor.** UnityScope runs inside the already-built game `.exe`. The csproj references the Unity DLLs that ship with the game's `_Data\Managed\` folder.
- **Unity license, Unity Hub, or any Unity GUI.** The toolchain is .NET SDK + a text editor. Same workflow as any BepInEx plugin.

## Requirements

- A Unity game built with the **Mono** runtime (most pre-2020 commercial Unity titles). IL2CPP support is planned — see [docs/IL2CPP.md](docs/IL2CPP.md).
- **BepInEx 5.x** installed in the target game.
- **.NET SDK** (any recent version) for building the plugin.
- **Node.js 20+** for running the MCP server.

## Install

This guide is written so an AI coding agent can follow it end-to-end given only the repo URL and the user's game install path. No file edits required.

### 1. Confirm the game uses the Mono runtime

UnityScope V1 only supports Mono Unity games. Quick check from the game's install folder:

- `<GameDir>\GameAssembly.dll` exists → **IL2CPP**, not supported (see [docs/IL2CPP.md](docs/IL2CPP.md)).
- `<GameDir>\<GameName>_Data\Managed\Assembly-CSharp.dll` exists → **Mono**, supported.

### 2. Install BepInEx 5.x into the game

Download from [BepInEx/BepInEx releases](https://github.com/BepInEx/BepInEx/releases). Pick the **Mono** build for your architecture (`BepInEx_x64_5.4.x.x.zip` for most modern games). Extract the contents directly into `<GameDir>` so that `<GameDir>\BepInEx\` and `<GameDir>\winhttp.dll` (or equivalent) sit next to the game's `.exe`. Launch the game once so BepInEx generates its config folders, then close it.

### 3. Clone this repo

```bash
git clone https://github.com/Orinks/unity-scope.git
cd unity-scope
```

Anywhere on disk is fine.

### 4. Build and deploy the plugin

The build accepts the game path as a parameter — no need to edit any project files.

```bash
# Windows:
build.bat release "C:\Program Files (x86)\Steam\steamapps\common\YourGame"

# Mac/Linux (rare for Unity Mono games, but works):
./build.sh release "/path/to/your/game"

# Or call dotnet directly:
dotnet build src/UnityScope.Runtime/UnityScope.Runtime.csproj -c Release \
  -p:GameDir="C:\path\to\your\game"
```

The build auto-detects the `*_Data\Managed` folder under `<GameDir>` and deploys `UnityScope.dll` to `<GameDir>\BepInEx\plugins\UnityScope\`. Helpful errors fire if BepInEx isn't installed or if the game is IL2CPP.

### 5. Launch the game once

BepInEx loads the plugin. Confirm in `<GameDir>\BepInEx\LogOutput.log`:

```
[Info   :UnityScope] UnityScope listening: http://127.0.0.1:<port>/ (http, token-protected)
```

A discovery file appears under the platform's local app data folder (`%LOCALAPPDATA%\UnityScope\` on Windows) containing the port and auth token. MCP clients find it automatically.

### 6. Build the MCP server

```bash
cd src/UnityScope.Mcp
npm install
npm run build
```

### 7. Register with your AI coding agent

**Claude Code:**

```bash
claude mcp add unityscope -- node <absolute-path-to-clone>/src/UnityScope.Mcp/dist/index.js
```

**Cursor / Windsurf / any other MCP-aware client:** add an entry pointing to the same `dist/index.js`.

Once registered, the agent has tools `unity_scene`, `unity_tree`, etc. Sanity-check by asking: *"What canvases are active in the game right now?"*

## A typical agent workflow

```
1. unity_scene                                 → orient (active scene + canvases)
2. unity_find selector="*Continue*"            → locate the Continue button
3. unity_snapshot label="before-continue"      → baseline state (returns id)
4. <user clicks Continue, or unity_invoke>
5. unity_diff since=<id>                       → exactly what changed
```

The diff shows added/removed/modified nodes with per-field before/after values. Agents learn the consequences of actions instead of guessing.

## Configuration

Settings live in `<GameDir>\BepInEx\config\com.orinks.unityscope.cfg` (created on first launch):

| Key | Default | Purpose |
|---|---|---|
| `Transport` | `http` | `http` (loopback HTTP, default) or `pipe` (named pipe stub — not yet implemented) |
| `HttpPort` | `17897` | Preferred loopback port for the http transport. Stable across restarts so MCP clients don't have to re-resolve discovery every launch. Falls back to an OS-assigned free port if this one is in use (e.g. a second game instance). |
| `AllowInvoke` | `false` | Set `true` to allow `POST /invoke` to call methods and set fields. Logged on every call. |
| `AutoDetectText` | `true` | Convention-based auto-detection of text on unknown UI types. See below. |
| `TextExtractors` | empty | Optional explicit text-extractor rules. Agents normally register these at runtime instead. |

### Reading text from custom UI types

UnityScope reads text from three layers, in order:

1. **Built-in.** `UnityEngine.UI.Text` and `TMPro.TextMeshPro*` work without any setup.
2. **Convention-based auto-detection.** For any other component type, UnityScope tries common property names (`text`, `Text`, `Label`, `Caption`, `DisplayText`) and no-arg method names (`GetText`, `GetLabel`, `GetCaption`, `GetDisplayText`) returning a string. The result is cached per type so reflection only runs once. Catches the great majority of custom UI types without configuration.
3. **Explicit rules.** For types that don't follow conventions, register a rule.

**The expected workflow is agentic, not manual.** When an agent encounters a UI element whose text isn't surfaced, it inspects the component with `unity_node`, identifies the relevant property/method, and registers the rule via `unity_register_text_extractor`. The rule persists to `<plugins>\UnityScope\text-extractors.txt` so it survives restarts. No human needs to know UI type names.

If you'd rather seed rules manually, the same rules can also go in the `TextExtractors` cfg key:

```
TextExtractors = BlackoutButton:method:GetText,MyGame.SignText:property:Caption
```

Disable convention-based auto-detection if it produces false positives by setting `AutoDetectText = false` in the cfg.

## Security model

- HTTP listener binds **only to `127.0.0.1`** — not reachable from other machines.
- Every request must carry the `X-UnityScope-Token` header; the token is generated per-launch and written to the discovery file. Other local users can't read your `%LOCALAPPDATA%`.
- `/invoke` is off by default. When enabled, every successful invocation is logged with target id, type, member, and arg count.

This is sufficient for single-user dev machines. Not designed for shared/CI environments.

## Layout

```
src/
  UnityScope.Runtime/   BepInEx Mono plugin
    Endpoints/          /scene /tree /node /snapshot /diff /find /invoke
    Inspection/         Hierarchy walker, component serializer, snapshot store, selector
    Server/             Request router, main-thread dispatcher
    Transport/          HTTP, named-pipe stub, discovery file
    Json/               Streaming JSON writer
  UnityScope.Mcp/       TypeScript MCP server (10 tools, stdio transport)
docs/
  ARCHITECTURE.md       Three-layer design + endpoint catalog
  TRANSPORT.md          Why loopback HTTP, port discovery, named pipe rationale
  IL2CPP.md             Plan for IL2CPP runtime support (Mono is V1)
```

## Status

V1. Runtime endpoints validated against a live game. MCP server speaks proper MCP/JSON-RPC. IL2CPP support, named-pipe transport, `/events` SSE, and a CLI client are roadmap.

## Note for Git Bash / MSYS users

If you call the runtime via `curl` from Git Bash on Windows, MSYS translates leading-slash paths into Windows paths (`/Canvas` → `C:/Program Files/Git/Canvas`). Either pre-encode the slash (`%2FCanvas`), prefix the env (`MSYS_NO_PATHCONV=1 curl ...`), or just use instance IDs (recommended for agents anyway — they're stable and unambiguous). MCP clients are unaffected.

## License

GPL-3.0 — see [LICENSE](LICENSE).
