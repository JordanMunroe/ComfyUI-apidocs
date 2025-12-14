# ComfyUI API Documentation

![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Version](https://img.shields.io/badge/ComfyUI-0.3.76-green.svg)
![Status](https://img.shields.io/badge/status-Complete-success.svg)
![Examples](https://img.shields.io/badge/examples-5-blue.svg)

## 🎉 Overview

This documentation was created to provide comprehensive API reference for my own use. I've decided to also make it available to the comfy community as there seems to be zero documentation currently.

## Disclaimer
Generated from the ComfyUI source code using AI so some of the documentation may be incorrect, it is intended as more of a starting point and experimentation will be required.

---

## 📚 Documentation

### 🔧 [Setup Guide](SETUP.md)
**New to ComfyUI?** Complete installation guide for Windows, Linux, macOS, and Docker.

### 🎯 [Quick Start Guide](QUICKSTART.md) 
**Start here!** Get running in 5 minutes with working examples.

### 📖 [Complete API Reference](api_docs/API.md)
Comprehensive documentation of all endpoints, WebSocket formats.

**Includes:**
- ✅ All API endpoints with request/response examples
- ✅ WebSocket connection and binary message formats  
- ✅ Preview and progress tracking system
- ✅ Authentication and security options
- ✅ Error handling and status codes
- ✅ Best practices and patterns

### 💻 [Code Examples](api_docs/examples/README.md)
Five detailed examples:

1. **[Simple Workflow Execution](api_docs/examples/simple-workflow-execution.md)** - Complete text-to-image workflow
2. **[WebSocket Monitoring](api_docs/examples/websocket-monitoring.md)** - Real-time progress with Python & JavaScript  
3. **[Image Upload & img2img](api_docs/examples/image-upload-workflow.md)** - Upload and modify images
4. **[Download Outputs](api_docs/examples/download-outputs.md)** - Retrieve generated images
5. **[Queue Management](api_docs/examples/queue-management.md)** - Advanced queue control

### 📊 [Documentation Summary](SUMMARY.md)
Overview of all features, improvements, and statistics.

---

## 🚀 Quick Start

### Installation

```bash
# Install .NET packages
dotnet add package System.Net.Http.Json
dotnet add package System.Text.Json

# Start ComfyUI server (in ComfyUI directory)
python main.py
```

Server will be available at: `http://127.0.0.1:8188`

### Your First Workflow (30 seconds)

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

// Simple text-to-image workflow
var workflow = new
{
    _1 = new { inputs = new { ckpt_name = "sd_xl_base_1.0.safetensors" }, class_type = "CheckpointLoaderSimple" },
    _2 = new { inputs = new { text = "a beautiful sunset over mountains", clip = new object[] { "1", 1 } }, class_type = "CLIPTextEncode" },
    _3 = new { inputs = new { width = 512, height = 512, batch_size = 1 }, class_type = "EmptyLatentImage" },
    _4 = new { inputs = new { seed = 42, steps = 20, cfg = 7.0, sampler_name = "euler", scheduler = "normal",
                     model = new object[] { "1", 0 }, positive = new object[] { "2", 0 }, negative = new object[] { "2", 0 }, 
                     latent_image = new object[] { "3", 0 } }, class_type = "KSampler" },
    _5 = new { inputs = new { samples = new object[] { "4", 0 }, vae = new object[] { "1", 2 } }, class_type = "VAEDecode" },
    _6 = new { inputs = new { filename_prefix = "quickstart", images = new object[] { "5", 0 } }, class_type = "SaveImage" }
};

// Execute it!
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
    // Image will be saved to ComfyUI/output/quickstart_*.png
}
else
{
    var error = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"✗ Error: {error}");
}
```

**[See complete quick start guide →](QUICKSTART.md)**

---

## API Coverage
- ✅ Workflow execution and validation
- ✅ Real-time WebSocket monitoring
- ✅ Queue management and control
- ✅ Model and resource discovery
- ✅ Image upload and download
- ✅ User management (multi-user mode)
- ✅ System information and stats
- ✅ Preview and progress tracking
- ✅ History and output retrieval
- ✅ Settings management

---

## � Learning Path

### 🟢 Beginner
1. Read the [Quick Start Guide](QUICKSTART.md)
2. Try the first workflow example above
3. Explore [Simple Workflow Execution](api_docs/examples/simple-workflow-execution.md)

### 🟡 Intermediate  
4. Implement [WebSocket Monitoring](api_docs/examples/websocket-monitoring.md)
5. Try [Image Upload & img2img](api_docs/examples/image-upload-workflow.md)
6. Learn [Queue Management](api_docs/examples/queue-management.md)

### 🔴 Advanced
7. Study binary WebSocket protocols in [API Reference](api_docs/API.md#websocket-binary-messages)
8. Build custom workflows programmatically
9. Implement production error handling and retry logic

---

## 🎓 Example Scenarios

### Text-to-Image Generation
```csharp
// See: examples/simple-workflow-execution.md
var workflow = BuildTxt2ImgWorkflow(prompt: "a serene landscape");
await ExecuteWorkflowAsync(workflow);
```

### Image-to-Image Modification
```csharp
// See: examples/image-upload-workflow.md
var uploaded = await UploadImageAsync("input.png");
var workflow = BuildImg2ImgWorkflow(uploaded, strength: 0.6);
await ExecuteWorkflowAsync(workflow);
```

### Real-time Progress Monitoring
```csharp
// See: examples/websocket-monitoring.md
using var websocket = new ClientWebSocket();
await websocket.ConnectAsync(wsUrl, CancellationToken.None);
await foreach (var message in ReceiveMessagesAsync(websocket))
{
    HandleProgressUpdate(message);
}
```

### Batch Processing
```csharp
// See: examples/download-outputs.md
foreach (var imagePath in imageList)
{
    await UploadAndProcessAsync(imagePath);
    await DownloadResultAsync();
}
```

---

## �️ Additional Resources

### Official ComfyUI
- [ComfyUI Repository](https://github.com/comfyanonymous/ComfyUI)
- [ComfyUI Examples](https://github.com/comfyanonymous/ComfyUI_examples)
- [ComfyUI Custom Nodes](https://github.com/ltdrdata/ComfyUI-Manager)

### Community
- [ComfyUI Discord](https://discord.gg/comfyui)
- [ComfyUI Matrix](https://app.element.io/#/room/%23comfyui_space%3Amatrix.org)
- [ComfyUI Wiki](https://github.com/comfyanonymous/ComfyUI/wiki)

---

## 🏗️ Project Structure

```
ComfyUI-apidocs/
├── README.md                           # This file
├── QUICKSTART.md                       # 5-minute quick start guide
├── SUMMARY.md                          # Documentation summary and stats
├── api_docs/
│   ├── API.md                          # Complete API reference (1800+ lines)
│   └── examples/                       # Code examples directory
│       ├── README.md                   # Examples overview and navigation
│       ├── simple-workflow-execution.md    # Text-to-image basics
│       ├── websocket-monitoring.md     # Real-time progress tracking  
│       ├── image-upload-workflow.md    # Image uploads and img2img
│       ├── download-outputs.md         # Retrieving generated images
│       └── queue-management.md         # Advanced queue control
```

---

## 📞 Support
### General Support
I will not be providing any support until I've had the chance to test the API and learn the ins and outs... Please refer to comfy support below.

### ComfyUI Support  
For ComfyUI software issues:
- [ComfyUI Issues](https://github.com/comfyanonymous/ComfyUI/issues)
- [ComfyUI Discussions](https://github.com/comfyanonymous/ComfyUI/discussions)
- [ComfyUI Discord](https://discord.gg/comfyui)
---

**Made with ❤️ by the ComfyUI community**
