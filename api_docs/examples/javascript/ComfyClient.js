/**
 * @file ComfyClient.js
 * @description HTTP API client for ComfyUI.
 */

import { createWriteStream } from 'node:fs';
import { mkdir } from 'node:fs/promises';
import { pipeline } from 'node:stream/promises';

/** HTTP client for the ComfyUI REST API. */
export class ComfyClient {
  /** @param {import('./ComfyConfig.js').ComfyConfig} config */
  constructor(config) {
    this._config = config;
  }

  // Builds request headers, adding comfy-user in multi-user mode.
  #buildHeaders(extra = {}) {
    const headers = { 'Content-Type': 'application/json', ...extra };
    if (this._config.multiUser) {
      headers['comfy-user'] = this._config.userId;
    }
    return headers;
  }

  /** Checks server reachability and logs the ComfyUI version. */
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

  /** Submits a workflow to the queue and returns the assigned `prompt_id`. */
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

    if (result.node_errors && Object.keys(result.node_errors).length > 0) {
      console.warn('  ⚠ Node errors detected:', result.node_errors);
    }

    const promptId = result.prompt_id;
    console.log(`  ✓ Queued — prompt_id: ${promptId}`);
    return promptId;
  }

  /** Downloads an image from `GET /view` and saves it locally. */
  async downloadImage(filename, subfolder = '', type = 'output', destDir = 'output') {
    console.log(`\n→ Downloading ${filename} …`);

    const query = new URLSearchParams({ filename, subfolder, type });
    const response = await fetch(`${this._config.baseUrl}/view?${query}`, {
      headers: this.#buildHeaders({ 'Content-Type': '' }),
    });

    if (!response.ok) {
      throw new Error(`GET /view failed: HTTP ${response.status}`);
    }

    await mkdir(destDir, { recursive: true });
    const localPath = `${destDir}/${filename}`;

    await pipeline(response.body, createWriteStream(localPath));

    console.log(`  ✓ Saved → ${localPath}`);
    return localPath;
  }

  /** Extracts image descriptors from node outputs returned after execution. */
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

