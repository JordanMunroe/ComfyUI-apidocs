# WebSocket Messages

This page documents every message type emitted by the ComfyUI WebSocket server, the payload schema, and when each message appears during a workflow's lifecycle. Pair it with [Preview & Output Retrieval](./previews_and_outputs.md) for details on handling preview binaries and downloading final images. All messages are JSON objects unless otherwise noted.

## Connecting

1. **Generate a client ID** – Use a UUID or other unique string; the server uses it to route events. If you omit `clientId`, ComfyUI creates one and returns it in the first `status` message, but reconnects are easier when you control the value.
2. **Open the socket** – Connect to `ws://<host>:<port>/ws?clientId=<your-id>` (or `wss://` when TLS is enabled). Keep the socket open across prompts to reuse the same session.
3. **Advertise capabilities** – Optionally send a feature flag message right after `open` to let the server know which protocol extensions you support (e.g., preview metadata).
4. **Queue prompts with the same `client_id`** – When you call `POST /prompt`, pass the socket’s client ID so execution events route back to this connection.

### Minimal JavaScript example

```javascript
import WebSocket from 'ws';
import { randomUUID } from 'node:crypto';

const clientId = randomUUID();
const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${clientId}`);

ws.on('open', () => {
   ws.send(JSON.stringify({
      type: 'feature_flags',
      data: { supports_preview_metadata: true }
   }));
});

ws.on('message', (payload) => {
   const message = JSON.parse(payload.toString());
   console.log('WS event:', message.type, message.data);
});
```

> Replace `127.0.0.1:8188` with your ComfyUI host/port and keep `ws` referenced so the connection is not garbage-collected.

## Connection & Capability Negotiation

| Type | When | Payload |
|------|------|---------|
| `status` | Immediately after connecting. Sent again whenever queue state changes. | `{ "status": { "queue_remaining": <int> }, "sid": "<session-id>" }` |
| `feature_flags` | Response to a client sending `{ "type": "feature_flags", "data": { ... } }`. | Server capabilities, e.g. `{ "supports_preview_metadata": true, "max_upload_size": <bytes>, "extension": { "manager": { "supports_v4": true }}}` |
| Client request | First message you should send to advertise your capabilities. | `{ "type": "feature_flags", "data": { "supports_preview_metadata": true } }` |

## Execution Lifecycle Messages

| Type | Stage | Payload |
|------|-------|---------|
| `execution_start` | Prompt begins executing. | `{ "prompt_id": "uuid" }`
| `execution_cached` | Right after `execution_start` if cached outputs will be reused. | `{ "prompt_id": "uuid", "nodes": ["node_id", ...] }`
| `execution_error` | A node threw an exception that halted execution. | `{ "prompt_id": "uuid", "node_id": "id", "node_type": "Class", "executed": ["node"], "exception_message": "...", "exception_type": "...", "traceback": "...", "current_inputs": {...}, "current_outputs": [...] }`
| `execution_interrupted` | Workflow was interrupted manually (global or prompt-specific). | `{ "prompt_id": "uuid", "node_id": "id", "node_type": "Class", "executed": ["node"] }`
| `execution_success` | Workflow finished without errors. | `{ "prompt_id": "uuid" }`
| `executing` | Heartbeat while nodes run. Sent with `node` equal to the currently running node ID. When `node` becomes `null` and `prompt_id` matches your request, execution is complete. | `{ "prompt_id": "uuid", "node": "node_id" | null }`

## Progress Tracking

| Type | Contents |
|------|----------|
| `progress_state` | Aggregated progress for active nodes. `{ "prompt_id": "uuid", "nodes": { "node_id": { "value": <float>, "max": <float>, "state": "pending|running|finished|error", "display_node_id": "A/Readable Name", "parent_node_id": "parent", "real_node_id": "original graph id" }}}`. Only non-pending nodes are included.
| `status` | (also under connection) but repeated whenever queue length changes so you can show remaining tasks.

## Queue & System Notifications

| Type | Payload |
|------|---------|
| `status` | `{ "status": { "queue_remaining": <int> } }` whenever queue changes.
| `execution_interrupted` | (see above) doubles as a queue notification if another client interrupts you.

## Binary Events

Binary frames start with a 4-byte big-endian integer corresponding to `BinaryEventTypes` defined on the server. Messages include:

| Event Code | Name | Notes |
|------------|------|-------|
| `1` | `PREVIEW_IMAGE` | Encoded JPEG or PNG preview. Use when the client did **not** negotiate metadata support.
| `2` | `UNENCODED_PREVIEW_IMAGE` | Legacy unencoded previews. Rarely enabled.
| `4` | `PREVIEW_IMAGE_WITH_METADATA` | Encoded preview plus JSON metadata. Enabled when the client sends `supports_preview_metadata: true` during negotiation.

Each binary frame's payload layout is described in the preview/access guide. Most clients will focus on event `4` to receive node IDs alongside thumbnails.

## Message Order Summary

1. **Connect** to `/ws?clientId=<uuid>`.
2. Server sends `status` (includes `sid`).
3. Client optionally sends `feature_flags`. Server responds with its capabilities.
4. After you queue a prompt with matching `client_id`, the server emits:
   - `execution_start`
   - `execution_cached` (if applicable)
   - Multiple `progress_state` and `executing` messages, interleaved with binary preview frames.
   - `execution_success` (or `execution_error` / `execution_interrupted`).
5. Queue updates trigger additional `status` messages regardless of execution state.

Keep the WebSocket open between prompts; the server reuses your `clientId` to route events to the correct consumer.
