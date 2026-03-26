/**
 * @file WebSocketMonitor.js
 * @description WebSocket monitor for ComfyUI workflow execution.
 */

import { mkdir, writeFile } from 'node:fs/promises';
import WebSocket from 'ws';

/** Connects to the ComfyUI WebSocket and processes events for a running prompt. */
export class WebSocketMonitor {
  /**
   * @param {import('./ComfyConfig.js').ComfyConfig} config
   * @param {string} [outputDir='output'] - Directory where preview images are saved.
   */
  constructor(config, outputDir = 'output') {
    this._config = config;
    this._outputDir = outputDir;
  }

  // Decodes a binary WebSocket frame into a preview image object.
  // Type 1 — PREVIEW_IMAGE:         [4B type][4B format][image bytes]
  // Type 4 — PREVIEW_IMAGE_METADATA:[4B type][4B meta len][JSON][image bytes]
  #decodePreviewImage(buffer) {
    const eventType = buffer.readUInt32BE(0);

    if (eventType === 1) {
      const formatCode = buffer.readUInt32BE(4);
      const extension = formatCode === 1 ? 'jpg' : 'png';
      const imageBytes = buffer.subarray(8);
      return { extension, imageBytes };
    }

    if (eventType === 4) {
      const metadataLength = buffer.readUInt32BE(4);
      const metadataStart = 8;
      const metadataEnd   = metadataStart + metadataLength;
      const metadataJson  = buffer.subarray(metadataStart, metadataEnd).toString('utf-8');
      const metadata      = JSON.parse(metadataJson);
      const imageBytes    = buffer.subarray(metadataEnd);
      const mimeType = metadata.image_type ?? 'image/png';
      const extension = mimeType === 'image/jpeg' ? 'jpg' : 'png';
      return { extension, imageBytes, metadata };
    }

    return null;
  }

  /**
   * Connects to the ComfyUI WebSocket and resolves when the prompt completes.
   *
   * @param {string} promptId - The `prompt_id` returned by {@link ComfyClient#queueWorkflow}.
   * @returns {Promise<Record<string, object>>} Node output map from `executed` events.
   */
  async waitForCompletion(promptId) {
    console.log('\n→ Connecting to WebSocket …');

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
        if (Object.keys(nodeOutputs).length === 0) {
          reject(new Error('WebSocket closed before execution completed'));
        }
      });

      ws.on('message', (data, isBinary) => {
        try {
          if (isBinary) {
            // Decode the preview and save it asynchronously.
            // The message handler never waits for the disk write.
            const preview = this.#decodePreviewImage(data);
            if (preview) {
              previewCount++;
              const filename = `${this._outputDir}/preview_${previewCount}.${preview.extension}`;
              const metaInfo = preview.metadata
                ? ` (node: ${preview.metadata.node_id ?? 'unknown'})`
                : '';
              console.log(`  📷 Preview ${previewCount} → ${filename}${metaInfo}`);
              writeFile(filename, preview.imageBytes).catch((err) =>
                console.error(`  ⚠ Failed to save preview: ${err.message}`),
              );
            }
          } else {
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

              default:
                if (msg.type !== 'execution_cached' && msg.type !== 'status') {
                  console.log(`  [ws] Unknown event: ${msg.type}`);
                }
            }
          }
        } catch (err) {
          console.error('  Error handling WebSocket message:', err);
        }
      });
    });
  }
}

