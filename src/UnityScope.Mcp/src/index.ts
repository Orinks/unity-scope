#!/usr/bin/env node
// MCP server exposing UnityScope to AI coding agents via stdio transport.
// Install in Claude Code: claude mcp add unityscope -- node <path-to>/dist/index.js

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { UnityScopeClient } from "./client.js";

// Resolve the discovery file lazily on first tool call so the server can start
// even when the game isn't running yet (the user may launch it after).
let cached: UnityScopeClient | null = null;
function client(): UnityScopeClient {
  if (cached) return cached;
  cached = new UnityScopeClient();
  return cached;
}

function ok(value: unknown) {
  return {
    content: [
      { type: "text" as const, text: typeof value === "string" ? value : JSON.stringify(value, null, 2) },
    ],
  };
}

function err(message: string) {
  return {
    isError: true,
    content: [{ type: "text" as const, text: message }],
  };
}

async function tool<T>(fn: () => Promise<T>) {
  try {
    return ok(await fn());
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e);
    return err(msg);
  }
}

const server = new McpServer({ name: "unityscope", version: "0.1.0" });

server.registerTool(
  "unity_scene",
  {
    description: "Returns the active Unity scene name and a summary of root canvases. Cheap orientation call — start here when exploring an unfamiliar game UI.",
    inputSchema: {},
  },
  async () => tool(() => client().get("/scene")),
);

server.registerTool(
  "unity_tree",
  {
    description: "Walks a partial GameObject hierarchy and returns it as JSON. Always specify depth/max — full dumps blow up agent context. Root resolution: instance id, /absolute/path, bare name, or empty for all root canvases.",
    inputSchema: {
      root: z.string().optional().describe("Instance id, /absolute/path, or bare GameObject name. Empty/omitted = all root canvases."),
      depth: z.number().int().min(0).max(20).default(3).describe("How many child levels to recurse. Default 3."),
      max: z.number().int().min(1).max(2000).default(200).describe("Hard cap on total node count. Default 200; truncated flag set when hit."),
    },
  },
  async ({ root, depth, max }) =>
    tool(() => client().get("/tree", { root, depth: String(depth), max: String(max) })),
);

server.registerTool(
  "unity_node",
  {
    description: "Returns a single GameObject's full detail: components, reflected public fields/properties, RectTransform metrics, immediate children. Heavy — use after unity_tree narrows the target.",
    inputSchema: {
      id: z.number().int().optional().describe("GameObject instance id (preferred — stable across queries)."),
      path: z.string().optional().describe("Hierarchy path like /Canvas/Foo/Bar (alternative to id)."),
    },
  },
  async ({ id, path }) => {
    if (id == null && !path) return err("Provide either 'id' or 'path'.");
    return tool(() =>
      client().get("/node", { id: id != null ? String(id) : undefined, path }),
    );
  },
);

server.registerTool(
  "unity_find",
  {
    description: "Query GameObjects with a CSS-ish selector. Examples: '*Continue*' (name glob), '*[type=BlackoutButton][active]' (any active node with a BlackoutButton component), '*[text*=Tutorial]' (any node whose extracted text contains 'Tutorial'). Filters: [active], [!active], [interactable], [type=...|*=...|^=...], [text=...|*=...|^=...].",
    inputSchema: {
      selector: z.string().describe("Selector string. See description for grammar."),
      max: z.number().int().min(1).max(500).default(50).describe("Max results returned. Default 50."),
    },
  },
  async ({ selector, max }) =>
    tool(() => client().get("/find", { selector, max: String(max) })),
);

server.registerTool(
  "unity_snapshot",
  {
    description: "Captures a fingerprint of every active GameObject (active state, text, sprite, alpha, interactability) keyed by instance id. Returns the snapshot id to pass into unity_diff later. Use this BEFORE asking the user (or yourself) to perform an action.",
    inputSchema: {
      label: z.string().optional().describe("Optional human-readable label, e.g. 'before-continue-click'."),
    },
  },
  async ({ label }) => tool(() => client().get("/snapshot", { label })),
);

server.registerTool(
  "unity_list_snapshots",
  {
    description: "Lists snapshots currently held in the runtime's LRU (max 10). Useful when you've forgotten which id is which.",
    inputSchema: {},
  },
  async () => tool(() => client().get("/snapshot/list")),
);

server.registerTool(
  "unity_diff",
  {
    description: "The killer endpoint. Captures fresh state and compares against a stored snapshot, returning added/removed/modified node lists with per-field before/after values. Run unity_snapshot, perform an action, then unity_diff to see exactly what the action changed in the UI.",
    inputSchema: {
      since: z.string().describe("Snapshot id from a prior unity_snapshot call."),
      max: z.number().int().min(1).max(2000).default(200).describe("Max entries per added/removed/modified list. Default 200."),
    },
  },
  async ({ since, max }) =>
    tool(() => client().get("/diff", { since, max: String(max) })),
);

server.registerTool(
  "unity_invoke",
  {
    description: "Calls a method or sets/gets a public property/field on a component. GATED — requires UnityScope:AllowInvoke=true in the game's BepInEx config; otherwise returns 403. V1 supports primitive args only (string/bool/int/long/float/double/enum). Action defaults to 'call' if args present, 'set' if value present, otherwise 'call' (no-arg method).",
    inputSchema: {
      target: z.string().describe("Instance id or hierarchy path of the GameObject."),
      member: z.string().describe("Method, property, or field name."),
      component: z.string().optional().describe("Optional: substring of component type name to disambiguate. Default tries every component."),
      action: z.enum(["call", "set", "get"]).optional().describe("Override action. Default inferred from args/value."),
      value: z.string().optional().describe("Value for 'set' action. Coerced to the member's declared type."),
      args: z.array(z.string()).optional().describe("Positional args for 'call' action. Strings; runtime coerces by parameter type."),
    },
  },
  async ({ target, member, component, action, value, args }) => {
    const query: Record<string, string | undefined> = { target, member, component, action, value };
    if (args) args.forEach((v, i) => (query[`arg${i}`] = v));
    return tool(() => client().post("/invoke", query));
  },
);

await server.connect(new StdioServerTransport());
