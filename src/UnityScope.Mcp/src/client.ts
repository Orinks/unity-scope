import { findDiscovery, type DiscoveryRecord } from "./discovery.js";

export class UnityScopeClient {
  private discovery: DiscoveryRecord;

  constructor(discovery?: DiscoveryRecord) {
    this.discovery = discovery ?? findDiscovery();
    if (this.discovery.transport !== "http") {
      throw new Error(
        `Transport '${this.discovery.transport}' not yet supported by MCP client. Use http for now.`
      );
    }
  }

  async get(path: string, query: Record<string, string | undefined> = {}): Promise<unknown> {
    return this.request("GET", path, query);
  }

  async post(path: string, query: Record<string, string | undefined> = {}): Promise<unknown> {
    return this.request("POST", path, query);
  }

  private async request(
    method: string,
    path: string,
    query: Record<string, string | undefined>,
  ): Promise<unknown> {
    const url = new URL(this.discovery.endpoint + path);
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== "") url.searchParams.set(k, String(v));
    }
    const res = await fetch(url, {
      method,
      headers: { "X-UnityScope-Token": this.discovery.auth_token },
    });
    const text = await res.text();
    let parsed: unknown;
    try {
      parsed = text.length === 0 ? null : JSON.parse(text);
    } catch {
      parsed = text;
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
    return `${this.discovery.process} (pid ${this.discovery.pid}) at ${this.discovery.endpoint}`;
  }
}
