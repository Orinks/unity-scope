# UnityScope.Mcp

MCP server exposing the UnityScope Runtime API to AI coding agents.

Currently a stub: the entry point reads the discovery file, hits `/ping` and `/scene` against a running plugin, and prints the result. Real MCP tool registration lands once the spike-phase endpoints in `UnityScope.Runtime` are stable.

## Smoke test

1. Build and load `UnityScope.Runtime` into a Mono Unity game via BepInEx.
2. Confirm a discovery file exists in `%LOCALAPPDATA%\UnityScope\`.
3. From this directory:
   ```
   npm install
   npm run build
   npm start
   ```
4. Expect JSON output for `/ping` and `/scene`.

## Planned tools

| Tool | Endpoint |
|---|---|
| `unity_scene` | `GET /scene` |
| `unity_tree` | `GET /tree` |
| `unity_node` | `GET /node/<id>` |
| `unity_find` | `GET /find` |
| `unity_diff` | `GET /diff` |
| `unity_invoke` | `POST /invoke` (gated) |
