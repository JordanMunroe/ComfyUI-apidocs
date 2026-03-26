# Minimal API Example

This guide provides a complete, self-contained example for integrating with the ComfyUI API in both **JavaScript (Node.js)** and **C#**. It covers every step you need to get an image out of ComfyUI from code:

1. [Check server status](#1-check-server-status)
2. [Queue a workflow](#2-queue-a-workflow)
3. [Monitor progress and receive preview images via WebSocket](#3-websocket-monitoring)
4. [Download the final generated image](#4-download-the-final-image)

Both examples handle **single-user** and **multi-user** modes by toggling a single constant.

> **Full runnable files:**
> - JavaScript: [`javascript/minimal-example.js`](./javascript/minimal-example.js)
> - C#:          [`csharp/MinimalExample.cs`](./csharp/MinimalExample.cs)

---

## Authentication Modes

ComfyUI supports two modes:

| Mode | Server flag | What to do in code |
|------|-------------|--------------------|
| **Single-user** (default) | *(none)* | No special headers needed |
| **Multi-user** | `--multi-user` | Add `comfy-user: <user_id>` header to every request |

> There is no password system. For network-level access control, place ComfyUI behind a reverse proxy. See the [Authentication Guide](../authentication.md) for details.

### JavaScript

```javascript
// true when ComfyUI is started with --multi-user, false otherwise
const MULTI_USER = false;
const USER_ID    = 'alice'; // only used in multi-user mode

/**
 * Returns headers common to every HTTP request.
 * In multi-user mode the comfy-user header is added automatically.
 */
function buildHeaders(extra = {}) {
  const headers = { 'Content-Type': 'application/json', ...extra };
  if (MULTI_USER) {
    headers['comfy-user'] = USER_ID;
  }
  return headers;
}
```

### C\#

```csharp
const bool MultiUser = false;
const string UserId  = "alice"; // only used in multi-user mode

using var httpClient = new HttpClient();

// Apply the header globally so every request carries it automatically
if (MultiUser)
{
    httpClient.DefaultRequestHeaders.Add("comfy-user", UserId);
}
```

---

## 1. Check Server Status

Call `GET /system_stats` to verify the server is reachable and to retrieve the running ComfyUI version.

### JavaScript

```javascript
async function getServerStatus() {
  const response = await fetch(`${BASE_URL}/system_stats`, {
    headers: buildHeaders(),
  });

  if (!response.ok) {
    throw new Error(`GET /system_stats failed: HTTP ${response.status}`);
  }

  const stats = await response.json();
  const version = stats?.system?.comfyui_version ?? 'unknown';
  console.log(`Server online — ComfyUI ${version}`);
  return stats;
}
```

### C\#

```csharp
/// <summary>
/// Fetches system statistics from the ComfyUI server.
/// Use this to confirm the server is reachable before submitting work.
/// </summary>
static async Task GetServerStatusAsync(HttpClient client)
{
    var stats = await client.GetFromJsonAsync<JsonElement>($"{BaseUrl}/system_stats");
    string version = stats
        .GetProperty("system")
        .GetProperty("comfyui_version")
        .GetString() ?? "unknown";
    Console.WriteLine($"Server online — ComfyUI {version}");
}
```

---

## 2. Queue a Workflow

A **workflow** is a JSON object where each key is a node ID and each value describes a node (its type and inputs). Nodes wire to each other using `["node_id", output_index]` references.

The example below builds a minimal text-to-image pipeline:

```
CheckpointLoader → CLIPTextEncode (positive)
                 → CLIPTextEncode (negative)
EmptyLatentImage →
                   KSampler → VAEDecode → SaveImage
```

### Building the workflow

#### JavaScript

```javascript
/**
 * Builds a minimal text-to-image workflow graph.
 *
 * Node references: ["node_id", output_index]
 *   ["1", 0] = MODEL, ["1", 1] = CLIP, ["1", 2] = VAE
 */
function buildWorkflow(
  positivePrompt = 'a beautiful sunset over mountains, photorealistic',
  negativePrompt = 'blurry, low quality, watermark',
  checkpointName = 'sd_xl_base_1.0.safetensors',
) {
  return {
    '1': {
      class_type: 'CheckpointLoaderSimple',
      inputs: { ckpt_name: checkpointName },
    },
    '2': {
      class_type: 'CLIPTextEncode',
      inputs: { text: positivePrompt, clip: ['1', 1] },
    },
    '3': {
      class_type: 'CLIPTextEncode',
      inputs: { text: negativePrompt, clip: ['1', 1] },
    },
    '4': {
      class_type: 'EmptyLatentImage',
      inputs: { width: 512, height: 512, batch_size: 1 },
    },
    '5': {
      class_type: 'KSampler',
      inputs: {
        seed: Math.floor(Math.random() * 2 ** 32),
        steps: 20,
        cfg: 7.0,            // classifier-free guidance scale
        sampler_name: 'euler',
        scheduler: 'normal',
        denoise: 1.0,        // 1.0 = full denoising (text-to-image)
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
      inputs: { filename_prefix: 'minimal_example', images: ['6', 0] },
    },
  };
}
```

#### C\#

```csharp
/// <summary>
/// Builds a minimal text-to-image workflow graph.
/// Node references use the format ["node_id", output_index].
/// </summary>
static object BuildWorkflow(
    string positivePrompt = "a beautiful sunset over mountains, photorealistic",
    string negativePrompt = "blurry, low quality, watermark",
    string checkpointName = "sd_xl_base_1.0.safetensors")
{
    int seed = new Random().Next();

    return new Dictionary<string, object>
    {
        ["1"] = new { class_type = "CheckpointLoaderSimple",
                      inputs = new { ckpt_name = checkpointName } },
        ["2"] = new { class_type = "CLIPTextEncode",
                      inputs = new { text = positivePrompt, clip = new object[] { "1", 1 } } },
        ["3"] = new { class_type = "CLIPTextEncode",
                      inputs = new { text = negativePrompt, clip = new object[] { "1", 1 } } },
        ["4"] = new { class_type = "EmptyLatentImage",
                      inputs = new { width = 512, height = 512, batch_size = 1 } },
        ["5"] = new
        {
            class_type = "KSampler",
            inputs = new
            {
                seed, steps = 20, cfg = 7.0,
                sampler_name = "euler", scheduler = "normal", denoise = 1.0,
                model        = new object[] { "1", 0 },
                positive     = new object[] { "2", 0 },
                negative     = new object[] { "3", 0 },
                latent_image = new object[] { "4", 0 },
            }
        },
        ["6"] = new { class_type = "VAEDecode",
                      inputs = new { samples = new object[] { "5", 0 }, vae = new object[] { "1", 2 } } },
        ["7"] = new { class_type = "SaveImage",
                      inputs = new { filename_prefix = "minimal_example", images = new object[] { "6", 0 } } },
    };
}
```

### Submitting to the queue

The `client_id` field links the HTTP submission to the WebSocket session so that progress events and previews are routed back to this client.

#### JavaScript

```javascript
/**
 * Submits a workflow to the execution queue.
 * @returns {Promise<string>} The prompt_id assigned by the server.
 */
async function queueWorkflow(workflow) {
  const response = await fetch(`${BASE_URL}/prompt`, {
    method: 'POST',
    headers: buildHeaders(),
    body: JSON.stringify({
      prompt: workflow,
      client_id: CLIENT_ID, // must match the WebSocket clientId query parameter
    }),
  });

  if (!response.ok) {
    throw new Error(`POST /prompt failed: HTTP ${response.status}`);
  }

  const result = await response.json();
  console.log(`Queued — prompt_id: ${result.prompt_id}`);
  return result.prompt_id;
}
```

#### C\#

```csharp
/// <summary>
/// Submits a workflow to the ComfyUI execution queue.
/// </summary>
/// <param name="clientId">Must match the WebSocket clientId query parameter.</param>
/// <returns>The prompt_id assigned by the server.</returns>
static async Task<string> QueueWorkflowAsync(HttpClient client, object workflow, string clientId)
{
    var body = new { prompt = workflow, client_id = clientId };
    var response = await client.PostAsJsonAsync($"{BaseUrl}/prompt", body);
    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    string promptId = result.GetProperty("prompt_id").GetString()!;
    Console.WriteLine($"Queued — prompt_id: {promptId}");
    return promptId;
}
```

---

## 3. WebSocket Monitoring

Open a WebSocket connection **before** or immediately after queuing a workflow. Use the same `clientId` in both the WebSocket URL and the prompt body so the server routes events for your prompt to your socket.

```
ws://127.0.0.1:8188/ws?clientId=<your-client-id>
```

### Event types

| Type | Meaning |
|------|---------|
| `execution_start` | The server began processing your prompt |
| `executing` | A node started executing; `node` is `null` when all nodes are done |
| `progress` | Periodic step counter from long-running nodes (e.g. KSampler) |
| `executed` | A node finished and produced output (e.g. image filenames) |
| `execution_error` | Something went wrong; `exception_message` has details |
| `execution_cached` | A node's output was served from cache |
| *(binary)* | A preview image (see below) |

### Binary preview images

When a node (like KSampler) generates a preview image during sampling, it is sent as a **binary WebSocket message** instead of JSON.

Two binary event types exist:

**Type 1 — PREVIEW_IMAGE**
```
[4B: event type = 1] [4B: format (1=JPEG, 2=PNG)] [image bytes]
```

**Type 4 — PREVIEW_IMAGE_WITH_METADATA**
```
[4B: event type = 4] [4B: metadata length] [UTF-8 JSON metadata] [image bytes]
```

#### JavaScript

```javascript
/**
 * Decodes a binary WebSocket message into a preview image.
 * Returns null for unrecognised event types.
 */
function decodePreviewImage(buffer) {
  const eventType = buffer.readUInt32BE(0);

  if (eventType === 1) {
    const formatCode = buffer.readUInt32BE(4);
    return {
      extension: formatCode === 1 ? 'jpg' : 'png',
      imageBytes: buffer.subarray(8),
    };
  }

  if (eventType === 4) {
    const metaLen = buffer.readUInt32BE(4);
    const metaEnd = 8 + metaLen;
    const metadata = JSON.parse(buffer.subarray(8, metaEnd).toString('utf-8'));
    const mimeType = metadata.image_type ?? 'image/png';
    return {
      extension: mimeType === 'image/jpeg' ? 'jpg' : 'png',
      imageBytes: buffer.subarray(metaEnd),
      metadata,
    };
  }

  return null; // unknown event type
}
```

#### C\#

```csharp
/// <summary>
/// Decodes a binary WebSocket message into a preview image.
/// Returns null for unrecognised event types.
/// </summary>
static PreviewImage? DecodePreviewImage(byte[] buffer, int length)
{
    if (length < 8) return null;

    uint eventType = ReadUInt32BigEndian(buffer, 0);

    if (eventType == 1)
    {
        uint formatCode = ReadUInt32BigEndian(buffer, 4);
        return new PreviewImage(
            Extension:  formatCode == 1 ? "jpg" : "png",
            ImageBytes: buffer[8..length],
            Metadata:   null);
    }

    if (eventType == 4)
    {
        uint metaLen   = ReadUInt32BigEndian(buffer, 4);
        int  metaEnd   = 8 + (int)metaLen;
        var  metadata  = JsonDocument.Parse(
            Encoding.UTF8.GetString(buffer, 8, (int)metaLen)).RootElement;
        string mimeType = metadata.TryGetProperty("image_type", out var mt)
            ? mt.GetString() ?? "image/png" : "image/png";
        return new PreviewImage(
            Extension:  mimeType == "image/jpeg" ? "jpg" : "png",
            ImageBytes: buffer[metaEnd..length],
            Metadata:   metadata);
    }

    return null;
}

static uint ReadUInt32BigEndian(byte[] buf, int offset) =>
    ((uint)buf[offset] << 24) | ((uint)buf[offset+1] << 16) |
    ((uint)buf[offset+2] << 8) | buf[offset+3];

record PreviewImage(string Extension, byte[] ImageBytes, JsonElement? Metadata);
```

### Full monitoring loop

#### JavaScript

```javascript
/**
 * Waits for a queued prompt to finish, saving preview images along the way.
 * @returns {Promise<object>} Node output map from the `executed` event.
 */
async function waitForCompletion(promptId) {
  const ws = new WebSocket(`${WS_URL}/ws?clientId=${CLIENT_ID}`);
  const nodeOutputs = {};

  return new Promise((resolve, reject) => {
    ws.on('open', () => console.log('WebSocket connected'));
    ws.on('error', reject);

    ws.on('message', async (data, isBinary) => {
      if (isBinary) {
        // Binary: preview image
        const preview = decodePreviewImage(Buffer.isBuffer(data) ? data : Buffer.from(data));
        if (preview) {
          await writeFile(`output/preview_${Date.now()}.${preview.extension}`, preview.imageBytes);
          console.log('Preview saved');
        }
      } else {
        // JSON: event
        const msg = JSON.parse(data.toString());
        if (msg.data?.prompt_id !== promptId) return; // ignore other clients' events

        if (msg.type === 'progress') {
          const { value, max } = msg.data;
          process.stdout.write(`\rSampling: ${((value/max)*100).toFixed(1)}%`);

        } else if (msg.type === 'executing') {
          if (msg.data.node == null) {
            // null node = all nodes finished
            ws.close();
            resolve(nodeOutputs);
          } else {
            console.log(`Executing node ${msg.data.node}`);
          }

        } else if (msg.type === 'executed') {
          nodeOutputs[msg.data.node] = msg.data.output;

        } else if (msg.type === 'execution_error') {
          ws.close();
          reject(new Error(`Execution error: ${msg.data.exception_message}`));
        }
      }
    });
  });
}
```

#### C\#

The C# version follows the same logic. See the complete implementation in [`csharp/MinimalExample.cs`](./csharp/MinimalExample.cs) — the `WaitForCompletionAsync` method.

---

## 4. Download the Final Image

After execution, the `executed` event for the SaveImage node contains the filenames of the generated images. Download each one via `GET /view`.

### JavaScript

```javascript
/**
 * Downloads a generated image and saves it locally.
 * @param {string} filename - Filename from node output.
 * @param {string} subfolder - Subfolder inside ComfyUI's output directory.
 * @param {string} type - "output" for generated images, "input" for uploads.
 */
async function downloadImage(filename, subfolder = '', type = 'output') {
  const query = new URLSearchParams({ filename, subfolder, type });
  const response = await fetch(`${BASE_URL}/view?${query}`, { headers: buildHeaders({'Content-Type': ''}) });
  if (!response.ok) throw new Error(`GET /view failed: HTTP ${response.status}`);

  await mkdir('output', { recursive: true });
  await pipeline(response.body, createWriteStream(`output/${filename}`));
  console.log(`Downloaded → output/${filename}`);
}

// Extract filenames from node outputs after waitForCompletion()
function extractImages(nodeOutputs) {
  return Object.values(nodeOutputs)
    .flatMap(output => output?.images ?? [])
    .map(img => ({ filename: img.filename, subfolder: img.subfolder ?? '', type: img.type ?? 'output' }));
}
```

### C\#

```csharp
/// <summary>
/// Downloads a generated image from the ComfyUI server and saves it locally.
/// </summary>
static async Task<string> DownloadImageAsync(
    HttpClient client, string filename, string subfolder = "", string type = "output")
{
    string url = $"{BaseUrl}/view?filename={Uri.EscapeDataString(filename)}"
               + $"&subfolder={Uri.EscapeDataString(subfolder)}"
               + $"&type={Uri.EscapeDataString(type)}";

    var response = await client.GetAsync(url);
    response.EnsureSuccessStatusCode();

    Directory.CreateDirectory("output");
    string localPath = Path.Combine("output", filename);
    await using var fs = File.Create(localPath);
    await response.Content.CopyToAsync(fs);

    Console.WriteLine($"Downloaded → {localPath}");
    return localPath;
}
```

---

## Running the Examples

### JavaScript

```bash
# Install the WebSocket library (Node.js 18+ required for global fetch)
npm install ws

# Run the example
node api_docs/examples/javascript/minimal-example.js
```

### C\#

```bash
# Create a new console project
dotnet new console -n ComfyMinimal
cd ComfyMinimal

# Add required NuGet packages
dotnet add package System.Net.Http.Json

# Copy MinimalExample.cs into the project, then run
dotnet run
```

---

## See Also

- [Authentication Guide](../authentication.md) — single-user vs multi-user setup
- [WebSocket Messages Reference](../websocket_messages.md) — full event schema
- [Preview & Output Retrieval](../previews_and_outputs.md) — binary preview formats
- [Core Endpoints](../core_endpoints.md) — `/prompt`, `/queue`, `/history`, `/view`
