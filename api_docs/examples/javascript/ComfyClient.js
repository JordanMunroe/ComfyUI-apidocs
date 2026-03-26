/**
 * @file ComfyClient.js
 * @description HTTP API client for ComfyUI.
 *
 * Handles all HTTP communication with the server: checking status, queuing
 * workflows, and downloading generated images.  One instance should be
 * shared across the application to benefit from connection reuse.
 */

import { createWriteStream } from 'node:fs';
import { mkdir } from 'node:fs/promises';
import { pipeline } from 'node:stream/promises';

/**
 * HTTP client for the ComfyUI REST API.
 *
 * Wraps `fetch` and centralises header construction (including the optional
 * `comfy-user` header required in multi-user mode).
 *
 * @example
 * const config = new ComfyConfig({ multiUser: false });
 * const client = new ComfyClient(config);
 * await client.getServerStatus();
 */
export class ComfyClient {
  /**
   * @param {import('./ComfyConfig.js').ComfyConfig} config - Shared configuration instance.
   */
  constructor(config) {
    /** @type {import('./ComfyConfig.js').ComfyConfig} */
    this._config = config;
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  /**
   * Builds the HTTP headers for a request.
   *
   * In multi-user mode the `comfy-user` header is automatically added so
   * the server can isolate this user's settings and output files.
   *
   * @param {Record<string, string>} [extra] - Additional headers to merge in.
   * @returns {Record<string, string>} Combined headers object.
   */
  #buildHeaders(extra = {}) {
    const headers = { 'Content-Type': 'application/json', ...extra };
    if (this._config.multiUser) {
      headers['comfy-user'] = this._config.userId;
    }
    return headers;
  }

  // ---------------------------------------------------------------------------
  // Public API methods
  // ---------------------------------------------------------------------------

  /**
   * Fetches system statistics from the ComfyUI server.
   *
   * Use this to confirm the server is reachable and to log the running version
   * before submitting any work.
   *
   * @returns {Promise<object>} Parsed JSON from `GET /system_stats`.
   * @throws {Error} When the server is unreachable or returns a non-200 status.
   */
  async getServerStatus() {
    console.log('→ Checking server status …');

    const response = await fetch(`${this._config.baseUrl}/system_stats`, {
      headers: this.#buildHeaders(),
    });

    if (!response.ok) {
      throw new Error(`GET /system_stats failed: HTTP ${response.status}`);
    }

    const stats = await response.json();
    const version = stats?.system?.comfyui_version ?? 'unknown';
    console.log(`  ✓ Server online — ComfyUI ${version}`);
    return stats;
  }

  /**
   * Submits a workflow to the ComfyUI execution queue.
   *
   * The `client_id` from {@link ComfyConfig} ties this submission to the
   * WebSocket connection so the server routes progress events and preview
   * images back to this specific client.
   *
   * @param {object} workflow - Workflow graph built by {@link WorkflowBuilder}.
   * @returns {Promise<string>} The `prompt_id` assigned by the server.
   * @throws {Error} When the server rejects the prompt (e.g. invalid workflow).
   */
  async queueWorkflow(workflow) {
    console.log('\n→ Queuing workflow …');

    const body = {
      prompt: workflow,
      client_id: this._config.clientId, // must match the WebSocket clientId parameter
    };

    const response = await fetch(`${this._config.baseUrl}/prompt`, {
      method: 'POST',
      headers: this.#buildHeaders(),
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

  /**
   * Downloads a generated image from the ComfyUI server and saves it locally.
   *
   * Images are served by `GET /view` and identified by a combination of
   * `filename`, `subfolder`, and `type`:
   * - `filename`  — the filename returned in node outputs
   * - `subfolder` — subdirectory under ComfyUI's `output/` folder (often empty)
   * - `type`      — `"output"` for finished images, `"input"` for uploads
   *
   * @param {string} filename - Image filename from node output.
   * @param {string} [subfolder=''] - Subfolder within the output directory.
   * @param {string} [type='output'] - Storage type.
   * @param {string} [destDir='output'] - Local directory to save the image.
   * @returns {Promise<string>} Local file path where the image was saved.
   * @throws {Error} When the download request fails.
   */
  async downloadImage(filename, subfolder = '', type = 'output', destDir = 'output') {
    console.log(`\n→ Downloading ${filename} …`);

    const query = new URLSearchParams({ filename, subfolder, type });
    const response = await fetch(`${this._config.baseUrl}/view?${query}`, {
      // Omit Content-Type for GET requests
      headers: this.#buildHeaders({ 'Content-Type': '' }),
    });

    if (!response.ok) {
      throw new Error(`GET /view failed: HTTP ${response.status}`);
    }

    await mkdir(destDir, { recursive: true });
    const localPath = `${destDir}/${filename}`;

    // Stream directly to disk to avoid buffering large image files in memory
    await pipeline(response.body, createWriteStream(localPath));

    console.log(`  ✓ Saved → ${localPath}`);
    return localPath;
  }

  /**
   * Extracts image descriptors from the node output map returned after execution.
   *
   * The server returns outputs keyed by node ID; image filenames appear under
   * the `images` array of nodes such as `SaveImage`.
   *
   * @param {Record<string, object>} nodeOutputs - Output map from
   *   {@link WebSocketMonitor#waitForCompletion}.
   * @returns {Array<{ filename: string, subfolder: string, type: string }>}
   *   Image descriptors ready to pass to {@link ComfyClient#downloadImage}.
   */
  static extractImages(nodeOutputs) {
    const images = [];
    for (const output of Object.values(nodeOutputs)) {
      if (Array.isArray(output?.images)) {
        for (const img of output.images) {
          images.push({
            filename:  img.filename,
            subfolder: img.subfolder ?? '',
            type:      img.type ?? 'output',
          });
        }
      }
    }
    return images;
  }
}
