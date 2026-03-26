# Minimal API Example

This guide provides a complete example for integrating with the ComfyUI API in both **JavaScript (Node.js)** and **C#**, using an object-oriented, multi-file structure. It covers every step you need to get an image out of ComfyUI from code:

1. [Check server status](#1-check-server-status)
2. [Queue a workflow](#2-queue-a-workflow)
3. [Monitor progress and receive preview images via WebSocket](#3-websocket-monitoring)
4. [Download the final generated image](#4-download-the-final-image)

Both examples handle **single-user** and **multi-user** modes by toggling a single property on the shared config object.

> **Runnable files — JavaScript** (`npm install && npm start`):
> | File | Responsibility |
> |------|---------------|
> | [`javascript/ComfyConfig.js`](./javascript/ComfyConfig.js) | Shared configuration class |
> | [`javascript/WorkflowBuilder.js`](./javascript/WorkflowBuilder.js) | Workflow graph builder class |
> | [`javascript/ComfyClient.js`](./javascript/ComfyClient.js) | HTTP API client class |
> | [`javascript/WebSocketMonitor.js`](./javascript/WebSocketMonitor.js) | WebSocket monitor class |
> | [`javascript/index.js`](./javascript/index.js) | Entry point |
>
> **Runnable files — C#** (`dotnet run`):
> | File | Responsibility |
> |------|---------------|
> | [`csharp/ComfyConfig.cs`](./csharp/ComfyConfig.cs) | Shared configuration class |
> | [`csharp/WorkflowBuilder.cs`](./csharp/WorkflowBuilder.cs) | Workflow graph builder class |
> | [`csharp/ComfyClient.cs`](./csharp/ComfyClient.cs) | HTTP API client class |
> | [`csharp/WebSocketMonitor.cs`](./csharp/WebSocketMonitor.cs) | WebSocket monitor class |
> | [`csharp/PreviewImage.cs`](./csharp/PreviewImage.cs) | `PreviewImage` record |
> | [`csharp/ImageDescriptor.cs`](./csharp/ImageDescriptor.cs) | `ImageDescriptor` record |
> | [`csharp/Program.cs`](./csharp/Program.cs) | Entry point |

---

## Authentication Modes

ComfyUI supports two modes:

| Mode | Server flag | What to do in code |
|------|-------------|--------------------|
| **Single-user** (default) | *(none)* | No special headers needed |
| **Multi-user** | `--multi-user` | Add `comfy-user: <user_id>` header to every request |

> There is no password system. For network-level access control, place ComfyUI behind a reverse proxy. See the [Authentication Guide](../authentication.md) for details.

### JavaScript

Auth mode is a single property on `ComfyConfig`. The `ComfyClient` class reads it
when building headers for every request.

```javascript
// ComfyConfig.js — one place for all settings
const config = new ComfyConfig({
  multiUser: false,   // flip to true for --multi-user server
  userId: 'alice',    // only used in multi-user mode
});

// ComfyClient.js — private helper used by every method
#buildHeaders(extra = {}) {
  const headers = { 'Content-Type': 'application/json', ...extra };
  if (this._config.multiUser) {
    headers['comfy-user'] = this._config.userId;
  }
  return headers;
}
```

### C\#

Auth mode is a single property on `ComfyConfig`. The `ComfyClient` constructor
applies the header globally to the shared `HttpClient`.

```csharp
// Program.cs — one place for all settings
var config = new ComfyConfig
{
    MultiUser = false,   // flip to true for --multi-user server
    UserId    = "alice", // only used in multi-user mode
};

// ComfyClient.cs — applied once in the constructor
if (_config.MultiUser)
{
    _httpClient.DefaultRequestHeaders.Add("comfy-user", _config.UserId);
}
```

---

## 1. Check Server Status

Call `GET /system_stats` to verify the server is reachable and to retrieve the running ComfyUI version.
This is implemented in the `ComfyClient` class.

### JavaScript (`ComfyClient.js`)

```javascript
/** HTTP client for the ComfyUI REST API. */
export class ComfyClient {
  constructor(config) {
    this._config = config; // shared ComfyConfig instance
  }

  /**
   * Fetches system statistics from the ComfyUI server.
   * @returns {Promise<object>} Parsed JSON from GET /system_stats.
   */
  async getServerStatus() {
    const response = await fetch(`${this._config.baseUrl}/system_stats`, {
      headers: this.#buildHeaders(),
    });
    if (!response.ok) throw new Error(`GET /system_stats failed: HTTP ${response.status}`);
    const stats = await response.json();
    console.log(`Server online — ComfyUI ${stats?.system?.comfyui_version ?? 'unknown'}`);
    return stats;
  }
}
```

### C\# (`ComfyClient.cs`)

```csharp
/// <summary>HTTP client for the ComfyUI REST API.</summary>
public class ComfyClient
{
    private readonly ComfyConfig _config;
    private readonly HttpClient  _httpClient;

    public ComfyClient(ComfyConfig config, HttpClient httpClient)
    {
        _config     = config;
        _httpClient = httpClient;
        if (_config.MultiUser)
            _httpClient.DefaultRequestHeaders.Add("comfy-user", _config.UserId);
    }

    /// <summary>Fetches system statistics from the ComfyUI server.</summary>
    public async Task GetServerStatusAsync(CancellationToken ct = default)
    {
        var stats = await _httpClient.GetFromJsonAsync<JsonElement>(
            $"{_config.BaseUrl}/system_stats", ct);
        // ... log version ...
    }
}
```

---

## 2. Queue a Workflow

A **workflow** is a JSON object where each key is a node ID and each value describes a node
(its type and inputs). Nodes wire to each other using `["node_id", output_index]` references.

The example below builds a minimal text-to-image pipeline:

```
CheckpointLoader → CLIPTextEncode (positive)
                 → CLIPTextEncode (negative)
EmptyLatentImage →
                   KSampler → VAEDecode → SaveImage
```

### Building the workflow (`WorkflowBuilder`)

#### JavaScript (`WorkflowBuilder.js`)

```javascript
/** Constructs ComfyUI workflow graphs. */
export class WorkflowBuilder {
  /**
   * Builds a minimal text-to-image workflow graph.
   * Node references: ["node_id", output_index]
   *   ["1", 0] = MODEL, ["1", 1] = CLIP, ["1", 2] = VAE
   */
  buildTxt2Img(
    positivePrompt = 'a beautiful sunset over mountains, photorealistic',
    negativePrompt = 'blurry, low quality, watermark',
    checkpointName = 'sd_xl_base_1.0.safetensors',
  ) {
    return {
      '1': { class_type: 'CheckpointLoaderSimple', inputs: { ckpt_name: checkpointName } },
      '2': { class_type: 'CLIPTextEncode', inputs: { text: positivePrompt, clip: ['1', 1] } },
      '3': { class_type: 'CLIPTextEncode', inputs: { text: negativePrompt, clip: ['1', 1] } },
      '4': { class_type: 'EmptyLatentImage', inputs: { width: 512, height: 512, batch_size: 1 } },
      '5': {
        class_type: 'KSampler',
        inputs: {
          seed: Math.floor(Math.random() * 2 ** 32),
          steps: 20, cfg: 7.0, sampler_name: 'euler', scheduler: 'normal', denoise: 1.0,
          model: ['1', 0], positive: ['2', 0], negative: ['3', 0], latent_image: ['4', 0],
        },
      },
      '6': { class_type: 'VAEDecode', inputs: { samples: ['5', 0], vae: ['1', 2] } },
      '7': { class_type: 'SaveImage', inputs: { filename_prefix: 'minimal_example', images: ['6', 0] } },
    };
  }
}
```

#### C\# (`WorkflowBuilder.cs`)

```csharp
/// <summary>Constructs ComfyUI workflow graphs.</summary>
public class WorkflowBuilder
{
    /// <summary>Builds a minimal text-to-image workflow graph.</summary>
    public object BuildTxt2Img(
        string positivePrompt = "a beautiful sunset over mountains, photorealistic",
        string negativePrompt = "blurry, low quality, watermark",
        string checkpointName = "sd_xl_base_1.0.safetensors")
    {
        int seed = new Random().Next();
        return new Dictionary<string, object>
        {
            ["1"] = new { class_type = "CheckpointLoaderSimple", inputs = new { ckpt_name = checkpointName } },
            // ... remaining nodes ...
        };
    }
}
```

### Submitting to the queue (`ComfyClient.QueueWorkflowAsync`)

The `ClientId` from `ComfyConfig` links the HTTP submission to the WebSocket session so
the server routes events and previews back to this client.

#### JavaScript

```javascript
// In ComfyClient:
async queueWorkflow(workflow) {
  const response = await fetch(`${this._config.baseUrl}/prompt`, {
    method: 'POST',
    headers: this.#buildHeaders(),
    body: JSON.stringify({ prompt: workflow, client_id: this._config.clientId }),
  });
  const result = await response.json();
  return result.prompt_id;
}

// Usage (index.js):
const promptId = await client.queueWorkflow(builder.buildTxt2Img());
```

#### C\#

```csharp
// In ComfyClient:
public async Task<string> QueueWorkflowAsync(object workflow, CancellationToken ct = default)
{
    var body = new { prompt = workflow, client_id = _config.ClientId };
    var response = await _httpClient.PostAsJsonAsync($"{_config.BaseUrl}/prompt", body, ct);
    var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    return result.GetProperty("prompt_id").GetString()!;
}

// Usage (Program.cs):
string promptId = await client.QueueWorkflowAsync(builder.BuildTxt2Img());
```

---

## 3. WebSocket Monitoring

The `WebSocketMonitor` class opens a connection and processes all server-pushed events.
Use the same `clientId` in both the WebSocket URL and the prompt body so the server
routes events for your prompt to your socket.

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

When a node (like KSampler) generates a preview image during sampling, it is sent as a
**binary WebSocket message** instead of JSON.

Two binary event types exist:

**Type 1 — PREVIEW_IMAGE**
```
[4B: event type = 1] [4B: format (1=JPEG, 2=PNG)] [image bytes]
```

**Type 4 — PREVIEW_IMAGE_WITH_METADATA**
```
[4B: event type = 4] [4B: metadata length] [UTF-8 JSON metadata] [image bytes]
```

#### JavaScript (`WebSocketMonitor.js` — private method)

```javascript
/** Real-time WebSocket monitor for ComfyUI workflow execution. */
export class WebSocketMonitor {
  constructor(config, outputDir = 'output') {
    this._config    = config;
    this._outputDir = outputDir;
  }

  /** Decodes a binary frame — ES2022 private class method. */
  #decodePreviewImage(buffer) {
    const eventType = buffer.readUInt32BE(0);
    if (eventType === 1) {
      return { extension: buffer.readUInt32BE(4) === 1 ? 'jpg' : 'png', imageBytes: buffer.subarray(8) };
    }
    if (eventType === 4) {
      const metaLen = buffer.readUInt32BE(4);
      const metadata = JSON.parse(buffer.subarray(8, 8 + metaLen).toString('utf-8'));
      return { extension: metadata.image_type === 'image/jpeg' ? 'jpg' : 'png',
               imageBytes: buffer.subarray(8 + metaLen), metadata };
    }
    return null;
  }
}
```

#### C\# (`WebSocketMonitor.cs` — private method)

```csharp
/// <summary>Real-time WebSocket monitor for ComfyUI workflow execution.</summary>
public class WebSocketMonitor
{
    private readonly ComfyConfig _config;

    public WebSocketMonitor(ComfyConfig config, string outputDir = "output")
    {
        _config   = config;
        OutputDir = outputDir;
    }

    /// <summary>Decodes a binary WebSocket frame into a PreviewImage.</summary>
    private static PreviewImage? DecodePreviewImage(byte[] buffer, int length)
    {
        if (length < 8) return null;
        uint eventType = ReadUInt32BigEndian(buffer, 0);
        if (eventType == 1)
        {
            uint formatCode = ReadUInt32BigEndian(buffer, 4);
            return new PreviewImage(formatCode == 1 ? "jpg" : "png", buffer[8..length], null);
        }
        // ... handle type 4 with metadata ...
        return null;
    }
}
```

### Full monitoring loop

#### JavaScript

```javascript
// In WebSocketMonitor:
async waitForCompletion(promptId) {
  const ws = new WebSocket(`${this._config.wsUrl}/ws?clientId=${this._config.clientId}`);
  const nodeOutputs = {};
  return new Promise((resolve, reject) => {
    ws.on('message', async (data, isBinary) => {
      if (isBinary) {
        const preview = this.#decodePreviewImage(Buffer.from(data));
        if (preview) await writeFile(`${this._outputDir}/preview_N.${preview.extension}`, preview.imageBytes);
      } else {
        const msg = JSON.parse(data.toString());
        if (msg.type === 'executing' && msg.data.node == null) { ws.close(); resolve(nodeOutputs); }
        if (msg.type === 'executed') nodeOutputs[msg.data.node] = msg.data.output;
        if (msg.type === 'execution_error') { ws.close(); reject(new Error(msg.data.exception_message)); }
      }
    });
  });
}

// Usage (index.js):
const nodeOutputs = await monitor.waitForCompletion(promptId);
```

#### C\#

```csharp
// In WebSocketMonitor:
public async Task<Dictionary<string, JsonElement>> WaitForCompletionAsync(
    string promptId, CancellationToken ct = default)
{
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri($"{_config.WsUrl}/ws?clientId={_config.ClientId}"), ct);
    // ... receive loop handles Binary (preview) and Text (events) frames ...
}

// Usage (Program.cs):
var nodeOutputs = await monitor.WaitForCompletionAsync(promptId);
```

---

## 4. Download the Final Image

After execution, the `executed` event for the SaveImage node contains the filenames of the
generated images. `ComfyClient.ExtractImages` parses them, and `ComfyClient.DownloadImageAsync`
fetches each one via `GET /view`.

### JavaScript

```javascript
// In ComfyClient (static utility):
static extractImages(nodeOutputs) {
  return Object.values(nodeOutputs)
    .flatMap(out => out?.images ?? [])
    .map(img => ({ filename: img.filename, subfolder: img.subfolder ?? '', type: img.type ?? 'output' }));
}

async downloadImage(filename, subfolder = '', type = 'output', destDir = 'output') {
  const query = new URLSearchParams({ filename, subfolder, type });
  const response = await fetch(`${this._config.baseUrl}/view?${query}`,
    { headers: this.#buildHeaders({ 'Content-Type': '' }) });
  await pipeline(response.body, createWriteStream(`${destDir}/${filename}`));
}

// Usage (index.js):
const images = ComfyClient.extractImages(nodeOutputs);
for (const img of images) await client.downloadImage(img.filename, img.subfolder, img.type);
```

### C\#

```csharp
// In ComfyClient (static utility + instance method):
public static List<ImageDescriptor> ExtractImages(Dictionary<string, JsonElement> nodeOutputs) { /* ... */ }

public async Task<string> DownloadImageAsync(string filename, string subfolder = "",
    string type = "output", string destDir = "output", CancellationToken ct = default)
{
    var response = await _httpClient.GetAsync($"{_config.BaseUrl}/view?filename=...", ct);
    await using var fs = File.Create(Path.Combine(destDir, filename));
    await response.Content.CopyToAsync(fs, ct);
    return Path.Combine(destDir, filename);
}

// Usage (Program.cs):
var images = ComfyClient.ExtractImages(nodeOutputs);
foreach (var img in images) await client.DownloadImageAsync(img.Filename, img.Subfolder, img.Type);
```

---

## Running the Examples

### JavaScript

```bash
cd api_docs/examples/javascript

# Install the WebSocket library (Node.js 18+ required for global fetch)
npm install

# Run the example
npm start
```

### C\#

```bash
cd api_docs/examples/csharp

# Restore packages and run (requires .NET 8+)
dotnet run
```

---

## See Also

- [Authentication Guide](../authentication.md) — single-user vs multi-user setup
- [WebSocket Messages Reference](../websocket_messages.md) — full event schema
- [Preview & Output Retrieval](../previews_and_outputs.md) — binary preview formats
- [Core Endpoints](../core_endpoints.md) — `/prompt`, `/queue`, `/history`, `/view`
