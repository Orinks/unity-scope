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

export interface ResolvedDiscovery {
  record: DiscoveryRecord;
  // Path of the file the record was read from, and its mtime. Used to detect
  // game restarts: a new process writes a new discovery file (new token/port),
  // so the MCP client can re-read instead of holding a stale record.
  path: string;
  mtimeMs: number;
}

export function discoveryDir(): string {
  const local =
    process.env.LOCALAPPDATA ||
    join(process.env.USERPROFILE || process.env.HOME || "", "AppData", "Local");
  return join(local, "UnityScope");
}

export function findDiscovery(): ResolvedDiscovery {
  const explicit = process.env.UNITY_SCOPE_DISCOVERY;
  if (explicit) {
    return {
      record: JSON.parse(readFileSync(explicit, "utf-8")),
      path: explicit,
      mtimeMs: statSync(explicit).mtimeMs,
    };
  }

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

  const path = join(dir, newest.f);
  return {
    record: JSON.parse(readFileSync(path, "utf-8")),
    path,
    mtimeMs: newest.mtime,
  };
}
