# ComfyUI API Documentation

## Table of Contents

1. [Introduction](#introduction)
2. [Base URL](#base-url)
3. [Authentication](#authentication)
4. [WebSocket Connection](#websocket-connection)
5. [Previews and Progress Tracking](#previews-and-progress-tracking)
6. [Core API Endpoints](#core-api-endpoints)
   - [Workflow Execution](#workflow-execution)
   - [Queue Management](#queue-management)
   - [Node Information](#node-information)
   - [History](#history)
7. [Resource Management](#resource-management)
   - [Models](#models)
   - [Embeddings](#embeddings)
   - [Images](#images)
8. [User Management](#user-management)
9. [Settings](#settings)
10. [System Information](#system-information)
11. [Internal Routes](#internal-routes)
12. [Error Handling](#error-handling)
13. [Examples](#examples)

---

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

---

## Core API Endpoints

This section covers the primary endpoints for interacting with ComfyUI. These endpoints allow you to execute workflows, manage the execution queue, query available nodes, and access execution history. Understanding these core endpoints is essential for building applications that leverage ComfyUI's powerful workflow execution capabilities.

### Frontend

#### Serve Frontend Application

**Endpoint:** `GET /`

**Response:** HTML file (index.html)

**Cache Headers:**
- `Cache-Control: no-cache`
- `Pragma: no-cache`
- `Expires: 0`

**Note:** This endpoint serves the main ComfyUI web interface.

---

### Workflow Execution

Workflow execution is the heart of ComfyUI's functionality. Workflows are defined as directed graphs of nodes, where each node represents an operation (like loading a model, encoding text, or generating an image). When you submit a workflow, it's added to an execution queue and processed node by node. The system automatically handles dependencies, caching, and parallel execution where possible.

#### Queue Prompt

Execute a workflow by adding it to the queue.

**Endpoint:** `POST /prompt`

**Request Body:**
```json
{
  "prompt": {
    "1": {
      "inputs": { /* node inputs */ },
      "class_type": "NodeClassName"
    },
    "2": { /* ... */ }
  },
  "client_id": "unique-client-id",
  "extra_data": {
    "extra_pnginfo": { /* workflow metadata */ }
  },
  "front": false,
  "number": 1,
  "prompt_id": "optional-custom-prompt-id",
  "partial_execution_targets": ["node_id1", "node_id2"]
}
```

**Parameters:**
- `prompt` (required): Object containing workflow nodes with their inputs
- `client_id` (optional): Client identifier for tracking execution
- `extra_data` (optional): Additional metadata to store with execution
- `front` (optional): If true, add to front of queue
- `number` (optional): Custom queue number
- `prompt_id` (optional): Custom prompt ID (UUID generated if not provided)
- `partial_execution_targets` (optional): Array of node IDs to execute (partial execution)

**Response (Success - 200):**
```json
{
  "prompt_id": "550e8400-e29b-41d4-a716-446655440000",
  "number": 1,
  "node_errors": {}
}
```

**Note:** Sensitive extra data keys (`auth_token_comfy_org`, `api_key_comfy_org`) are automatically removed from the queue and stored separately for security.

**Response (Error - 400):**
```json
{
  "error": {
    "type": "prompt_error",
    "message": "Invalid node configuration",
    "details": "...",
    "extra_info": {}
  },
  "node_errors": {
    "node_id": {
      "errors": [
        {
          "type": "error_type",
          "message": "error message"
        }
      ],
      "dependent_outputs": ["other_node_id"]
    }
  }
}
```

#### Get Current Prompt Status

**Endpoint:** `GET /prompt`

**Response:**
```json
{
  "exec_info": {
    "queue_remaining": 5
  }
}
```

---

### Queue Management

The queue system manages all workflow executions in ComfyUI. It maintains two separate queues: one for currently running workflows and one for pending workflows waiting to execute. Understanding queue management is crucial for building responsive applications, as it allows you to monitor execution status, cancel pending jobs, clear the queue, or interrupt running executions. The queue processes items in order (FIFO by default), but you can prioritize specific items by adding them to the front.

#### Get Queue Status

**Endpoint:** `GET /queue`

**Response:**
```json
{
  "queue_running": [
    [1, "prompt_id_1", "workflow_data", { "client_id": "..." }, ["output_nodes"]],
    ...
  ],
  "queue_pending": [
    [2, "prompt_id_2", "workflow_data", { "client_id": "..." }, ["output_nodes"]],
    ...
  ]
}
```

**Note:** Queue items are returned with only the first 5 elements (number, prompt_id, prompt, extra_data, outputs_to_execute). Sensitive data is removed for security.

#### Manage Queue

**Endpoint:** `POST /queue`

**Clear Queue:**
```json
{
  "clear": true
}
```

**Delete Specific Items:**
```json
{
  "delete": ["prompt_id_1", "prompt_id_2"]
}
```

**Response:** `200 OK`

#### Interrupt Execution

**Endpoint:** `POST /interrupt`

**Request Body (optional):**
```json
{
  "prompt_id": "specific-prompt-id-to-interrupt"
}
```

**Response:** `200 OK`

**Note:** 
- If `prompt_id` is provided, only interrupts that specific prompt if it's currently running.
- If no `prompt_id` is provided, performs a global interrupt of the currently executing prompt.
- If the specified `prompt_id` is not currently running, no interrupt occurs (logged but no error).

#### Free Memory

**Endpoint:** `POST /free`

**Request Body:**
```json
{
  "unload_models": true,
  "free_memory": true
}
```

**Parameters:**
- `unload_models` (optional): Unload all models from memory (default: false)
- `free_memory` (optional): Free up system memory (default: false)

**Response:** `200 OK`

**Note:** This endpoint sets flags that are processed by the execution queue. The actual memory freeing happens asynchronously during queue processing.

---

### Node Information

Nodes are the building blocks of ComfyUI workflows. Each node represents a specific operation with defined inputs and outputs. The node information endpoints provide comprehensive metadata about all available nodes, including their input parameters, output types, categories, and documentation. This information is essential for building dynamic workflow editors, validating workflows before execution, or creating node selection interfaces. Node information is generated from the actual Python classes, ensuring it's always accurate and up-to-date.

#### Get All Node Information

**Endpoint:** `GET /object_info`

**Response:**
```json
{
  "NodeClassName": {
    "input": {
      "required": {
        "param_name": ["TYPE", { "default": value }]
      },
      "optional": {
        "param_name": ["TYPE"]
      }
    },
    "input_order": {
      "required": ["param1", "param2"],
      "optional": ["param3"]
    },
    "output": ["OUTPUT_TYPE1", "OUTPUT_TYPE2"],
    "output_is_list": [false, false],
    "output_name": ["Output 1", "Output 2"],
    "output_tooltips": ["Description of output 1", "Description of output 2"],
    "name": "NodeClassName",
    "display_name": "Human Readable Name",
    "description": "Node description",
    "category": "category/subcategory",
    "output_node": false,
    "python_module": "nodes",
    "deprecated": false,
    "experimental": false,
    "api_node": false
  }
}
```

**Field Descriptions:**
- `input`: Required and optional input parameters with their types and defaults
- `input_order`: Order of inputs for UI rendering
- `output`: Return types for each output
- `output_is_list`: Whether each output is a list (array) of values
- `output_name`: Human-readable names for outputs
- `output_tooltips`: Descriptions for each output (if provided)
- `name`: Class name of the node
- `display_name`: Display name shown in UI
- `description`: Node description
- `category`: Category path (e.g., "image/transform")
- `output_node`: Whether this is an output/save node
- `python_module`: Python module containing the node
- `deprecated`: Whether the node is deprecated
- `experimental`: Whether the node is experimental
- `api_node`: Whether this is an API-specific node

#### Get Specific Node Information

**Endpoint:** `GET /object_info/{node_class}`

**Parameters:**
- `node_class`: The class name of the node

**Response:** Same format as above, but only for the specified node.

---

### History

The history system maintains a record of all completed workflow executions, including their results, outputs, and status. This is invaluable for tracking past generations, retrieving previously created images, debugging failed executions, or implementing undo/redo functionality. History entries include the complete workflow definition, all output artifacts (like generated images), execution status, and any error messages. You can query history by prompt ID, retrieve recent items with pagination, or clear old entries to manage storage.

#### Get Execution History

**Endpoint:** `GET /history`

**Query Parameters:**
- `max_items` (optional): Maximum number of items to return
- `offset` (optional): Offset for pagination (default: -1 which means no offset)

**Response:**
```json
{
  "prompt_id_1": {
    "prompt": [ /* queue number */, /* workflow */ ],
    "outputs": {
      "node_id": {
        "images": [
          {
            "filename": "image.png",
            "subfolder": "",
            "type": "output"
          }
        ]
      }
    },
    "status": {
      "status_str": "success",
      "completed": true,
      "messages": []
    }
  }
}
```

**Note:** The response is a dictionary where keys are prompt IDs and values contain execution details.

#### Get Specific Prompt History

**Endpoint:** `GET /history/{prompt_id}`

**Parameters:**
- `prompt_id`: The prompt ID to retrieve

**Response:** Same format as above, filtered to the specified prompt.

#### Manage History

**Endpoint:** `POST /history`

**Clear All History:**
```json
{
  "clear": true
}
```

**Delete Specific Items:**
```json
{
  "delete": ["prompt_id_1", "prompt_id_2"]
}
```

**Response:** `200 OK`

---

## Resource Management

Resource management endpoints provide access to ComfyUI's file-based resources including models, embeddings, and images. These endpoints allow you to discover available resources, upload new files, retrieve metadata, and access generated outputs. Understanding resource management is essential for building UIs that help users select models, manage their asset library, or display generated content.

### Models

Models are the AI components that power ComfyUI's generation capabilities. ComfyUI supports various model types including checkpoints, VAEs, LoRAs, controlnets, upscale models, and more. Models are organized into folders by type, and can be located in multiple directories. The model endpoints help you discover what models are available, view their metadata, and even preview models that support it (like safetensors files with embedded previews).

#### List Model Types

**Endpoint:** `GET /models`

**Response:**
```json
[
  "checkpoints",
  "vae",
  "loras",
  "controlnet",
  "clip",
  "upscale_models",
  "embeddings",
  ...
]
```

#### List Models in Folder

**Endpoint:** `GET /models/{folder}`

**Parameters:**
- `folder`: Model folder type (e.g., "checkpoints", "loras")

**Response:**
```json
[
  "model1.safetensors",
  "model2.ckpt",
  "subfolder/model3.safetensors"
]
```

#### Get Model Metadata (Safetensors)

**Endpoint:** `GET /view_metadata/{folder_name}`

**Query Parameters:**
- `filename`: Name of the safetensors file

**Response:**
```json
{
  "modelspec.architecture": "stable-diffusion-xl-v1-base",
  "modelspec.title": "Model Name",
  "modelspec.description": "Model description",
  ...
}
```

**Note:** Only works with `.safetensors` files.

#### Experimental: Get Model Folders with Paths

**Endpoint:** `GET /experiment/models`

**Response:**
```json
[
  {
    "name": "checkpoints",
    "folders": ["/path/to/models/checkpoints", "/another/path"]
  },
  ...
]
```

#### Experimental: Get Model Files with Details

**Endpoint:** `GET /experiment/models/{folder}`

**Response:**
```json
[
  {
    "name": "model.safetensors",
    "path": "subfolder/model.safetensors",
    "folder_index": 0,
    "size": 2147483648,
    "modified": 1701234567.89
  }
]
```

#### Experimental: Get Model Preview

**Endpoint:** `GET /experiment/models/preview/{folder}/{path_index}/{filename}`

**Parameters:**
- `folder`: Model folder type
- `path_index`: Index of the folder path
- `filename`: Model filename (can include subfolders)

**Response:** Image file (WEBP format)

---

### Embeddings

Embeddings (also known as textual inversions) are learned representations that can be used in prompts to achieve specific styles or subjects. They're typically small files that modify how the model interprets certain tokens. The embeddings endpoint lists all available embeddings in your ComfyUI installation, allowing users to discover and use them in their text prompts.

#### List Embeddings

**Endpoint:** `GET /embeddings`

**Response:**
```json
[
  "embedding1",
  "embedding2",
  "subfolder/embedding3"
]
```

**Note:** Returns filenames without extensions.

---

### Images

Image management is crucial for workflows that require input images (like img2img, inpainting, or controlnet workflows) or for retrieving generated outputs. ComfyUI maintains separate directories for different image types: input (user uploads), output (generated results), and temp (intermediate/preview images). The image endpoints support uploading, viewing with on-the-fly format conversion, and managing masks for inpainting workflows.

#### Upload Image

**Endpoint:** `POST /upload/image`

**Content-Type:** `multipart/form-data`

**Form Parameters:**
- `image`: Image file (required)
- `subfolder` (optional): Subfolder within the upload directory
- `type` (optional): Directory type ("input", "temp", "output") - default is "input"
- `overwrite` (optional): "true" or "1" to overwrite existing files

**Response:**
```json
{
  "name": "image.png",
  "subfolder": "subfolder",
  "type": "input"
}
```

**Notes:** 
- If file exists and overwrite is false, filename will be incremented (e.g., "image (1).png")
- Duplicate images (same content hash) are not saved again - the existing filename is returned
- Supported formats include PNG, JPEG, WEBP, and other common image formats

#### Upload Mask

**Endpoint:** `POST /upload/mask`

**Content-Type:** `multipart/form-data`

**Form Parameters:**
- `image`: Mask image file (required)
- `original_ref`: JSON string with reference to original image
  ```json
  {
    "filename": "original.png",
    "type": "output",
    "subfolder": ""
  }
  ```
- `subfolder` (optional): Subfolder within the upload directory
- `type` (optional): Directory type ("input", "temp", "output")

**Response:** Same as upload image

**Note:** This endpoint applies the uploaded mask as an alpha channel to the referenced original image, creating a composite image.

#### View Image

**Endpoint:** `GET /view`

**Query Parameters:**
- `filename`: Image filename (required, can include annotation like `image.png [output]`)
- `type` (optional): Directory type ("output", "input", "temp") - default is "output"
- `subfolder` (optional): Subfolder path within the directory
- `preview` (optional): Format for preview with optional quality (e.g., "webp", "jpeg", "webp;90", "jpeg;80")
  - Supported formats: "webp", "jpeg"
  - Quality range: 1-100 (default: 90)
- `channel` (optional): Channel to extract from image
  - "rgb" - RGB channels only
  - "a" - Alpha channel only (as grayscale)
  - "rgba" - All channels including alpha

**Response:** Image file (binary data)

**Examples:**
- `/view?filename=image.png&type=output`
- `/view?filename=image.png&preview=webp;80`
- `/view?filename=image.png&channel=a` (alpha channel only)
- `/view?filename=subfolder/image.png&subfolder=myfolder&type=input`

**Note:** The preview parameter allows for on-the-fly image conversion and compression without modifying the original file.

---

### Extensions

Extensions are JavaScript modules that enhance ComfyUI's frontend functionality. They can add new UI components, modify node behavior, integrate with external services, or provide custom visualizations. Custom nodes often include their own web extensions to provide specialized interfaces. The extensions endpoint lists all available JavaScript files that will be loaded by the frontend.

**Endpoint:** `GET /extensions`

**Response:**
```json
[
  "/extensions/extension1/script.js",
  "/extensions/extension2/module.js",
  "/extensions/custom_nodes.node_name/script.js"
]
```

**Note:** Returns JavaScript files from both the core extensions directory and custom node web extensions. Paths are relative to the web root and URL-encoded for custom node paths.

---

## User Management

User management enables multi-user ComfyUI installations where different users can have isolated workflows, settings, and data. When running in multi-user mode (enabled with the `--multi-user` flag), each user gets their own namespace for settings, saved workflows, and user data files. This is particularly useful for shared installations, team environments, or when hosting ComfyUI as a service. In single-user mode (default), user management endpoints still exist but operate on a single default user.

**Important:** User management features require the `--multi-user` command-line flag to be fully functional.

### List Users

**Endpoint:** `GET /users`

**Response (Multi-user mode):**
```json
{
  "storage": "server",
  "users": {
    "user_id_1": "Username 1",
    "user_id_2": "Username 2"
  }
}
```

**Response (Single-user mode):**
```json
{
  "storage": "server",
  "migrated": true
}
```

### Create User

**Endpoint:** `POST /users`

**Request Body:**
```json
{
  "username": "New User"
}
```

**Response (Success):**
```json
"new_user_id_uuid"
```

**Response (Error - 400):**
```json
{
  "error": "Duplicate username."
}
```

**Note:** Only available in multi-user mode.

### User Data Management

User data provides persistent storage for each user's custom files, such as saved workflows, presets, configurations, or any other JSON/binary data your application needs to store. This is separate from the global model/image directories and is scoped per user in multi-user mode. The user data API provides a simple file-system-like interface with support for directories, file metadata, moving files, and both text and binary content.

**Use Cases:**
- Saving and loading custom workflows
- Storing user preferences or presets
- Managing project files
- Caching user-specific data

#### List User Files

**Endpoint:** `GET /userdata`

**Query Parameters:**
- `dir` (optional): Subdirectory to list (default: root)
- `recurse` (optional): "true" to recurse subdirectories
- `full_info` (optional): "true" to include file metadata

**Response (basic):**
```json
[
  "file1.json",
  "folder1/"
]
```

**Response (with full_info):**
```json
[
  {
    "path": "file1.json",
    "size": 1024,
    "modified": 1701234567.89,
    "created": 1701234560.00
  }
]
```

#### V2: List User Files (Enhanced)

**Endpoint:** `GET /v2/userdata`

**Query Parameters:**
- `dir` (optional): Subdirectory to list
- `recurse` (optional): "true" to recurse
- `split` (optional): "true" to split folders and files
- `sort_by` (optional): "name", "modified", "created", "size", "type"
- `sort_order` (optional): "asc" or "desc" (default: "asc")

**Response (with split):**
```json
{
  "folders": [
    {
      "path": "folder1",
      "size": 0,
      "modified": 1701234567.89,
      "created": 1701234560.00
    }
  ],
  "files": [
    {
      "path": "file1.json",
      "size": 1024,
      "modified": 1701234567.89,
      "created": 1701234560.00
    }
  ]
}
```

#### Get User File

**Endpoint:** `GET /userdata/{file}`

**Parameters:**
- `file`: File path (URL-encoded if contains special characters)

**Response:** File content

#### Save User File

**Endpoint:** `POST /userdata/{file}`

**Content-Type:** `application/json` or `multipart/form-data`

**JSON Request:**
```json
{
  "any": "json data"
}
```

**Multipart Request:**
- Form field `file`: File to upload
- `overwrite` (optional): "true" to overwrite

**Response:**
```json
{
  "status": "success"
}
```

#### Delete User File

**Endpoint:** `DELETE /userdata/{file}`

**Parameters:**
- `file`: File path to delete

**Response:** `204 No Content`

#### Move User File

**Endpoint:** `POST /userdata/{file}/move/{dest}`

**Parameters:**
- `file`: Source file path
- `dest`: Destination file path

**Response:**
```json
{
  "status": "success"
}
```

---

## Settings

Settings control ComfyUI's behavior and appearance. They're stored per-user (in multi-user mode) or globally (in single-user mode). Settings can include UI preferences, default values, feature toggles, and custom configurations added by extensions or custom nodes. The settings system is schema-based, meaning each setting has a defined type and validation rules. Changes to settings are persisted across sessions.

**Common Setting Types:**
- Boolean flags (enable/disable features)
- String values (paths, IDs, text)
- Number values (timeouts, limits, scales)
- Complex objects (configurations, presets)

### Get All Settings

**Endpoint:** `GET /settings`

**Response:**
```json
{
  "setting_id_1": { /* setting value */ },
  "setting_id_2": { /* setting value */ }
}
```

### Get Specific Setting

**Endpoint:** `GET /settings/{id}`

**Parameters:**
- `id`: Setting identifier

**Response:**
```json
{
  "value": "setting value"
}
```

### Save All Settings

**Endpoint:** `POST /settings`

**Request Body:**
```json
{
  "setting_id_1": { /* setting value */ },
  "setting_id_2": { /* setting value */ }
}
```

**Response:** `200 OK`

### Save Specific Setting

**Endpoint:** `POST /settings/{id}`

**Parameters:**
- `id`: Setting identifier

**Request Body:**
```json
{
  "value": "new setting value"
}
```

**Response:** `200 OK`

---

## System Information

System information endpoints provide visibility into ComfyUI's runtime environment, available resources, and capabilities. This information is essential for monitoring system health, debugging performance issues, understanding version compatibility, and making intelligent decisions about resource-intensive operations. The system stats endpoint is particularly useful for checking available VRAM before queuing heavy workflows or displaying system status in monitoring dashboards.

### Get System Stats

**Endpoint:** `GET /system_stats`

**Response:**
```json
{
  "system": {
    "os": "linux",
    "ram_total": 34359738368,
    "ram_free": 17179869184,
    "comfyui_version": "0.3.76",
    "required_frontend_version": "1.0.0",
    "installed_templates_version": "1.0.0",
    "required_templates_version": "1.0.0",
    "python_version": "3.11.5 (main, Aug 24 2023, 15:09:45) [GCC 11.3.0]",
    "pytorch_version": "2.1.0+cu121",
    "embedded_python": false,
    "argv": ["main.py", "--listen"]
  },
  "devices": [
    {
      "name": "NVIDIA GeForce RTX 4090",
      "type": "cuda",
      "index": 0,
      "vram_total": 25769803776,
      "vram_free": 23622320128,
      "torch_vram_total": 25769803776,
      "torch_vram_free": 23622320128
    }
  ]
}
```

**Notes:**
- RAM and VRAM values are in bytes
- `pytorch_version` includes CUDA version if applicable (e.g., "+cu121" for CUDA 12.1)
- `embedded_python` indicates if using a bundled Python environment
- Multiple devices may be listed if available (e.g., multiple GPUs)

### Get Feature Flags

**Endpoint:** `GET /features`

**Response:**
```json
{
  "feature_name": true,
  "another_feature": false
}
```

---

## Subgraphs/Templates

Subgraphs (also called templates) are reusable workflow components that encapsulate common patterns or complex node arrangements. They allow you to package a group of nodes as a single reusable unit, similar to functions in programming. Global subgraphs are available system-wide and can be provided by custom nodes or the core system. Workflow templates are pre-built complete workflows that users can load as starting points. These features promote workflow reusability and help users get started quickly.

**Benefits:**
- Reduce complexity by hiding implementation details
- Share common patterns across workflows
- Provide starting points for new users
- Enable modular workflow design

### List Global Subgraphs

**Endpoint:** `GET /global_subgraphs`

**Response:**
```json
[
  {
    "id": "subgraph_id",
    "name": "Subgraph Name",
    "module": "custom_nodes.module_name"
  }
]
```

### Get Subgraph by ID

**Endpoint:** `GET /global_subgraphs/{id}`

**Parameters:**
- `id`: Subgraph identifier

**Response:**
```json
{
  "id": "subgraph_id",
  "name": "Subgraph Name",
  "data": { /* subgraph data */ }
}
```

### List Workflow Templates

**Endpoint:** `GET /workflow_templates`

**Response:**
```json
{
  "custom_node_name": [
    {
      "name": "Template 1",
      "path": "/path/to/template1.json"
    }
  ]
}
```

### Get Internationalization Data

**Endpoint:** `GET /i18n`

**Query Parameters:**
- `language` (optional): Language code (default: "en")

**Response:**
```json
{
  "nodeDefs": {
    "NodeClassName": {
      "name": "Translated Name",
      "description": "Translated description",
      "inputs": {
        "input_name": "Translated input label"
      },
      "outputs": {
        "output_name": "Translated output label"
      }
    }
  },
  "commands": { /* translated commands */ },
  "settings": { /* translated settings */ }
}
```

**Note:** Returns internationalization (i18n) data for the ComfyUI frontend, including node definitions, commands, and settings translations. Custom nodes can provide their own i18n data.

---

## Internal Routes

**Base Path:** `/internal/`

Internal routes are designed specifically for the ComfyUI frontend and internal tooling. These endpoints may change without notice, have different stability guarantees, or expose implementation details not meant for external consumption. While they can be useful for debugging or building tightly integrated tools, production applications should prefer the stable public API endpoints whenever possible.

**⚠️ Warning:** These endpoints are for internal ComfyUI use only and should not be relied upon in external applications. They may change or be removed in future versions without following normal API versioning practices.

**When to Use Internal Routes:**
- Building ComfyUI frontend extensions
- Debugging and development
- Internal tooling and automation
- When explicitly directed by ComfyUI documentation

### Get Logs

**Endpoint:** `GET /internal/logs`

**Response:** Plain text log entries

### Get Raw Logs

**Endpoint:** `GET /internal/logs/raw`

**Response:**
```json
{
  "entries": [
    {
      "t": "2024-01-01 12:00:00",
      "m": "Log message"
    }
  ],
  "size": {
    "cols": 80,
    "rows": 24
  }
}
```

### Subscribe to Logs

**Endpoint:** `PATCH /internal/logs/subscribe`

**Request Body:**
```json
{
  "clientId": "client-uuid",
  "enabled": true
}
```

**Response:** `200 OK`

**Note:** When enabled, log messages will be pushed to the specified client via WebSocket. This is useful for real-time log monitoring in the frontend.

### Get Folder Paths

**Endpoint:** `GET /internal/folder_paths`

**Response:**
```json
{
  "checkpoints": ["/path/to/checkpoints"],
  "vae": ["/path/to/vae"],
  ...
}
```

### List Files in Directory

**Endpoint:** `GET /internal/files/{directory_type}`

**Parameters:**
- `directory_type`: "output", "input", or "temp"

**Response:**
```json
[
  "newest_file.png",
  "older_file.jpg",
  "oldest_file.png"
]
```

**Note:** Files are sorted by modification time (newest first). Returns only files, not subdirectories.

---

## Error Handling

Proper error handling is crucial for building robust ComfyUI integrations. ComfyUI uses standard HTTP status codes combined with detailed JSON error responses to help you understand and recover from failures. Errors can occur at multiple levels: HTTP transport errors, workflow validation errors, node execution errors, or resource access errors. The API provides structured error information that includes error types, human-readable messages, and contextual details to help with debugging and user feedback.

**Error Handling Strategy:**
1. Check HTTP status code first
2. Parse the error response JSON
3. Look for `node_errors` for validation issues
4. Display user-friendly messages based on error type
5. Log full error details for debugging

### HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 204 | No Content (successful deletion) |
| 400 | Bad Request (invalid parameters or validation error) |
| 403 | Forbidden (security violation) |
| 404 | Not Found (resource doesn't exist) |
| 500 | Internal Server Error |

### Error Response Format

```json
{
  "error": {
    "type": "error_type",
    "message": "Human readable error message",
    "details": "Detailed error information",
    "extra_info": {}
  },
  "node_errors": {
    "node_id": {
      "errors": [
        {
          "type": "required_input_missing",
          "message": "Input 'param' is required"
        }
      ],
      "dependent_outputs": ["node_2", "node_3"]
    }
  }
}
```

### Common Error Types

- `prompt_error`: Invalid workflow configuration
- `validation_error`: Node validation failed
- `required_input_missing`: Required input not provided
- `invalid_input_type`: Input type mismatch
- `value_not_in_list`: Input value not in allowed list
- `no_prompt`: No prompt provided in request
- `duplicate_username`: Username already exists (user management)
- `invalid_directory_type`: Invalid directory type specified

---

## Examples

These practical examples demonstrate common ComfyUI API usage patterns. Each example is self-contained and shows best practices for specific tasks. The examples use JavaScript (Node.js and browser) to cover both server-side and browser-based implementations.

**📚 Detailed Examples (Separate Files):**

For comprehensive, production-ready examples with detailed explanations:

1. **[Simple Workflow Execution](./examples/simple-workflow-execution.md)** - Complete guide to constructing and executing workflows
2. **[WebSocket Monitoring and Progress Tracking](./examples/websocket-monitoring.md)** - Real-time execution monitoring with Node.js and browser JavaScript examples
3. **[Image Upload and Image-to-Image Workflow](./examples/image-upload-workflow.md)** - Upload images and create img2img workflows
4. **[Download Generated Images](./examples/download-outputs.md)** - Retrieve and save generated outputs

**[📖 Browse All Examples →](./examples/README.md)**

---

### Quick Reference Examples

Below are quick snippets for common tasks. For complete, production-ready code, see the detailed examples above.

### Example 1: Execute a Simple Workflow

```javascript
// Run with Node 18+ for global fetch support (or import 'node-fetch').
const workflow = {
  "1": {
    "inputs": {
      "ckpt_name": "model.safetensors"
    },
    "class_type": "CheckpointLoaderSimple"
  },
  "2": {
    "inputs": {
      "text": "a beautiful landscape",
      "clip": ["1", 1]
    },
    "class_type": "CLIPTextEncode"
  }
};

async function queuePrompt() {
  const response = await fetch('http://127.0.0.1:8188/prompt', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      prompt: workflow,
      client_id: 'my-client-id'
    })
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const result = await response.json();
  console.log(`Prompt ID: ${result.prompt_id}`);
}

queuePrompt().catch(console.error);
```

**💡 See Also:** [Complete workflow execution example](./examples/simple-workflow-execution.md) with detailed node explanations

### Example 2: Monitor Execution via WebSocket

```javascript
// npm install ws (Node.js WebSocket client)
import WebSocket from 'ws';
import { randomUUID } from 'node:crypto';

const clientId = randomUUID();
const ws = new WebSocket(`ws://127.0.0.1:8188/ws?clientId=${clientId}`);

ws.on('open', () => console.log('WebSocket connected'));
ws.on('close', () => console.log('WebSocket closed'));
ws.on('error', (err) => console.error('WebSocket error', err));

ws.on('message', (payload) => {
  const data = JSON.parse(payload.toString());
  console.log(`Event: ${data.type}`);
  if (data.type === 'executing') {
    console.log(`Currently executing node: ${data.data.node}`);
  } else if (data.type === 'executed') {
    console.log('Node outputs:', data.data.output);
  }
});
```

**💡 See Also:** [Complete WebSocket monitoring example](./examples/websocket-monitoring.md) with preview image handling

### Example 3: Upload and Use an Image

```javascript
// Node 18+ exposes FormData and Blob globally.
import { readFile } from 'node:fs/promises';

async function uploadAndUseImage() {
  const formData = new FormData();
  const fileBuffer = await readFile('input.png');
  formData.append('image', new Blob([fileBuffer], { type: 'image/png' }), 'input.png');
  formData.append('type', 'input');
  formData.append('subfolder', '');

  const response = await fetch('http://127.0.0.1:8188/upload/image', {
    method: 'POST',
    body: formData
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const uploadResult = await response.json();
  console.log(`Uploaded: ${uploadResult.name}`);

  const workflow = {
    "1": {
      "inputs": {
        "image": uploadResult.name
      },
      "class_type": "LoadImage"
    }
  };

  return workflow;
}

uploadAndUseImage().catch(console.error);
```

**💡 See Also:** [Complete image upload and img2img example](./examples/image-upload-workflow.md) with mask support

### Example 4: Check Queue Status

```javascript
async function checkQueue() {
  const response = await fetch('http://127.0.0.1:8188/queue');
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const queueData = await response.json();

  console.log(`Running: ${queueData.queue_running.length} items`);
  console.log(`Pending: ${queueData.queue_pending.length} items`);
}

checkQueue().catch(console.error);
```

### Example 5: Get Execution History

```javascript
async function loadHistory() {
  const response = await fetch('http://127.0.0.1:8188/history?max_items=10');
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const history = await response.json();

  Object.entries(history).forEach(([promptId, data]) => {
    console.log(`Prompt: ${promptId}`);
    console.log(`Status: ${data.status.status_str}`);

    if (data.outputs) {
      Object.values(data.outputs).forEach((output) => {
        output.images?.forEach((img) => {
          console.log(`  Image: ${img.filename}`);
        });
      });
    }
  });
}

loadHistory().catch(console.error);
```

**💡 See Also:** [Complete history and download example](./examples/download-outputs.md) with batch downloading

### Example 6: Download Generated Image

```javascript
import { writeFile } from 'node:fs/promises';

async function downloadImage(filename) {
  const query = new URLSearchParams({ filename, type: 'output' });
  const response = await fetch(`http://127.0.0.1:8188/view?${query.toString()}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);

  const imageBuffer = Buffer.from(await response.arrayBuffer());
  await writeFile('output.png', imageBuffer);
  console.log('Saved output.png');
}

downloadImage('ComfyUI_00001_.png').catch(console.error);
```

**💡 See Also:** [Complete history and download example](./examples/download-outputs.md) with batch downloading

---

### More Complete Examples

The quick examples above are meant for reference. For production-ready code with comprehensive error handling, detailed explanations, and advanced features, check out our detailed example files:

| Example | What You'll Learn |
|---------|-------------------|
| [Simple Workflow Execution](./examples/simple-workflow-execution.md) | Complete workflow construction, node connections, error handling, and workflow anatomy |
| [WebSocket Monitoring](./examples/websocket-monitoring.md) | Real-time monitoring with Node.js and browser JavaScript, binary preview handling, progress tracking |
| [Image Upload & img2img](./examples/image-upload-workflow.md) | Upload API, image-to-image workflows, VAE encoding, inpainting with masks, format conversion |
| [Download Outputs](./examples/download-outputs.md) | History queries, batch downloading, format conversion, alpha channel extraction, verification |

**[📖 View All Examples with Full Documentation →](./examples/README.md)**

---

## Best Practices

1. **Use WebSocket for Real-time Updates**: Connect via WebSocket to receive real-time execution updates instead of polling.

2. **Handle Errors Gracefully**: Always check for `node_errors` in responses and handle validation errors before execution.

3. **Clean Up Resources**: Use `/free` endpoint to unload models when switching between different workflows.

4. **Unique Client IDs**: Use unique client IDs (UUIDs) for each client to properly track execution state and receive targeted messages.

5. **Validate Workflows**: Use the validation that happens during `/prompt` POST to catch errors before execution.

6. **Multi-user Considerations**: When running in multi-user mode, always include the `comfy-user` header in requests.

7. **File Path Security**: Never use absolute paths or path traversal patterns (`..`) in file-related endpoints.

8. **Rate Limiting**: Be mindful of queue depth - check queue status before adding many prompts.

9. **Feature Flags**: Exchange feature flags with the server via WebSocket to enable/disable capabilities based on client support.

10. **Binary Messages**: Handle both JSON and binary WebSocket messages for optimal performance, especially for image previews.

11. **Compression**: Include `Accept-Encoding: gzip` header for compressed responses on slower connections.

---

## Changelog

For version history and updates, check the main repository or the `/system_stats` endpoint for current version information.

---

## Support

- **GitHub**: [https://github.com/comfyanonymous/ComfyUI](https://github.com/comfyanonymous/ComfyUI)
- **Discord**: [ComfyUI Discord](https://www.comfy.org/discord)
- **Website**: [https://www.comfy.org/](https://www.comfy.org/)

---

*Last Updated: December 7, 2025*
*ComfyUI Version: 0.3.76*
*For the latest updates, check the [ComfyUI GitHub repository](https://github.com/comfyanonymous/ComfyUI)*
