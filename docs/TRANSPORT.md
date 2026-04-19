# Transport

## Why loopback HTTP is the default

Concern raised: *what about blocked ports?*

Reality on Windows:

- **Windows Firewall does not prompt or block traffic on `127.0.0.1`.** Loopback is exempt by default policy. The firewall prompt you've seen for other dev tools is triggered by binding to `0.0.0.0` or a non-loopback adapter — UnityScope binds explicitly to `IPAddress.Loopback`, never `Any`.
- **No URL ACL needed for `127.0.0.1`.** `HttpListener` requires `netsh http add urlacl` only for hostname or `+` prefixes. Loopback IP literal works under a normal user account.
- **The real failure mode is port conflicts.** Hardcoding any port (8080, 9000, 38000…) means a second game launch, or another tool already using that port, fails. So we don't hardcode.

Resolution: bind to port `0`. The OS picks a free ephemeral port. We then write the actual port to a discovery file so clients can find us.

## Discovery file

Path: `%LOCALAPPDATA%\UnityScope\<process_name>_<pid>.json`

Example contents:

```json
{
  "version": "0.1.0",
  "transport": "http",
  "endpoint": "http://127.0.0.1:54839",
  "auth_token": "8f3a...redacted...",
  "process": "Blackout Rugby",
  "pid": 12480,
  "started_utc": "2026-04-19T18:22:14Z"
}
```

Client lookup order:

1. `UNITY_SCOPE_DISCOVERY` env var if set (full path to a specific discovery file).
2. Newest file in `%LOCALAPPDATA%\UnityScope\` matching `*.json` (typical single-game case).
3. Error with a clear message listing what was tried.

The plugin deletes its own discovery file on `OnDestroy`. Stale files from crashed processes are pruned at boot if their PID no longer exists.

## Security model

UnityScope binds to loopback only — remote machines cannot reach it. But *other local processes* on the same machine can hit `127.0.0.1`. Mitigation:

- Every request must include `X-UnityScope-Token: <token>` matching the value in the discovery file.
- The discovery file is written with the user's default ACL (other users on the machine cannot read it; same user processes can).
- The token is regenerated each plugin boot (`Guid.NewGuid().ToString("N")`).
- `POST /invoke` additionally requires the plugin to have been launched with `--allow-invoke` (BepInEx config flag). Read-only endpoints work without it.

This is sufficient for single-user dev machines. Not sufficient for shared/CI environments — that's a non-goal.

## Named pipe fallback

For environments where loopback HTTP fails (rare; some corporate AV/EDR products intercept all sockets including loopback):

- Set BepInEx config `UnityScope:Transport = pipe`.
- Plugin creates `NamedPipeServerStream("unity-scope-<pid>", PipeDirection.InOut, ...)`.
- Discovery file gets `"transport": "pipe"` and `"endpoint": "\\\\.\\pipe\\unity-scope-12480"`.
- Clients use `net.connect(pipePath)` (Node) or `open("\\\\.\\pipe\\...")` (Python). Both work without admin rights.

Same JSON wire format. Clients pick branch based on `transport` field in discovery file.

## Why not Unix sockets / gRPC / WebSockets

- Unix sockets — Mono on Windows doesn't support `AF_UNIX` cleanly across all Unity-bundled Mono versions.
- gRPC — adds a heavyweight dependency that bloats the plugin DLL and fights BepInEx's assembly-loading rules.
- WebSockets — useful only for `/events` streaming. SSE over plain HTTP works fine and needs zero extra deps.

We may add SSE on the existing HTTP transport for `/events` later. WebSockets stay off the menu unless a concrete use case demands them.
