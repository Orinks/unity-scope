import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

export interface DiscoveryRecord {
  version: string;
  transport: "http" | "pipe";
  endpoint: string;
  auth_token: string;
  process: string;
  pid: number;
  started_utc: string;
}

export function discoveryDir(): string {
  const local =
    process.env.LOCALAPPDATA ||
    join(process.env.USERPROFILE || process.env.HOME || "", "AppData", "Local");
  return join(local, "UnityScope");
}

export function findDiscovery(): DiscoveryRecord {
  const explicit = process.env.UNITY_SCOPE_DISCOVERY;
  if (explicit) return JSON.parse(readFileSync(explicit, "utf-8"));

  const dir = discoveryDir();
  let files: string[];
  try {
    files = readdirSync(dir).filter((f) => f.endsWith(".json"));
  } catch {
    throw new Error(
      `No UnityScope discovery directory at ${dir}. Is the plugin loaded? Set UNITY_SCOPE_DISCOVERY to a specific file to override.`
    );
  }
  if (files.length === 0) {
    throw new Error(`No UnityScope discovery files in ${dir}.`);
  }

  const newest = files
    .map((f) => ({ f, mtime: statSync(join(dir, f)).mtimeMs }))
    .sort((a, b) => b.mtime - a.mtime)[0];

  return JSON.parse(readFileSync(join(dir, newest.f), "utf-8"));
}
