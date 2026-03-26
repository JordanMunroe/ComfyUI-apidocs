/**
 * @file ComfyConfig.js
 * @description Configuration for the ComfyUI API client.
 * Edit this file to point to your server before running the example.
 */

import { randomUUID } from 'node:crypto';

/** Holds configuration values shared by all ComfyUI client classes. */
export class ComfyConfig {
  /**
   * @param {object}  [opts]
   * @param {string}  [opts.baseUrl='http://127.0.0.1:8188']
   * @param {string}  [opts.wsUrl='ws://127.0.0.1:8188']
   * @param {boolean} [opts.multiUser=false] - Set to true when running with --multi-user.
   * @param {string}  [opts.userId='alice']  - Sent as the comfy-user header in multi-user mode.
   * @param {string}  [opts.clientId]        - Shared between HTTP and WebSocket; defaults to a new UUID.
   */
  constructor({
    baseUrl = 'http://127.0.0.1:8188',
    wsUrl = 'ws://127.0.0.1:8188',
    multiUser = false,
    userId = 'alice',
    clientId = randomUUID(),
  } = {}) {
    this.baseUrl   = baseUrl;
    this.wsUrl     = wsUrl;
    this.multiUser = multiUser;
    this.userId    = userId;
    this.clientId  = clientId;
  }
}

