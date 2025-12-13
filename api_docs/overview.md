# API Overview

## Introduction

ComfyUI is a powerful and modular visual AI engine that provides a comprehensive REST API and WebSocket interface for workflow execution, queue management, and resource handling. This documentation covers all available API endpoints and their usage.

**Version:** 0.3.76 (Check `/system_stats` endpoint for current version)  
**Protocol:** HTTP/HTTPS with WebSocket support  
**Data Format:** JSON (with binary WebSocket support for images)

---

## Base URL

All API endpoints are available at:

```
http://<host>:<port>/
```

Most endpoints are also available with the `/api` prefix for frontend development:

```
http://<host>:<port>/api/
```

**Default:** `http://127.0.0.1:8188`

### Response Compression

The server automatically supports gzip compression for JSON and text responses when the client includes `Accept-Encoding: gzip` in request headers.

### Caching

The API implements cache control middleware to manage resource caching:
- Frontend files (index.html) are served with `no-cache` headers
- Static resources may have appropriate cache headers set
- API responses are typically not cached

---

## Authentication

### Multi-User Mode

When ComfyUI is running in multi-user mode (`--multi-user` flag), include the user ID in request headers:

```http
comfy-user: <user_id>
```

### Single-User Mode

No authentication required in single-user mode (default).

### API Nodes

API nodes can be disabled with the `--disable-api-nodes` command-line flag. When disabled, certain advanced node types will not be available.

### CORS (Cross-Origin Resource Sharing)

ComfyUI supports CORS configuration through command-line arguments:
- `--enable-cors-header <origin>`: Enable CORS with specified origin (e.g., "https://example.com" or "*")
- CORS headers are automatically added to all responses when enabled

### Security Options

- `--listen [address]`: Listen on specified network address (default: 127.0.0.1)
- `--enable-origin-check-only`: Only allow requests from same origin
- External access restrictions can be configured via middleware

### ComfyUI Manager

When the `--enable-manager` flag is used, additional manager-related middleware and endpoints may be available. See ComfyUI Manager documentation for details.

---

## WebSocket Connection

WebSocket connections provide real-time, bidirectional communication between your client and ComfyUI server. This is the recommended way to monitor workflow execution, receive progress updates, and get live preview images. The WebSocket connection remains open throughout your session, allowing the server to push updates to your client immediately as they occur, rather than requiring you to poll for status changes.

**Key Benefits:**
- Real-time execution updates and progress tracking
- Efficient binary image previews
- Reduced server load compared to HTTP polling
- Immediate error notifications

### Connect to WebSocket

```
ws://<host>:<port>/ws?clientId=<client_id>
```

**Parameters:**
- `clientId` (optional): Unique identifier for the client. If not provided, a new UUID will be generated.

### WebSocket Message Format

**Client to Server:**
```json
{
  "type": "feature_flags",
  "data": {
    "flag_name": true
  }
}
```

**Server to Client:**
```json
{
  "type": "status|executing|progress|executed|execution_start|execution_cached|execution_error",
  "data": { /* event-specific data */ }
}
```

### WebSocket Events

| Event Type | Description |
|------------|-------------|
| `status` | Queue status update with current queue information |
| `executing` | Currently executing node information |
| `progress` | Progress update with value/max |
| `executed` | Node execution completed with outputs |
| `execution_start` | Workflow execution started |
| `execution_cached` | Node output retrieved from cache |
| `execution_error` | Execution error occurred |
| `feature_flags` | Feature flags exchange between client and server |

### WebSocket Binary Messages

The server can also send binary messages for efficient image preview transmission. Binary messages use the following format:

**Binary Message Structure:**
```
[4 bytes: Event Type (big-endian uint32)] [payload data]
```

**Binary Event Types:**
- `1` - PREVIEW_IMAGE: Encoded preview image with format header
- `2` - UNENCODED_PREVIEW_IMAGE: Raw preview image data
- `3` - TEXT: Text message
- `4` - PREVIEW_IMAGE_WITH_METADATA: Preview image with JSON metadata

**PREVIEW_IMAGE Format (Type 1):**
```
[4 bytes: Event Type = 1] [4 bytes: Image Format] [image data]
```
- Image Format: `1` = JPEG, `2` = PNG

**PREVIEW_IMAGE_WITH_METADATA Format (Type 4):**
```
[4 bytes: Event Type = 4] [4 bytes: Metadata Length] [JSON metadata] [image data]
```

---

## Previews and Progress Tracking

Previews are one of ComfyUI's most powerful features for monitoring long-running image generation workflows. During execution, ComfyUI can send progressive preview images showing the current state of generation, allowing users to see results in real-time and make informed decisions about whether to continue or interrupt the process. Preview images are transmitted via WebSocket using efficient binary encoding to minimize bandwidth while maintaining responsiveness.

**Key Features:**
- Real-time progress updates during workflow execution
- Binary-encoded preview images for efficiency
- Multiple preview formats (JPEG for speed, PNG for quality)
- Optional metadata with preview context
- Configurable preview size and quality
- Progress percentage tracking

### How Previews Work

1. **During Execution**: When a node supports previews (like samplers during diffusion), it periodically sends preview images
2. **WebSocket Delivery**: Previews are sent as binary WebSocket messages to connected clients
3. **Client Display**: Your client receives and displays the preview, updating as generation progresses
4. **Progress Events**: Separate progress events provide numerical completion percentage

### Preview Message Types

#### Progress Event (JSON)

Sent periodically to indicate execution progress as a percentage.

**Message Type:** `progress`

**Format:**
```json
{
  "type": "progress",
  "data": {
    "value": 15,
    "max": 20,
    "prompt_id": "550e8400-e29b-41d4-a716-446655440000",
    "node": "3"
  }
}
```

**Fields:**
- `value`: Current step number
- `max`: Total steps for this operation
- `prompt_id`: ID of the executing workflow
- `node`: Node ID currently executing

**Progress Percentage:** `(value / max) * 100`

#### Binary Preview Image (Type 1)

The most common preview format, using JPEG or PNG encoding.

**Binary Structure:**
```
[4 bytes: Event Type = 1]
[4 bytes: Image Format (1=JPEG, 2=PNG)]
[Image bytes in specified format]
```

**Example Decoding (Node.js):**
```javascript
import { readFile, writeFile } from 'node:fs/promises';

function decodePreviewImage(buffer) {
  const eventType = buffer.readUInt32BE(0);
  if (eventType !== 1) throw new Error(`Unexpected event type: ${eventType}`);

  const formatCode = buffer.readUInt32BE(4);
  const mimeType = formatCode === 1 ? 'image/jpeg' : 'image/png';
  const imageBytes = buffer.subarray(8);

  return { mimeType, imageBytes };
}

async function savePreviewExample(pathToBinary) {
  const binary = await readFile(pathToBinary);
  const { mimeType, imageBytes } = decodePreviewImage(binary);
  const extension = mimeType === 'image/jpeg' ? 'jpg' : 'png';
  await writeFile(`preview.${extension}`, imageBytes);
}

// usage: await savePreviewExample('preview.bin');
```

#### Binary Preview with Metadata (Type 4)

Enhanced preview that includes contextual metadata along with the image.

**Binary Structure:**
```
[4 bytes: Event Type = 4]
[4 bytes: Metadata JSON Length]
[Metadata JSON bytes (UTF-8)]
[Image bytes (PNG or JPEG)]
```

**Metadata Example:**
```json
{
  "image_type": "image/png",
  "prompt_id": "550e8400-e29b-41d4-a716-446655440000",
  "node_id": "3",
  "step": 15,
  "total_steps": 20,
  "seed": 42
}
```

**Example Decoding (Node.js):**
```javascript
import { readFile } from 'node:fs/promises';

function decodePreviewWithMetadata(buffer) {
  const eventType = buffer.readUInt32BE(0);
  if (eventType !== 4) throw new Error(`Unexpected event type: ${eventType}`);

  const metadataLength = buffer.readUInt32BE(4);
  const metadataStart = 8;
  const metadataEnd = metadataStart + metadataLength;
  const metadataJson = buffer.subarray(metadataStart, metadataEnd).toString('utf-8');
  const metadata = JSON.parse(metadataJson);
  const imageBytes = buffer.subarray(metadataEnd);

  return { metadata, imageBytes };
}

async function decodeFromFile(pathToBinary) {
  const binary = await readFile(pathToBinary);
  const { metadata, imageBytes } = decodePreviewWithMetadata(binary);
  console.log('Preview metadata:', metadata);
  return imageBytes;
}

// usage: await decodeFromFile('preview_with_metadata.bin');
```

### Implementing Preview Support

#### JavaScript/Browser Example

```javascript
// Connect to WebSocket
const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${clientId}`);

ws.onmessage = async (event) => {
  if (event.data instanceof Blob) {
    // Binary message (preview image)
    const arrayBuffer = await event.data.arrayBuffer();
    const dataView = new DataView(arrayBuffer);
    
    // Read event type
    const eventType = dataView.getUint32(0);
    
    if (eventType === 1) {
      // PREVIEW_IMAGE
      const imageFormat = dataView.getUint32(4);
      const imageBlob = new Blob([arrayBuffer.slice(8)], {
        type: imageFormat === 1 ? 'image/jpeg' : 'image/png'
      });
      
      // Display preview
      const imageUrl = URL.createObjectURL(imageBlob);
      document.getElementById('preview').src = imageUrl;
      
    } else if (eventType === 4) {
      // PREVIEW_IMAGE_WITH_METADATA
      const metadataLength = dataView.getUint32(4);
      const metadataBytes = new Uint8Array(arrayBuffer, 8, metadataLength);
      const metadata = JSON.parse(new TextDecoder().decode(metadataBytes));
      
      const imageBlob = new Blob([arrayBuffer.slice(8 + metadataLength)], {
        type: metadata.image_type || 'image/png'
      });
      
      const imageUrl = URL.createObjectURL(imageBlob);
      document.getElementById('preview').src = imageUrl;
      console.log('Preview metadata:', metadata);
    }
    
  } else {
    // JSON message
    const msg = JSON.parse(event.data);
    
    if (msg.type === 'progress') {
      const percent = (msg.data.value / msg.data.max) * 100;
      document.getElementById('progress-bar').style.width = `${percent}%`;
      document.getElementById('progress-text').textContent = 
        `Step ${msg.data.value}/${msg.data.max}`;
    }
  }
};
```

#### Node.js Example with WebSocket Client

```javascript
import WebSocket from 'ws';
import { writeFile } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';

const clientId = randomUUID();
const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${clientId}`);

ws.on('open', () => console.log('Connected to ComfyUI'));
ws.on('close', () => console.log('Connection closed'));
ws.on('error', (err) => console.error('WebSocket error', err));

ws.on('message', async (data) => {
  if (Buffer.isBuffer(data)) {
    const eventType = data.readUInt32BE(0);

    if (eventType === 1) {
      const formatCode = data.readUInt32BE(4);
      const extension = formatCode === 1 ? 'jpg' : 'png';
      const imageBytes = data.subarray(8);
      await writeFile(`preview_${Date.now()}.${extension}`, imageBytes);
      console.log('Saved preview image');

    } else if (eventType === 4) {
      const metadataLength = data.readUInt32BE(4);
      const metadataStart = 8;
      const metadataEnd = metadataStart + metadataLength;
      const metadataJson = data.subarray(metadataStart, metadataEnd).toString('utf-8');
      const metadata = JSON.parse(metadataJson);
      const imageBytes = data.subarray(metadataEnd);
      await writeFile(`preview_step_${metadata.step ?? 'unknown'}.png`, imageBytes);
      console.log('Preview with metadata:', metadata);
    }

  } else {
    const msg = JSON.parse(data.toString());
    if (msg.type === 'progress') {
      const { value, max } = msg.data;
      const percent = ((value / max) * 100).toFixed(1);
      console.log(`Progress: ${percent}% (${value}/${max})`);
    } else {
      console.log('Event:', msg.type);
    }
  }
});
```

### Preview Configuration

Preview behavior can be influenced by workflow configuration and node settings:

**Preview Quality:**
- JPEG previews: Faster transmission, smaller size, quality=95
- PNG previews: Lossless quality, larger size, slower

**Preview Size:**
- Previews are typically downscaled to reduce bandwidth
- Maximum size can be configured per-node
- Common sizes: 256px, 512px, 768px

**Preview Frequency:**
- Depends on the node implementation
- Samplers typically preview every N steps
- Some nodes may not support previews at all

### Best Practices

1. **Handle Both Formats**: Support both Type 1 and Type 4 preview messages for maximum compatibility

2. **Memory Management**: Dispose of old preview images to prevent memory leaks
   ```javascript
   // Revoke old object URL before creating new one
   if (oldImageUrl) URL.revokeObjectURL(oldImageUrl);
   ```

3. **Progress Calculation**: Always calculate progress as `(value/max)*100` to handle variable step counts

4. **Error Handling**: Wrap preview decoding in try-catch blocks as binary formats can vary

5. **Client ID Consistency**: Use the same client ID for WebSocket and prompt submission to receive previews

6. **Network Efficiency**: Binary previews are already optimized; avoid re-encoding

7. **User Experience**: Show progress bar alongside preview for better feedback

### Nodes That Support Previews

Common nodes that generate preview images:
- **KSampler**: Shows diffusion progress during sampling
- **KSamplerAdvanced**: Advanced sampling with previews
- **SamplerCustom**: Custom samplers with preview support
- **Image processing nodes**: Some image nodes show intermediate results

**Note:** Preview support depends on node implementation. Not all nodes generate previews, and preview frequency varies by node type and configuration.
