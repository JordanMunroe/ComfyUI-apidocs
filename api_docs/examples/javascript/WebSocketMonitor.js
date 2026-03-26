/**
 * @file WebSocketMonitor.js
 * @description Real-time WebSocket monitor for ComfyUI workflow execution.
 *
 * Connects to the ComfyUI WebSocket endpoint and handles all server-pushed
 * events: execution lifecycle, per-step progress, and binary preview images.
 */

import { mkdir, writeFile } from 'node:fs/promises';
import WebSocket from 'ws';

/**
 * Monitors a ComfyUI workflow execution via WebSocket.
 *
 * Opens a persistent WebSocket connection and processes:
 * - JSON events: `execution_start`, `executing`, `progress`, `executed`, `execution_error`
 * - Binary frames: encoded preview images (Type 1 and Type 4)
 *
 * Preview images are saved to disk as they arrive so you can watch generation
 * progress in real time.
 *
 * @example
 * const monitor = new WebSocketMonitor(config);
 * const nodeOutputs = await monitor.waitForCompletion(promptId);
 */
export class WebSocketMonitor {
  /**
   * @param {import('./ComfyConfig.js').ComfyConfig} config - Shared configuration instance.
   * @param {string} [outputDir='output'] - Directory where preview images are saved.
   */
  constructor(config, outputDir = 'output') {
    /** @type {import('./ComfyConfig.js').ComfyConfig} */
    this._config = config;

    /** @type {string} Local directory for preview image output. */
    this._outputDir = outputDir;
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  /**
   * Decodes a binary WebSocket message into a preview image.
   *
   * ComfyUI sends preview images as binary frames with a small header:
   *
   * **Type 1 — PREVIEW_IMAGE**
   * ```
   * [4B: event type = 1] [4B: format (1=JPEG, 2=PNG)] [image bytes]
   * ```
   *
   * **Type 4 — PREVIEW_IMAGE_WITH_METADATA**
   * ```
   * [4B: event type = 4] [4B: metadata length] [UTF-8 JSON] [image bytes]
   * ```
   *
   * @param {Buffer} buffer - Raw binary data from the WebSocket.
   * @returns {{ extension: string, imageBytes: Buffer, metadata?: object } | null}
   *   Decoded preview, or `null` for unrecognised event types.
   */
  #decodePreviewImage(buffer) {
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
      const metadataEnd   = metadataStart + metadataLength;
      const metadataJson  = buffer.subarray(metadataStart, metadataEnd).toString('utf-8');
      const metadata      = JSON.parse(metadataJson);
      const imageBytes    = buffer.subarray(metadataEnd);
      // Use image_type from metadata when available, fall back to PNG
      const mimeType = metadata.image_type ?? 'image/png';
      const extension = mimeType === 'image/jpeg' ? 'jpg' : 'png';
      return { extension, imageBytes, metadata };
    }

    return null; // Unknown or unsupported binary event type
  }

  // ---------------------------------------------------------------------------
  // Public API
  // ---------------------------------------------------------------------------

  /**
   * Opens a WebSocket connection to ComfyUI and waits until the specified
   * prompt finishes executing.
   *
   * Progress events are logged to the console as they arrive.  Preview images
   * are saved to `outputDir` with sequential filenames (`preview_1.jpg`, …).
   *
   * The promise resolves with the map of node outputs once the `executing`
   * event with `node: null` is received (signalling that all nodes are done).
   * It rejects if the server reports an `execution_error` for this prompt.
   *
   * @param {string} promptId - The `prompt_id` returned by
   *   {@link ComfyClient#queueWorkflow}.
   * @returns {Promise<Record<string, object>>} Node output map from `executed` events.
   * @throws {Error} On WebSocket connection failure or server-side execution error.
   */
  async waitForCompletion(promptId) {
    console.log('\n→ Connecting to WebSocket …');

    // The clientId in the URL must match the one used when queuing the prompt
    // so the server routes events for this prompt back to this connection.
    const ws = new WebSocket(
      `${this._config.wsUrl}/ws?clientId=${this._config.clientId}`,
    );

    await mkdir(this._outputDir, { recursive: true });

    let previewCount = 0;
    const nodeOutputs = {};

    return new Promise((resolve, reject) => {
      ws.on('open', () => {
        console.log('  ✓ WebSocket connected — waiting for results …\n');
      });

      ws.on('error', (err) => {
        reject(new Error(`WebSocket error: ${err.message}`));
      });

      ws.on('close', () => {
        // If the socket closed before we received any outputs, treat it as an error.
        if (Object.keys(nodeOutputs).length === 0) {
          reject(new Error('WebSocket closed before execution completed'));
        }
      });

      ws.on('message', async (data, isBinary) => {
        try {
          if (isBinary) {
            // -------------------------------------------------------------------
            // Binary frame: preview image (generated during sampling)
            // The `ws` library always provides a Buffer for binary messages in
            // Node.js, so we can use `data` directly without conversion.
            // -------------------------------------------------------------------
            const preview = this.#decodePreviewImage(data);

            if (preview) {
              previewCount++;
              const filename = `${this._outputDir}/preview_${previewCount}.${preview.extension}`;
              await writeFile(filename, preview.imageBytes);

              const metaInfo = preview.metadata
                ? ` (node: ${preview.metadata.node_id ?? 'unknown'})`
                : '';
              console.log(`  📷 Preview ${previewCount} saved → ${filename}${metaInfo}`);
            }
          } else {
            // -------------------------------------------------------------------
            // JSON text frame: lifecycle / progress / output events
            // -------------------------------------------------------------------
            const msg = JSON.parse(data.toString());

            switch (msg.type) {
              case 'execution_start':
                if (msg.data?.prompt_id === promptId) {
                  console.log('  ▶ Execution started');
                }
                break;

              case 'executing': {
                const { node, prompt_id } = msg.data ?? {};
                if (prompt_id !== promptId) break;

                if (node === null || node === undefined) {
                  // null node means all nodes are done — close and resolve
                  console.log('\n  ✅ Execution complete');
                  ws.close();
                  resolve(nodeOutputs);
                } else {
                  console.log(`  ⚙  Executing node ${node} …`);
                }
                break;
              }

              case 'progress': {
                const { value, max, prompt_id } = msg.data ?? {};
                if (prompt_id !== promptId) break;

                const percent = ((value / max) * 100).toFixed(1);
                // Overwrite the same line to avoid flooding the terminal
                process.stdout.write(`\r  ⏳ Sampling: ${percent}% (step ${value}/${max})    `);
                break;
              }

              case 'executed': {
                const { node, output, prompt_id } = msg.data ?? {};
                if (prompt_id !== promptId) break;
                nodeOutputs[node] = output;
                break;
              }

              case 'execution_error': {
                const { prompt_id, exception_message } = msg.data ?? {};
                if (prompt_id !== promptId) break;
                ws.close();
                reject(new Error(`Execution error: ${exception_message}`));
                break;
              }

              case 'execution_cached':
                // Node served from cache — no action required
                break;

              case 'status':
                // Overall queue status — useful for diagnostics, not needed here
                break;

              default:
                console.log(`  [ws] Unknown event: ${msg.type}`);
            }
          }
        } catch (err) {
          console.error('  Error handling WebSocket message:', err);
        }
      });
    });
  }
}
