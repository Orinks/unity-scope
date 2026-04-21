import { statSync } from "node:fs";
import { findDiscovery, type DiscoveryRecord, type ResolvedDiscovery } from "./discovery.js";

export class UnityScopeClient {
  private resolved: ResolvedDiscovery | null;

  constructor(resolved?: ResolvedDiscovery) {
    this.resolved = resolved ?? null;
    if (this.resolved && this.resolved.record.transport !== "http") {
      throw new Error(
        `Transport '${this.resolved.record.transport}' not yet supported by MCP client. Use http for now.`,
      );
    }
  }

  async get(path: string, query: Record<string, string | undefined> = {}): Promise<unknown> {
    return this.request("GET", path, query);
  }

  async post(path: string, query: Record<string, string | undefined> = {}): Promise<unknown> {
    return this.request("POST", path, query);
  }

  private discovery(): DiscoveryRecord {
    // Refresh if the file on disk is newer than what we cached. A game restart
    // writes a new discovery file (fresh pid/token/port), so this naturally
    // picks up the new endpoint without forcing a Claude Code restart.
    if (this.resolved) {
      try {
        const mtime = statSync(this.resolved.path).mtimeMs;
        if (mtime > this.resolved.mtimeMs) this.resolved = null;
      } catch {
        // File vanished (plugin unloaded / game exited). Fall through to re-resolve.
        this.resolved = null;
      }
    }
    if (!this.resolved) {
      this.resolved = findDiscovery();
      if (this.resolved.record.transport !== "http") {
        throw new Error(
          `Transport '${this.resolved.record.transport}' not yet supported by MCP client. Use http for now.`,
        );
      }
    }
    return this.resolved.record;
  }

  private async request(
    method: string,
    path: string,
    query: Record<string, string | undefined>,
  ): Promise<unknown> {
    try {
      return await this.doRequest(this.discovery(), method, path, query);
    } catch (e) {
      // Network errors (ECONNREFUSED / fetch failed) typically mean the game
      // has been restarted and the listener moved. Drop the cached record and
      // retry once against freshly resolved discovery.
      if (isNetworkError(e)) {
        this.resolved = null;
        return this.doRequest(this.discovery(), method, path, query);
      }
      throw e;
    }
  }

  private async doRequest(
    disc: DiscoveryRecord,
    method: string,
    path: string,
    query: Record<string, string | undefined>,
  ): Promise<unknown> {
    const url = new URL(disc.endpoint + path);
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== "") url.searchParams.set(k, String(v));
    }
    const res = await fetch(url, {
      method,
      headers: { "X-UnityScope-Token": disc.auth_token },
    });
    const text = await res.text();
    let parsed: unknown;
    try {
      parsed = text.length === 0 ? null : JSON.parse(text);
    } catch {
      parsed = text;
    }
    // 401 means the token we sent is stale — the game restarted and minted a new
    // one. Invalidate the cache so the caller's retry picks up the new token.
    if (res.status === 401) {
      this.resolved = null;
    }
    if (!res.ok) {
      const summary =
        typeof parsed === "object" && parsed !== null
          ? JSON.stringify(parsed)
          : String(parsed ?? "");
      throw new Error(`UnityScope ${method} ${path} -> ${res.status}: ${summary}`);
    }
    return parsed;
  }

  describe(): string {
    const d = this.discovery();
    return `${d.process} (pid ${d.pid}) at ${d.endpoint}`;
  }
}

function isNetworkError(e: unknown): boolean {
  if (!(e instanceof Error)) return false;
  // undici raises TypeError("fetch failed") with a nested cause carrying the
  // real errno. Accept both the top-level and cause shapes.
  const codes = ["ECONNREFUSED", "ECONNRESET", "EHOSTUNREACH", "ENOTFOUND"];
  const anyE = e as Error & { code?: string; cause?: { code?: string } };
  if (anyE.code && codes.includes(anyE.code)) return true;
  if (anyE.cause && anyE.cause.code && codes.includes(anyE.cause.code)) return true;
  return e.name === "TypeError" && /fetch failed/i.test(e.message);
}
