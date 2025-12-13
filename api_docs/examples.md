# Examples

These practical examples demonstrate common ComfyUI API usage patterns. Each example is self-contained and shows best practices for specific tasks. The examples use JavaScript (Node.js and browser) to cover both server-side and browser-based implementations.

**📚 Detailed Examples (Separate Files):**

For comprehensive, production-ready examples with detailed explanations:

1. **[Simple Workflow Execution](./examples/simple-workflow-execution.md)** - Complete guide to constructing and executing workflows
2. **[WebSocket Monitoring and Progress Tracking](./examples/websocket-monitoring.md)** - Real-time execution monitoring with Node.js and browser JavaScript examples
3. **[Image Upload and Image-to-Image Workflow](./examples/image-upload-workflow.md)** - Upload images and create img2img workflows
4. **[Download Generated Images](./examples/download-outputs.md)** - Retrieve and save generated outputs

**[📖 Browse All Examples →](./examples/README.md)**

---

## Quick Reference Examples

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

## More Complete Examples

The quick examples above are meant for reference. For production-ready code with comprehensive error handling, detailed explanations, and advanced features, check out our detailed example files:

| Example | What You'll Learn |
|---------|-------------------|
| [Simple Workflow Execution](./examples/simple-workflow-execution.md) | Complete workflow construction, node connections, error handling, and workflow anatomy |
| [WebSocket Monitoring](./examples/websocket-monitoring.md) | Real-time monitoring with Node.js and browser JavaScript, binary preview handling, progress tracking |
| [Image Upload & img2img](./examples/image-upload-workflow.md) | Upload API, image-to-image workflows, VAE encoding, inpainting with masks, format conversion |
| [Download Outputs](./examples/download-outputs.md) | History queries, batch downloading, format conversion, alpha channel extraction, verification |

**[📖 View All Examples with Full Documentation →](./examples/README.md)**
