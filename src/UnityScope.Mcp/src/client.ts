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

  async get(path: string, query: Record<string, string> = {}): Promise<unknown> {
    const url = new URL(this.discovery.endpoint + path);
    for (const [k, v] of Object.entries(query)) url.searchParams.set(k, v);

    const res = await fetch(url, {
      headers: { "X-UnityScope-Token": this.discovery.auth_token },
    });
    if (!res.ok) {
      throw new Error(`UnityScope ${path} -> ${res.status} ${await res.text()}`);
    }
    return res.json();
  }
}
