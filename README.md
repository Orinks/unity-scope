# UnityScope

Agent-native introspection runtime for Mono Unity games. Think Unity Explorer, but designed for AI coding agents (Claude Code, Codex, Cursor) instead of humans clicking through a tree view.

## What it is

A BepInEx plugin that exposes the running game's scene graph, components, types, and (optionally) method invocation via a local-only HTTP API. Agents query it through an MCP server or CLI to understand UI structure, observe state changes between actions, and build accurate mental models of unfamiliar games.

Spun out from patterns developed in [blackout-access](https://github.com/Orinks/blackout-access) — a screen-reader accessibility mod for Blackout Rugby that needed deep, repeatable Unity UI introspection during development.

## Why not just use Unity Explorer?

Unity Explorer is a human GUI: in-game windows, mouse-driven tree expansion, manual inspection. Agents need:

- **Structured output** — JSON, not log lines.
- **Queryable** — selectors and filters; agents OOM on full dumps.
- **Stable addressing** — IDs that survive scene reloads.
- **Diffs** — what changed between snapshots is more valuable than any single snapshot. This is how an agent learns what a click *did*.
- **Remote invocation** — no human in the loop pressing F12.

## Status

Scaffolding only. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/TRANSPORT.md](docs/TRANSPORT.md).

## Layout

```
src/
  UnityScope.Runtime/   BepInEx plugin (net472, Mono Unity)
  UnityScope.Mcp/       MCP server (TypeScript) wrapping the Runtime API
docs/
  ARCHITECTURE.md       3-layer design + endpoint catalog
  TRANSPORT.md          Why loopback HTTP, port discovery, named pipe fallback
```

## Build

```
build.bat            Debug build of the runtime
build.bat release    Builds and copies to <GameDir>\BepInEx\plugins\UnityScope\
```

Edit `GameDir` in `src/UnityScope.Runtime/UnityScope.Runtime.csproj` for your target game.

## License

TBD.
