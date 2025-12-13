# ComfyUI API Quick Start Guide

Get started with the ComfyUI API in 5 minutes!

## 🚀 Quick Setup

### 1. Start ComfyUI Server

```bash
python main.py
```

Default server: `http://127.0.0.1:8188`

### 2. Install .NET Dependencies

```bash
dotnet add package System.Net.Http.Json
dotnet add package System.Text.Json
```

### 3. Test Connection

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

// Test if server is running
using var client = new HttpClient();
var response = await client.GetAsync("http://127.0.0.1:8188/system_stats");
if (response.IsSuccessStatusCode)
{
    Console.WriteLine("✓ Server is running!");
    var stats = await response.Content.ReadFromJsonAsync<JsonElement>();
    var version = stats.GetProperty("system").GetProperty("comfyui_version").GetString();
    Console.WriteLine($"Version: {version}");
}
else
{
    Console.WriteLine("✗ Server not responding");
}
```

## 📝 Your First Workflow (30 seconds)

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

// Simple text-to-image workflow
var workflow = new
{
    _1 = new
    {
        inputs = new { ckpt_name = "sd_xl_base_1.0.safetensors" },
        class_type = "CheckpointLoaderSimple"
    },
    _2 = new
    {
        inputs = new
        {
            text = "a beautiful sunset over mountains",
            clip = new object[] { "1", 1 }
        },
        class_type = "CLIPTextEncode"
    },
    _3 = new
    {
        inputs = new { width = 512, height = 512, batch_size = 1 },
        class_type = "EmptyLatentImage"
    },
    _4 = new
    {
        inputs = new
        {
            seed = 42, steps = 20, cfg = 7.0,
            sampler_name = "euler", scheduler = "normal",
            denoise = 1.0,
            model = new object[] { "1", 0 },
            positive = new object[] { "2", 0 },
            negative = new object[] { "2", 0 },
            latent_image = new object[] { "3", 0 }
        },
        class_type = "KSampler"
    },
    _5 = new
    {
        inputs = new { samples = new object[] { "4", 0 }, vae = new object[] { "1", 2 } },
        class_type = "VAEDecode"
    },
    _6 = new
    {
        inputs = new { filename_prefix = "quickstart", images = new object[] { "5", 0 } },
        class_type = "SaveImage"
    }
};

// Submit it!
using var client = new HttpClient();
var response = await client.PostAsJsonAsync(
    "http://127.0.0.1:8188/prompt",
    new { prompt = workflow, client_id = Guid.NewGuid().ToString() }
);

if (response.IsSuccessStatusCode)
{
    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    var promptId = result.GetProperty("prompt_id").GetString();
    Console.WriteLine($"✓ Workflow queued! ID: {promptId}");
}
else
{
    var error = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"✗ Error: {error}");
}
```

## 🎯 Common Tasks

### Check Queue
```csharp
using var client = new HttpClient();
var response = await client.GetAsync("http://127.0.0.1:8188/queue");
var queue = await response.Content.ReadFromJsonAsync<JsonElement>();
var running = queue.GetProperty("queue_running").GetArrayLength();
var pending = queue.GetProperty("queue_pending").GetArrayLength();
Console.WriteLine($"Running: {running}");
Console.WriteLine($"Pending: {pending}");
```

### Download Latest Image
```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

// Get recent history
using var client = new HttpClient();
var history = await client.GetFromJsonAsync<JsonElement>(
    "http://127.0.0.1:8188/history?max_items=1"
);

foreach (var item in history.EnumerateObject())
{
    var outputs = item.Value.GetProperty("outputs");
    foreach (var output in outputs.EnumerateObject())
    {
        if (output.Value.TryGetProperty("images", out var images))
        {
            foreach (var img in images.EnumerateArray())
            {
                var filename = img.GetProperty("filename").GetString();
                
                // Download image
                var imgData = await client.GetByteArrayAsync(
                    $"http://127.0.0.1:8188/view?filename={filename}&type=output"
                );
                
                // Save it
                await File.WriteAllBytesAsync(filename, imgData);
                
                Console.WriteLine($"✓ Downloaded: {filename}");
            }
        }
    }
}
```

### Upload Image for img2img
```csharp
// Upload
using var content = new MultipartFormDataContent();
var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync("input.png"));
fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
content.Add(fileContent, "image", "input.png");
content.Add(new StringContent("input"), "type");

using var client = new HttpClient();
var response = await client.PostAsync(
    "http://127.0.0.1:8188/upload/image",
    content
);

var uploaded = await response.Content.ReadFromJsonAsync<JsonElement>();
var uploadedName = uploaded.GetProperty("name").GetString();
Console.WriteLine($"✓ Uploaded: {uploadedName}");

// Use in workflow (node 4)
var node4 = new
{
    inputs = new
    {
        image = uploadedName,
        upload = "image"
    },
    class_type = "LoadImage"
};
```

### Monitor with WebSocket (Simple)
```csharp
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

var clientId = Guid.NewGuid().ToString();

using var ws = new ClientWebSocket();
await ws.ConnectAsync(
    new Uri($"ws://127.0.0.1:8188/ws?clientId={clientId}"),
    CancellationToken.None
);

var buffer = new byte[8192];
while (ws.State == WebSocketState.Open)
{
    var result = await ws.ReceiveAsync(
        new ArraySegment<byte>(buffer),
        CancellationToken.None
    );
    
    if (result.MessageType == WebSocketMessageType.Text)
    {
        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var data = JsonDocument.Parse(message);
        var type = data.RootElement.GetProperty("type").GetString();
        
        if (type == "progress")
        {
            var info = data.RootElement.GetProperty("data");
            var value = info.GetProperty("value").GetInt32();
            var max = info.GetProperty("max").GetInt32();
            var percent = (value / (double)max * 100);
            Console.WriteLine($"Progress: {percent:F1}%");
        }
        else if (type == "executing")
        {
            var nodeData = data.RootElement.GetProperty("data");
            if (nodeData.TryGetProperty("node", out var node))
            {
                Console.WriteLine($"Executing: node {node.GetString()}");
            }
            else
            {
                Console.WriteLine("✓ Complete!");
            }
        }
    }
}
```

## 📚 Next Steps

### Learn More
1. **[Complete API Reference](./api_docs/API.md)** - All endpoints documented
2. **[Detailed Examples](./api_docs/examples/README.md)** - Production-ready code
3. **[WebSocket Guide](./api_docs/examples/websocket-monitoring.md)** - Real-time monitoring

### Popular Examples
- [Simple Workflow Execution](./api_docs/examples/simple-workflow-execution.md) - Basics
- [Image Upload & img2img](./api_docs/examples/image-upload-workflow.md) - Image workflows
- [Queue Management](./api_docs/examples/queue-management.md) - Advanced control

### Try These Workflows
- **Text-to-Image**: Use the example above
- **Image-to-Image**: See [image upload example](./api_docs/examples/image-upload-workflow.md)
- **Upscaling**: Load upscale model + use UpscaleImage node
- **ControlNet**: Add ControlNet loader + preprocessor nodes

## 🔍 Useful Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /object_info` | List all available nodes |
| `GET /models` | List model types |
| `GET /models/{type}` | List models of type |
| `POST /prompt` | Execute workflow |
| `GET /queue` | Check queue status |
| `GET /history` | Get execution history |
| `GET /view?filename=...` | Download image |
| `POST /upload/image` | Upload image |

## ⚡ Pro Tips

1. **Save client_id**: Use same ID for WebSocket and workflows
2. **Check node info**: `GET /object_info` to see available nodes
3. **Handle errors**: Always check `node_errors` in response
4. **Use WebSocket**: Much better than polling for status
5. **Cache models**: Models stay loaded between workflows

## 🐛 Troubleshooting

**"Connection refused"**
```bash
# Check server is running
curl http://127.0.0.1:8188/system_stats
```

**"Model not found"**
```python
# List available models
models = requests.get("http://127.0.0.1:8188/models/checkpoints").json()
print(models)
```

**"Node errors in response"**
```python
result = response.json()
if 'node_errors' in result:
    for node_id, error in result['node_errors'].items():
        print(f"Node {node_id}: {error}")
```

**"Workflow not executing"**
1. Check queue: `GET /queue`
2. Check history: `GET /history/{prompt_id}`
3. Look for errors in history status

## 🎓 Learning Path

### Beginner (Start Here)
1. ✅ Run the "Your First Workflow" above
2. ✅ Check queue and download result
3. ✅ Read [Simple Workflow Example](./api_docs/examples/simple-workflow-execution.md)

### Intermediate
4. ⬜ Implement WebSocket monitoring
5. ⬜ Upload and use custom images
6. ⬜ Explore different node types

### Advanced
7. ⬜ Build queue management
8. ⬜ Handle binary preview images
9. ⬜ Create custom workflows programmatically

## 📖 Resources

- **[Full API Docs](./api_docs/API.md)** - Complete reference
- **[Examples](./api_docs/examples/)** - Working code samples
- **[ComfyUI GitHub](https://github.com/comfyanonymous/ComfyUI)** - Source code
- **[Discord](https://www.comfy.org/discord)** - Community help

## 💡 Code Templates

All examples are available in [`/api_docs/examples`](./api_docs/examples/):
- Copy and paste working code
- Modify for your needs
- Production-ready patterns
- Error handling included

---

**Ready to build?** Start with the workflow above, then explore the [examples directory](./api_docs/examples/)!

**Questions?** Check the [full API documentation](./api_docs/API.md) or [examples README](./api_docs/examples/README.md).
