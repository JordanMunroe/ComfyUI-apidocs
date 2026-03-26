/**
 * ComfyUI Minimal API Example — JavaScript (Node.js 18+)
 *
 * Demonstrates:
 *  - Single-user mode (no auth header required)
 *  - Multi-user mode   (comfy-user header required)
 *  - Getting server status
 *  - Queuing a text-to-image workflow
 *  - Receiving real-time progress updates and preview images via WebSocket
 *  - Downloading the final generated image over HTTP
 *
 * Requirements:
 *  - Node.js 18 or newer (for global fetch, WebSocket, and crypto.randomUUID)
 *  - The 'ws' package for WebSocket support in Node.js:
 *      npm install ws
 *
 * Usage:
 *  node minimal-example.js
 *
 * Before running, make sure ComfyUI is running:
 *  python main.py                  # single-user mode (default)
 *  python main.py --multi-user     # multi-user mode
 */

import { createWriteStream } from 'node:fs';
import { mkdir, writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import { pipeline } from 'node:stream/promises';
import WebSocket from 'ws';

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

const BASE_URL = 'http://127.0.0.1:8188';
const WS_URL = 'ws://127.0.0.1:8188';

/**
 * Set MULTI_USER to true when ComfyUI is started with --multi-user.
 * In multi-user mode every request must include the `comfy-user` header so
 * the server can isolate each user's settings and output files.
 * In single-user mode the header is simply omitted.
 */
const MULTI_USER = false;

/**
 * Stable identifier for this client session.
 * Use the same ID for both the WebSocket connection and prompt submissions so
 * that the server routes live previews back to this specific client.
 */
const CLIENT_ID = randomUUID();

/**
 * User identifier used in multi-user mode.
 * Any non-empty string is valid — you can use a username, UUID, or email.
 */
const USER_ID = 'alice';

// ---------------------------------------------------------------------------
// Helper — build request headers
// ---------------------------------------------------------------------------

/**
 * Returns the base HTTP headers for every request.
 * In multi-user mode the `comfy-user` header is added so the server can
 * associate the request with a specific user's data directory.
 *
 * @param {Record<string, string>} [extra] - Additional headers to merge in.
 * @returns {Record<string, string>} Headers object.
 */
function buildHeaders(extra = {}) {
  const headers = { 'Content-Type': 'application/json', ...extra };
  if (MULTI_USER) {
    headers['comfy-user'] = USER_ID;
  }
  return headers;
}

// ---------------------------------------------------------------------------
// Step 1 — Check server status
// ---------------------------------------------------------------------------

/**
 * Fetches system statistics from the ComfyUI server.
 * Use this to confirm the server is reachable before submitting work.
 *
 * @returns {Promise<object>} Parsed JSON response from GET /system_stats.
 * @throws {Error} When the server is unreachable or returns a non-200 status.
 */
async function getServerStatus() {
  console.log('→ Checking server status …');
  const response = await fetch(`${BASE_URL}/system_stats`, {
    headers: buildHeaders(),
  });

  if (!response.ok) {
    throw new Error(`GET /system_stats failed: HTTP ${response.status}`);
  }

  const stats = await response.json();
  const version = stats?.system?.comfyui_version ?? 'unknown';
  console.log(`  ✓ Server online — ComfyUI ${version}`);
  return stats;
}

// ---------------------------------------------------------------------------
// Step 2 — Build a minimal text-to-image workflow
// ---------------------------------------------------------------------------

/**
 * Builds a minimal Stable Diffusion text-to-image workflow graph.
 *
 * Nodes (IDs map directly to the object keys):
 *  "1" — CheckpointLoaderSimple  : loads the model, CLIP encoder, and VAE
 *  "2" — CLIPTextEncode          : encodes the positive text prompt
 *  "3" — CLIPTextEncode          : encodes the negative text prompt
 *  "4" — EmptyLatentImage        : creates a blank latent canvas
 *  "5" — KSampler                : runs the diffusion sampling loop
 *  "6" — VAEDecode               : converts the latent tensor to pixel space
 *  "7" — SaveImage               : writes the final PNG to disk
 *
 * Node references use the format ["node_id", output_index]:
 *  ["1", 0] → MODEL output of CheckpointLoaderSimple
 *  ["1", 1] → CLIP  output of CheckpointLoaderSimple
 *  ["1", 2] → VAE   output of CheckpointLoaderSimple
 *
 * @param {string} positivePrompt - Text describing what to generate.
 * @param {string} [negativePrompt] - Text describing what to avoid.
 * @param {string} [checkpointName] - Model filename inside ComfyUI's models/ directory.
 * @returns {object} Workflow graph ready to submit to POST /prompt.
 */
function buildWorkflow(
  positivePrompt = 'a beautiful sunset over mountains, golden hour, photorealistic',
  negativePrompt = 'blurry, low quality, watermark',
  checkpointName = 'sd_xl_base_1.0.safetensors',
) {
  return {
    '1': {
      class_type: 'CheckpointLoaderSimple',
      inputs: { ckpt_name: checkpointName },
    },
    '2': {
      class_type: 'CLIPTextEncode',
      inputs: {
        text: positivePrompt,
        clip: ['1', 1], // CLIP from CheckpointLoader
      },
    },
    '3': {
      class_type: 'CLIPTextEncode',
      inputs: {
        text: negativePrompt,
        clip: ['1', 1], // CLIP from CheckpointLoader
      },
    },
    '4': {
      class_type: 'EmptyLatentImage',
      inputs: { width: 512, height: 512, batch_size: 1 },
    },
    '5': {
      class_type: 'KSampler',
      inputs: {
        seed: Math.floor(Math.random() * 2 ** 32), // random seed each run
        steps: 20,
        cfg: 7.0,           // classifier-free guidance scale
        sampler_name: 'euler',
        scheduler: 'normal',
        denoise: 1.0,       // 1.0 = full denoising (text-to-image)
        model: ['1', 0],    // MODEL from CheckpointLoader
        positive: ['2', 0], // CONDITIONING from positive CLIPTextEncode
        negative: ['3', 0], // CONDITIONING from negative CLIPTextEncode
        latent_image: ['4', 0], // LATENT from EmptyLatentImage
      },
    },
    '6': {
      class_type: 'VAEDecode',
      inputs: {
        samples: ['5', 0], // LATENT from KSampler
        vae: ['1', 2],     // VAE from CheckpointLoader
      },
    },
    '7': {
      class_type: 'SaveImage',
      inputs: {
        filename_prefix: 'minimal_example',
        images: ['6', 0], // IMAGE from VAEDecode
      },
    },
  };
}

// ---------------------------------------------------------------------------
// Step 3 — Queue the workflow
// ---------------------------------------------------------------------------

/**
 * Submits a workflow to the ComfyUI execution queue.
 *
 * The `client_id` ties this submission to the WebSocket connection so the
 * server sends progress events and previews back to this specific client.
 *
 * @param {object} workflow - Workflow graph built by buildWorkflow().
 * @returns {Promise<string>} The prompt_id assigned by the server.
 * @throws {Error} When the server rejects the prompt (e.g. invalid workflow).
 */
async function queueWorkflow(workflow) {
  console.log('\n→ Queuing workflow …');

  const body = {
    prompt: workflow,
    client_id: CLIENT_ID, // must match the WebSocket clientId query parameter
  };

  const response = await fetch(`${BASE_URL}/prompt`, {
    method: 'POST',
    headers: buildHeaders(),
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`POST /prompt failed: HTTP ${response.status} — ${text}`);
  }

  const result = await response.json();

  // The server may return node-level validation errors even with a 200 OK.
  if (result.node_errors && Object.keys(result.node_errors).length > 0) {
    console.warn('  ⚠ Node errors detected:', result.node_errors);
  }

  const promptId = result.prompt_id;
  console.log(`  ✓ Queued — prompt_id: ${promptId}`);
  return promptId;
}

// ---------------------------------------------------------------------------
// Step 4 — Monitor via WebSocket (previews + progress + completion)
// ---------------------------------------------------------------------------

/**
 * Decodes a binary preview image sent by the ComfyUI server over WebSocket.
 *
 * Two binary event formats are supported:
 *
 *  Type 1 — PREVIEW_IMAGE
 *    Bytes: [4B event type] [4B image format] [image bytes]
 *    Image format: 1 = JPEG, 2 = PNG
 *
 *  Type 4 — PREVIEW_IMAGE_WITH_METADATA
 *    Bytes: [4B event type] [4B metadata length] [UTF-8 JSON] [image bytes]
 *
 * @param {Buffer} buffer - Raw binary WebSocket message.
 * @returns {{ extension: string, imageBytes: Buffer, metadata?: object } | null}
 *   Decoded preview or null when the event type is not a preview.
 */
function decodePreviewImage(buffer) {
  const eventType = buffer.readUInt32BE(0);

  if (eventType === 1) {
    // PREVIEW_IMAGE: 4-byte event type + 4-byte format code + image data
    const formatCode = buffer.readUInt32BE(4);
    const extension = formatCode === 1 ? 'jpg' : 'png';
    const imageBytes = buffer.subarray(8);
    return { extension, imageBytes };
  }

  if (eventType === 4) {
    // PREVIEW_IMAGE_WITH_METADATA: metadata JSON prepended to image data
    const metadataLength = buffer.readUInt32BE(4);
    const metadataStart = 8;
    const metadataEnd = metadataStart + metadataLength;
    const metadataJson = buffer.subarray(metadataStart, metadataEnd).toString('utf-8');
    const metadata = JSON.parse(metadataJson);
    const imageBytes = buffer.subarray(metadataEnd);
    // Use image_type from metadata if available, fall back to PNG
    const mimeType = metadata.image_type ?? 'image/png';
    const extension = mimeType === 'image/jpeg' ? 'jpg' : 'png';
    return { extension, imageBytes, metadata };
  }

  // Unknown or unsupported binary event type — skip silently
  return null;
}

/**
 * Opens a WebSocket connection to ComfyUI and waits until the specified
 * prompt finishes executing.  Progress updates and preview images are
 * handled in real-time during the wait.
 *
 * The promise resolves with the node output data once the prompt is complete,
 * or rejects if an execution error is reported by the server.
 *
 * @param {string} promptId - The prompt_id returned by queueWorkflow().
 * @returns {Promise<object>} Node output map from the `executed` event.
 */
async function waitForCompletion(promptId) {
  console.log('\n→ Connecting to WebSocket …');

  // Pass the same clientId used during prompt submission so the server routes
  // events for this prompt to this WebSocket connection.
  const ws = new WebSocket(`${WS_URL}/ws?clientId=${CLIENT_ID}`);

  // Collect preview images so the caller can inspect the last one
  let previewCount = 0;
  let nodeOutputs = {};

  // Ensure the output directory exists for preview images
  await mkdir('output', { recursive: true });

  return new Promise((resolve, reject) => {
    ws.on('open', () => {
      console.log('  ✓ WebSocket connected — waiting for results …\n');
    });

    ws.on('error', (err) => {
      reject(new Error(`WebSocket error: ${err.message}`));
    });

    ws.on('close', () => {
      // Connection closed before we received the executed event — treat as an
      // error only if we never saw any outputs.
      if (Object.keys(nodeOutputs).length === 0) {
        reject(new Error('WebSocket closed before execution completed'));
      }
    });

    ws.on('message', async (data, isBinary) => {
      try {
        if (isBinary) {
          // ---------------------------------------------------------------
          // Binary message: preview image
          // ---------------------------------------------------------------
          const buffer = Buffer.isBuffer(data) ? data : Buffer.from(data);
          const preview = decodePreviewImage(buffer);

          if (preview) {
            previewCount++;
            const filename = `output/preview_${previewCount}.${preview.extension}`;
            await writeFile(filename, preview.imageBytes);

            const metaInfo = preview.metadata
              ? ` (node: ${preview.metadata.node_id ?? 'unknown'})`
              : '';
            console.log(`  📷 Preview ${previewCount} saved → ${filename}${metaInfo}`);
          }
        } else {
          // ---------------------------------------------------------------
          // JSON text message: status / progress / executed / error events
          // ---------------------------------------------------------------
          const msg = JSON.parse(data.toString());

          switch (msg.type) {
            case 'execution_start':
              // Fired once when the server begins processing our prompt
              if (msg.data?.prompt_id === promptId) {
                console.log('  ▶ Execution started');
              }
              break;

            case 'executing': {
              // Fired for each node as it begins executing; node is null when
              // the entire prompt finishes (all nodes are done).
              const { node, prompt_id } = msg.data ?? {};
              if (prompt_id !== promptId) break;

              if (node === null || node === undefined) {
                // null node means the entire prompt is done — close the socket
                console.log('\n  ✅ Execution complete');
                ws.close();
                resolve(nodeOutputs);
              } else {
                console.log(`  ⚙  Executing node ${node} …`);
              }
              break;
            }

            case 'progress': {
              // Periodic progress update during long-running nodes (e.g. KSampler)
              const { value, max, prompt_id } = msg.data ?? {};
              if (prompt_id !== promptId) break;

              const percent = ((value / max) * 100).toFixed(1);
              // Overwrite the same console line to avoid spamming the terminal
              process.stdout.write(`\r  ⏳ Sampling: ${percent}% (step ${value}/${max})    `);
              break;
            }

            case 'executed': {
              // A node finished and produced outputs (e.g. image filenames)
              const { node, output, prompt_id } = msg.data ?? {};
              if (prompt_id !== promptId) break;

              nodeOutputs[node] = output;
              break;
            }

            case 'execution_error': {
              // The server reported an error — reject the promise
              const { prompt_id, exception_message } = msg.data ?? {};
              if (prompt_id !== promptId) break;

              ws.close();
              reject(new Error(`Execution error: ${exception_message}`));
              break;
            }

            case 'execution_cached':
              // Nodes that hit the cache are reported here; no action needed
              break;

            case 'status':
              // Overall queue status — useful for diagnostics but not required
              break;

            default:
              // Unrecognised event type — log for debugging
              console.log(`  [ws] Unknown event: ${msg.type}`);
          }
        }
      } catch (err) {
        console.error('  Error handling WebSocket message:', err);
      }
    });
  });
}

// ---------------------------------------------------------------------------
// Step 5 — Download the final generated image
// ---------------------------------------------------------------------------

/**
 * Downloads a generated image from the ComfyUI server and saves it locally.
 *
 * Images live under the /view endpoint and are identified by:
 *  - filename  : the filename returned in node outputs
 *  - subfolder : subdirectory under ComfyUI's output/ folder (often empty)
 *  - type      : "output" for finished images, "input" for uploaded images
 *
 * @param {string} filename - Image filename from node output.
 * @param {string} [subfolder] - Subfolder within the output directory.
 * @param {string} [type] - Storage type ("output" or "input").
 * @returns {Promise<string>} Local path where the image was saved.
 */
async function downloadImage(filename, subfolder = '', type = 'output') {
  console.log(`\n→ Downloading ${filename} …`);

  const query = new URLSearchParams({ filename, subfolder, type });
  const response = await fetch(`${BASE_URL}/view?${query}`, {
    headers: buildHeaders({ 'Content-Type': '' }), // no Content-Type for GET
  });

  if (!response.ok) {
    throw new Error(`GET /view failed: HTTP ${response.status}`);
  }

  await mkdir('output', { recursive: true });
  const localPath = `output/${filename}`;

  // Stream the response body directly to disk to avoid buffering large files
  await pipeline(response.body, createWriteStream(localPath));

  console.log(`  ✓ Saved → ${localPath}`);
  return localPath;
}

// ---------------------------------------------------------------------------
// Step 6 — Extract image filenames from node outputs
// ---------------------------------------------------------------------------

/**
 * Searches the node output map returned after execution for image filenames.
 *
 * The server returns outputs per node; image filenames appear under the
 * "images" key of SaveImage (and similar) nodes.
 *
 * @param {Record<string, object>} nodeOutputs - Output map from waitForCompletion().
 * @returns {Array<{ filename: string, subfolder: string, type: string }>}
 *   List of image descriptors ready to pass to downloadImage().
 */
function extractImages(nodeOutputs) {
  const images = [];
  for (const output of Object.values(nodeOutputs)) {
    if (Array.isArray(output?.images)) {
      for (const img of output.images) {
        images.push({
          filename: img.filename,
          subfolder: img.subfolder ?? '',
          type: img.type ?? 'output',
        });
      }
    }
  }
  return images;
}

// ---------------------------------------------------------------------------
// Main entry point
// ---------------------------------------------------------------------------

async function main() {
  console.log('=== ComfyUI Minimal API Example ===');
  console.log(`Mode    : ${MULTI_USER ? `multi-user (user: ${USER_ID})` : 'single-user'}`);
  console.log(`ClientID: ${CLIENT_ID}\n`);

  // 1. Verify the server is reachable
  await getServerStatus();

  // 2. Build a simple text-to-image workflow
  const workflow = buildWorkflow();

  // 3. Submit the workflow to the execution queue
  const promptId = await queueWorkflow(workflow);

  // 4. Wait for completion, collecting previews along the way
  const nodeOutputs = await waitForCompletion(promptId);

  // 5. Download all generated images
  const images = extractImages(nodeOutputs);

  if (images.length === 0) {
    console.log('\n⚠ No images found in node outputs.');
  } else {
    for (const img of images) {
      await downloadImage(img.filename, img.subfolder, img.type);
    }
    console.log(`\n✅ Done! ${images.length} image(s) downloaded to ./output/`);
  }
}

main().catch((err) => {
  console.error('\n✗ Fatal error:', err.message);
  process.exit(1);
});
