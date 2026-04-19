// Smoke test against a running plugin. Not part of the MCP server — run with
// `npm run smoke` to verify discovery + transport from outside an MCP client.

import { UnityScopeClient } from "./client.js";

async function main() {
  const client = new UnityScopeClient();
  console.log("connected:", client.describe());
  console.log("ping:", await client.get("/ping"));
  const scene: any = await client.get("/scene");
  console.log(`scene: ${scene.scene} (${scene.canvas_count} canvases)`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
