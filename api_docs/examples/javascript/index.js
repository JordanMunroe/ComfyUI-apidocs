/**
 * @file index.js
 * @description Entry point for the ComfyUI minimal API example.
 *
 * Orchestrates the four OOP classes to demonstrate the full workflow:
 *  1. Check server status
 *  2. Build a text-to-image workflow
 *  3. Queue the workflow
 *  4. Monitor progress and preview images via WebSocket
 *  5. Download the final generated image
 *
 * Requirements:
 *  - Node.js 18+ (for global fetch and crypto.randomUUID)
 *  - npm install   (installs the `ws` WebSocket package)
 *
 * Usage:
 *  npm start          (or: node index.js)
 *
 * Before running, start ComfyUI:
 *  python main.py                  # single-user mode (default)
 *  python main.py --multi-user     # multi-user mode
 */

import { ComfyConfig }        from './ComfyConfig.js';
import { ComfyClient }        from './ComfyClient.js';
import { WorkflowBuilder }    from './WorkflowBuilder.js';
import { WebSocketMonitor }   from './WebSocketMonitor.js';

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

/**
 * Shared configuration for this session.
 *
 * Set `multiUser: true` when ComfyUI is started with `--multi-user`.
 * All other values default to the standard local development setup.
 */
const config = new ComfyConfig({
  multiUser: false,   // flip to true for --multi-user server
  userId: 'alice',    // only used in multi-user mode
});

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  console.log('=== ComfyUI Minimal API Example ===');
  console.log(`Mode    : ${config.multiUser ? `multi-user (user: ${config.userId})` : 'single-user'}`);
  console.log(`ClientID: ${config.clientId}\n`);

  // Create one instance of each service class, passing the shared config.
  const client  = new ComfyClient(config);
  const builder = new WorkflowBuilder();
  const monitor = new WebSocketMonitor(config);

  // 1. Verify the server is reachable.
  await client.getServerStatus();

  // 2. Build a text-to-image workflow graph.
  const workflow = builder.buildTxt2Img(
    'a beautiful sunset over mountains, golden hour, photorealistic',
    'blurry, low quality, watermark',
  );

  // 3. Submit the workflow to the execution queue.
  const promptId = await client.queueWorkflow(workflow);

  // 4. Wait for execution to finish, collecting previews along the way.
  const nodeOutputs = await monitor.waitForCompletion(promptId);

  // 5. Download every generated image to ./output/.
  const images = ComfyClient.extractImages(nodeOutputs);

  if (images.length === 0) {
    console.log('\n⚠ No images found in node outputs.');
  } else {
    for (const img of images) {
      await client.downloadImage(img.filename, img.subfolder, img.type);
    }
    console.log(`\n✅ Done! ${images.length} image(s) downloaded to ./output/`);
  }
}

main().catch((err) => {
  console.error('\n✗ Fatal error:', err.message);
  process.exit(1);
});
