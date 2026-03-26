/**
 * @file ComfyConfig.js
 * @description Configuration for the ComfyUI API client.
 *
 * Edit the properties in this file to point to your ComfyUI server and
 * choose between single-user and multi-user mode before running the example.
 */

import { randomUUID } from 'node:crypto';

/**
 * Holds all configuration values used by the ComfyUI API client classes.
 *
 * A single shared instance is created at startup and passed into every class
 * that needs it, so there is one place to change any setting.
 */
export class ComfyConfig {
  /**
   * @param {object} [opts] - Optional overrides for every config value.
   * @param {string}  [opts.baseUrl='http://127.0.0.1:8188'] - HTTP base URL of the server.
   * @param {string}  [opts.wsUrl='ws://127.0.0.1:8188']     - WebSocket base URL.
   * @param {boolean} [opts.multiUser=false]
   *   Set to `true` when ComfyUI is started with `--multi-user`.
   *   In multi-user mode every request must include the `comfy-user` header
   *   so the server can isolate each user's settings and output files.
   * @param {string}  [opts.userId='alice']
   *   User identifier sent as the `comfy-user` header in multi-user mode.
   *   Any non-empty string is valid — username, UUID, or email.
   * @param {string}  [opts.clientId] - Stable client ID for this session.
   *   The same ID must be used for both the WebSocket connection and prompt
   *   submissions so the server routes previews back to this client.
   *   Defaults to a freshly generated UUID.
   */
  constructor({
    baseUrl = 'http://127.0.0.1:8188',
    wsUrl = 'ws://127.0.0.1:8188',
    multiUser = false,
    userId = 'alice',
    clientId = randomUUID(),
  } = {}) {
    /** @type {string} HTTP base URL of the ComfyUI server. */
    this.baseUrl = baseUrl;

    /** @type {string} WebSocket base URL of the ComfyUI server. */
    this.wsUrl = wsUrl;

    /**
     * @type {boolean}
     * Whether the server is running in multi-user mode (`--multi-user`).
     */
    this.multiUser = multiUser;

    /**
     * @type {string}
     * User identifier used in multi-user mode (`comfy-user` header value).
     */
    this.userId = userId;

    /**
     * @type {string}
     * Unique identifier for this client session.
     * Shared between HTTP prompt submissions and the WebSocket connection.
     */
    this.clientId = clientId;
  }
}
