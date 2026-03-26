# Simple Workflow Execution

This example provides a deep dive into building and executing ComfyUI workflows programmatically. You will learn the anatomy of a workflow node, how to wire nodes together, and how to submit, poll, and validate a prompt.

> **New to the API?** Start with the [Minimal API Example](./minimal-api-example.md) first, then return here for a more detailed explanation.

---

## Workflow Anatomy

A ComfyUI workflow is a JSON object (called a **prompt**) where:
- Each **key** is a unique node ID (typically a numeric string like `"1"`, `"2"`, …)
- Each **value** is a node descriptor with two fields:
  - `class_type` — the node type (must match a name from `GET /object_info`)
  - `inputs` — named input values; other nodes are referenced as `["node_id", output_index]`

```json
{
  "1": {
    "class_type": "CheckpointLoaderSimple",
    "inputs": {
      "ckpt_name": "sd_xl_base_1.0.safetensors"
    }
  },
  "2": {
    "class_type": "CLIPTextEncode",
    "inputs": {
      "text": "a serene mountain landscape",
      "clip": ["1", 1]
    }
  }
}
```

### Node output references

Output references follow the format `["node_id", output_index]`:

| Node | Output index | Type |
|------|-------------|------|
| CheckpointLoaderSimple | 0 | MODEL |
| CheckpointLoaderSimple | 1 | CLIP |
| CheckpointLoaderSimple | 2 | VAE |
| CLIPTextEncode | 0 | CONDITIONING |
| KSampler | 0 | LATENT |
| VAEDecode | 0 | IMAGE |
| EmptyLatentImage | 0 | LATENT |

Use `GET /object_info` to discover the exact inputs and outputs for any node type.

---

## Discover Available Nodes

```javascript
// Fetch all available node types and their input/output signatures
const response = await fetch('http://127.0.0.1:8188/object_info');
const nodeInfo = await response.json();

// Find nodes by category
const samplers = Object.entries(nodeInfo)
  .filter(([, info]) => info.category === 'sampling')
  .map(([name]) => name);

console.log('Available samplers:', samplers);
```

---

## Complete Text-to-Image Example

```javascript
import { randomUUID } from 'node:crypto';

const CLIENT_ID = randomUUID();

const workflow = {
  '1': {
    class_type: 'CheckpointLoaderSimple',
    inputs: { ckpt_name: 'sd_xl_base_1.0.safetensors' },
  },
  '2': {
    class_type: 'CLIPTextEncode',
    inputs: {
      text: 'a serene mountain lake at sunrise, photorealistic, 8k',
      clip: ['1', 1],
    },
  },
  '3': {
    class_type: 'CLIPTextEncode',
    inputs: {
      text: 'blurry, low quality, watermark, text',
      clip: ['1', 1],
    },
  },
  '4': {
    class_type: 'EmptyLatentImage',
    inputs: { width: 1024, height: 1024, batch_size: 1 },
  },
  '5': {
    class_type: 'KSampler',
    inputs: {
      seed: Math.floor(Math.random() * 2 ** 32),
      steps: 30,
      cfg: 7.5,
      sampler_name: 'dpmpp_2m',
      scheduler: 'karras',
      denoise: 1.0,
      model:        ['1', 0],
      positive:     ['2', 0],
      negative:     ['3', 0],
      latent_image: ['4', 0],
    },
  },
  '6': {
    class_type: 'VAEDecode',
    inputs: { samples: ['5', 0], vae: ['1', 2] },
  },
  '7': {
    class_type: 'SaveImage',
    inputs: { filename_prefix: 'txt2img', images: ['6', 0] },
  },
};

const response = await fetch('http://127.0.0.1:8188/prompt', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ prompt: workflow, client_id: CLIENT_ID }),
});

if (!response.ok) {
  const err = await response.text();
  throw new Error(`Queue failed: ${err}`);
}

const result = await response.json();

// Check for node-level validation errors (still returns HTTP 200)
if (result.node_errors && Object.keys(result.node_errors).length > 0) {
  console.warn('Node errors:', result.node_errors);
}

console.log(`Queued: ${result.prompt_id}`);
```

---

## Polling for Completion

If you prefer HTTP polling over WebSocket monitoring, use the history endpoint:

```javascript
/**
 * Polls GET /history/{promptId} until the prompt is complete.
 * Note: WebSocket monitoring (see minimal-api-example.md) is preferred
 * over polling as it reduces server load and provides live previews.
 *
 * @param {string} promptId - ID returned by POST /prompt.
 * @param {number} intervalMs - How often to poll (milliseconds).
 * @returns {Promise<object>} History entry for the completed prompt.
 */
async function pollUntilComplete(promptId, intervalMs = 1000) {
  while (true) {
    const response = await fetch(`http://127.0.0.1:8188/history/${promptId}`);
    const history = await response.json();

    if (history[promptId]) {
      const entry = history[promptId];
      const status = entry.status?.status_str;

      if (status === 'success') return entry;
      if (status === 'error') throw new Error('Execution failed');
    }

    // Not done yet — wait before polling again
    await new Promise(resolve => setTimeout(resolve, intervalMs));
  }
}
```

---

## Error Handling

The `/prompt` endpoint returns HTTP 200 even when there are validation errors. Always check the response body:

```javascript
const result = await response.json();

// Validation errors from the server-side scheduler
if (result.error) {
  console.error('Prompt error:', result.error);
}

// Per-node errors (missing inputs, type mismatches, etc.)
if (result.node_errors) {
  for (const [nodeId, errors] of Object.entries(result.node_errors)) {
    console.error(`Node ${nodeId}:`, errors);
  }
}
```

---

## See Also

- [Minimal API Example](./minimal-api-example.md) — Complete runnable example
- [WebSocket Monitoring](./websocket-monitoring.md) — Real-time progress and previews
- [Core Endpoints Reference](../core_endpoints.md) — Full `/prompt` request schema
