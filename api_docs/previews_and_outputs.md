# Preview & Output Retrieval

This guide shows how to collect images from ComfyUI while a workflow is running and after it finishes. Use the WebSocket channel for low-latency previews and workflow status, then fall back to HTTP endpoints to download the final image artifacts in deterministic formats. For a complete catalog of WebSocket event names and payloads, see [WebSocket Messages](./websocket_messages.md).

## Real-Time Previews (WebSockets)

1. **Connect with a stable client ID** – Generate a UUID and open `ws://<host>:<port>/ws?clientId=<uuid>`. The same ID scopes events and lets you reconnect without missing progress updates.
2. **Negotiate feature flags (optional but recommended)** – Immediately send:
   ```json
   {"type":"feature_flags","data":{"supports_preview_metadata":true}}
   ```
   The server responds with its own feature list. Advertising `supports_preview_metadata` unlocks preview frames that include node IDs, prompt IDs, and display metadata. If you skip the negotiation you still receive previews, but without metadata.
3. **Listen for JSON events** – Every text frame is a JSON envelope with `type` and `data`. Common events:
   - `status`: queue depth and your `sid` (session ID).
   - `execution_start`: your prompt began executing.
   - `execution_cached`: nodes skipped because cached outputs were reused.
   - `progress_state`: aggregated per-node progress with `value`, `max`, `display_node_id`, and `state` (`running`, `finished`, etc.).
   - `executing`: de facto heartbeat. When `data.node` becomes `null` for your `prompt_id`, execution is done.
   - `execution_success`, `execution_error`, `execution_interrupted`: terminal events for success, failure, or manual interrupt.
4. **Handle binary preview frames** – Binary WebSocket frames always begin with a 4-byte big-endian integer indicating the `BinaryEventTypes` enum. Relevant values:
   - `1 (PREVIEW_IMAGE)`: legacy previews. Payload begins with a 4-byte integer describing the image encoding (1 = JPEG, 2 = PNG), followed by the encoded bytes.
   - `2 (UNENCODED_PREVIEW_IMAGE)`: rarely used raw previews; payload is a pickled tuple. Skip unless you control the server.
   - `4 (PREVIEW_IMAGE_WITH_METADATA)`: modern previews. Payload layout:
     1. 4-byte big-endian metadata length.
     2. UTF-8 JSON metadata (includes `prompt_id`, `node_id`, `display_node_id`, `parent_node_id`, and `image_type`).
     3. Encoded JPEG or PNG bytes.

### Node.js listener example

```javascript
import WebSocket from 'ws';
import { createWriteStream } from 'node:fs';
import { randomUUID } from 'node:crypto';

const clientId = randomUUID();
const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${clientId}`);

ws.on('open', () => {
  ws.send(JSON.stringify({
    type: 'feature_flags',
    data: { supports_preview_metadata: true }
  }));
});

ws.on('message', (payload, isBinary) => {
  if (!isBinary) {
    const message = JSON.parse(payload.toString());
    if (message.type === 'progress_state') {
      console.log('Progress:', Object.values(message.data.nodes).map(n => `${n.display_node_id}:${n.state}`));
    }
    if (message.type === 'execution_success') {
      console.log('Prompt finished:', message.data.prompt_id);
    }
    return;
  }

  const view = new DataView(payload.buffer, payload.byteOffset, payload.byteLength);
  const eventType = view.getUint32(0); // BinaryEventTypes
  if (eventType !== 4) return; // ignore previews without metadata
  const metadataLength = view.getUint32(4);
  const metaBytes = payload.subarray(8, 8 + metadataLength);
  const metadata = JSON.parse(Buffer.from(metaBytes).toString('utf8'));
  const imageBytes = payload.subarray(8 + metadataLength);
  const filename = `${metadata.prompt_id}_${metadata.display_node_id}.jpg`;
  createWriteStream(filename).end(imageBytes);
  console.log('Preview saved for', metadata.display_node_id);
});
```

### Python snippet (websocket-client)

```python
import websocket, json, uuid
from io import BytesIO
from PIL import Image

client_id = uuid.uuid4().hex
ws = websocket.WebSocket()
ws.connect(f"ws://127.0.0.1:8188/ws?clientId={client_id}")
ws.send(json.dumps({"type": "feature_flags", "data": {"supports_preview_metadata": True}}))

while True:
    frame = ws.recv()
    if isinstance(frame, str):
        message = json.loads(frame)
        if message["type"] == "execution_success":
            print("Run finished", message["data"]["prompt_id"])
            break
        continue

    event_type = int.from_bytes(frame[:4], "big")
    if event_type != 4:
        continue
    meta_len = int.from_bytes(frame[4:8], "big")
    metadata = json.loads(frame[8:8 + meta_len])
    img_bytes = frame[8 + meta_len:]
    preview = Image.open(BytesIO(img_bytes))
    preview.save(f"preview_{metadata['display_node_id']}.jpg")
```

> **Tip:** Keep the WebSocket open even after a workflow finishes to reuse the session for subsequent prompts. ComfyUI automatically cleans up old sockets when you reconnect with the same `clientId`.

## Deterministic Outputs (HTTP)

Even though previews travel over WebSocket, the authoritative artifacts still live on disk. Use the REST API to list, download, or convert them once the WebSocket notifies you that a run is complete.

### 1. Inspect history

- `GET /history/{prompt_id}` returns the most recent execution record for that prompt ID.
- Each node entry under `outputs` contains an `images` array. Every image describes `filename`, `subfolder`, and `type` (usually `output`, `temp`, or `input`).
- `GET /history?max_items=10&offset=0` paginates recent prompts if you do not track IDs client-side.

```bash
curl http://127.0.0.1:8188/history/550e8400-e29b-41d4-a716-446655440000 | jq '."550e8400-e29b-41d4-a716-446655440000".outputs'
```

### 2. Download the file with `/view`

The `/view` endpoint streams the binary image and can downsample or change format on the fly:

```
GET /view?filename=<name>&type=<output|input|temp>&subfolder=<path>&preview=<webp;jpeg quality>&channel=<rgb|rgba|a>
```

Examples:
- `GET /view?filename=image.png&type=output` – raw PNG.
- `GET /view?filename=image.png&preview=webp;80` – convert to WEBP (quality 80) without touching the source file.
- `GET /view?filename=subfolder/img.png&subfolder=myset&type=input&channel=a` – alpha channel only.

### JavaScript download helper

```javascript
async function fetchOutputImage(imageMeta) {
  const params = new URLSearchParams({
    filename: imageMeta.filename,
    subfolder: imageMeta.subfolder,
    type: imageMeta.type,
    preview: 'webp;85'
  });
  const res = await fetch(`http://127.0.0.1:8188/view?${params}`);
  if (!res.ok) throw new Error('Image fetch failed');
  return await res.arrayBuffer();
}
```

### Putting it together

1. Submit the workflow with `client_id` set to the WebSocket session.
2. Watch WebSocket `progress_state` and preview frames to drive your UI.
3. When you receive `execution_success`, call `GET /history/{prompt_id}`.
4. Loop through each `outputs[node_id].images` entry, and request `/view` with the provided filename/subfolder/type triple to obtain the canonical artifacts.

This split design gives you the best of both worlds: instant previews through WebSocket streaming and tamper-proof assets through stateless HTTP requests.
