# WebSocket Monitoring and Progress Tracking

This example covers real-time monitoring of ComfyUI workflow execution via WebSocket, including binary preview image handling and progress bars.

> **Looking for a quick start?** The [Minimal API Example](./minimal-api-example.md) includes a complete WebSocket implementation you can run immediately.

---

## Connection

Connect using the same `clientId` you supply to `POST /prompt`:

```
ws://127.0.0.1:8188/ws?clientId=<client-id>
```

The server uses `clientId` to route events — progress updates and preview images for **your** prompt are delivered only to the WebSocket connection that shares the same ID.

---

## JSON Event Types

All non-binary messages are JSON objects with a `type` field and a `data` payload.

| Type | When fired | Key data fields |
|------|-----------|-----------------|
| `execution_start` | Server begins processing the prompt | `prompt_id` |
| `executing` | A node starts executing | `node`, `prompt_id` (`node` is `null` when all nodes finish) |
| `progress` | Periodic step update during sampling | `value`, `max`, `prompt_id`, `node` |
| `executed` | A node finished with outputs | `node`, `output`, `prompt_id` |
| `execution_cached` | A node's result was served from cache | `nodes`, `prompt_id` |
| `execution_error` | The prompt failed | `prompt_id`, `exception_message`, `node_id`, `traceback` |
| `status` | Queue status changed | `status.exec_info` |

---

## Binary Preview Images

When a sampler node (e.g. KSampler) is running, it periodically sends the current denoised image as a binary WebSocket frame. These previews let users see generation progress in real time.

### Format: Type 1 — PREVIEW_IMAGE

```
Bytes 0–3 : event type = 1 (big-endian uint32)
Bytes 4–7 : image format  (1 = JPEG, 2 = PNG)
Bytes 8…  : image data
```

### Format: Type 4 — PREVIEW_IMAGE_WITH_METADATA

```
Bytes 0–3      : event type = 4 (big-endian uint32)
Bytes 4–7      : metadata JSON length in bytes (big-endian uint32)
Bytes 8…(8+N)  : UTF-8 JSON metadata
Bytes (8+N)…   : image data
```

The metadata JSON may include fields like `node_id`, `prompt_id`, `image_type`, and `display_node_id`.

---

## Node.js Example

```javascript
// npm install ws
import WebSocket from 'ws';
import { writeFile, mkdir } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';

const CLIENT_ID = randomUUID();

/** Decode a binary WebSocket buffer into a preview image. */
function decodePreview(buffer) {
  const eventType = buffer.readUInt32BE(0);

  if (eventType === 1) {
    const formatCode = buffer.readUInt32BE(4);
    return { ext: formatCode === 1 ? 'jpg' : 'png', data: buffer.subarray(8) };
  }

  if (eventType === 4) {
    const metaLen = buffer.readUInt32BE(4);
    const metaEnd = 8 + metaLen;
    const meta = JSON.parse(buffer.subarray(8, metaEnd).toString('utf-8'));
    const mimeType = meta.image_type ?? 'image/png';
    return { ext: mimeType === 'image/jpeg' ? 'jpg' : 'png', data: buffer.subarray(metaEnd), meta };
  }

  return null;
}

async function monitorExecution(promptId) {
  await mkdir('output', { recursive: true });
  const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${CLIENT_ID}`);
  let previewIndex = 0;

  ws.on('open', () => console.log('Connected — waiting for prompt:', promptId));
  ws.on('error', console.error);

  ws.on('message', async (data, isBinary) => {
    if (isBinary) {
      // Binary: preview image frame
      const preview = decodePreview(Buffer.isBuffer(data) ? data : Buffer.from(data));
      if (preview) {
        const path = `output/preview_${++previewIndex}.${preview.ext}`;
        await writeFile(path, preview.data);
        console.log(`  📷 Preview saved → ${path}`);
      }
      return;
    }

    // JSON event
    const { type, data: d } = JSON.parse(data.toString());
    if (d?.prompt_id && d.prompt_id !== promptId) return; // ignore other clients

    switch (type) {
      case 'execution_start':
        console.log('▶ Execution started');
        break;

      case 'progress': {
        const pct = ((d.value / d.max) * 100).toFixed(1);
        process.stdout.write(`\r⏳ Sampling ${pct}% (${d.value}/${d.max})    `);
        break;
      }

      case 'executing':
        if (d.node == null) {
          console.log('\n✅ Complete');
          ws.close();
        } else {
          console.log(`\n⚙  Node ${d.node} …`);
        }
        break;

      case 'executed':
        console.log(`  Node ${d.node} outputs:`, d.output);
        break;

      case 'execution_error':
        console.error('✗ Error:', d.exception_message);
        ws.close();
        break;
    }
  });
}

monitorExecution('<your-prompt-id>').catch(console.error);
```

---

## Browser Example

```javascript
const clientId = crypto.randomUUID();
const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${clientId}`);

const previewImg  = document.getElementById('preview');
const progressBar = document.getElementById('progress-bar');
const progressTxt = document.getElementById('progress-text');
let previousUrl   = null;

ws.onmessage = async (event) => {
  if (event.data instanceof Blob) {
    // Binary: preview image
    const buf  = await event.data.arrayBuffer();
    const view = new DataView(buf);
    const eventType = view.getUint32(0);

    let imageBlob;

    if (eventType === 1) {
      const fmt = view.getUint32(4);
      imageBlob = new Blob([buf.slice(8)], { type: fmt === 1 ? 'image/jpeg' : 'image/png' });

    } else if (eventType === 4) {
      const metaLen   = view.getUint32(4);
      const metaBytes = new Uint8Array(buf, 8, metaLen);
      const meta      = JSON.parse(new TextDecoder().decode(metaBytes));
      imageBlob = new Blob([buf.slice(8 + metaLen)], { type: meta.image_type ?? 'image/png' });
    }

    if (imageBlob) {
      // Revoke the previous object URL to avoid memory leaks
      if (previousUrl) URL.revokeObjectURL(previousUrl);
      previousUrl = URL.createObjectURL(imageBlob);
      previewImg.src = previousUrl;
    }
    return;
  }

  // JSON event
  const { type, data } = JSON.parse(event.data);

  if (type === 'progress') {
    const pct = (data.value / data.max) * 100;
    progressBar.style.width = `${pct}%`;
    progressTxt.textContent = `Step ${data.value} / ${data.max}`;
  }
};
```

---

## See Also

- [Minimal API Example](./minimal-api-example.md) — Complete runnable Node.js and C# files
- [WebSocket Messages Reference](../websocket_messages.md) — Full event schema with field descriptions
- [Preview & Output Retrieval](../previews_and_outputs.md) — Binary format details
