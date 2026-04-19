// MCP server stub. Wire real tools (unity_scene, unity_tree, unity_node, unity_find,
// unity_diff) once the corresponding endpoints land in UnityScope.Runtime.
//
// For now this file documents the intended tool surface and proves the discovery
// + client wiring works end-to-end against /ping and /scene.

import { UnityScopeClient } from "./client.js";

async function main() {
  const client = new UnityScopeClient();
  const ping = await client.get("/ping");
  console.log("ping:", ping);

  const scene = await client.get("/scene");
  console.log("scene:", JSON.stringify(scene, null, 2));
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
