# ComfyUI API Quick Start Guide

Get started with the ComfyUI API in 5 minutes!

## 🚀 Quick Setup

### 1. Start ComfyUI Server

```bash
python main.py
```

Default server: `http://127.0.0.1:8188`

### 2. Install Python Dependencies

```bash
pip install requests websockets pillow
```

### 3. Test Connection

```python
import requests

# Test if server is running
response = requests.get("http://127.0.0.1:8188/system_stats")
if response.status_code == 200:
    print("✓ Server is running!")
    stats = response.json()
    print(f"Version: {stats['system']['comfyui_version']}")
else:
    print("✗ Server not responding")
```

## 📝 Your First Workflow (30 seconds)

```python
import requests
import uuid

# Simple text-to-image workflow
workflow = {
    "1": {
        "inputs": {"ckpt_name": "sd_xl_base_1.0.safetensors"},
        "class_type": "CheckpointLoaderSimple"
    },
    "2": {
        "inputs": {
            "text": "a beautiful sunset over mountains",
            "clip": ["1", 1]
        },
        "class_type": "CLIPTextEncode"
    },
    "3": {
        "inputs": {"width": 512, "height": 512, "batch_size": 1},
        "class_type": "EmptyLatentImage"
    },
    "4": {
        "inputs": {
            "seed": 42, "steps": 20, "cfg": 7.0,
            "sampler_name": "euler", "scheduler": "normal",
            "denoise": 1.0,
            "model": ["1", 0],
            "positive": ["2", 0],
            "negative": ["2", 0],
            "latent_image": ["3", 0]
        },
        "class_type": "KSampler"
    },
    "5": {
        "inputs": {"samples": ["4", 0], "vae": ["1", 2]},
        "class_type": "VAEDecode"
    },
    "6": {
        "inputs": {"filename_prefix": "quickstart", "images": ["5", 0]},
        "class_type": "SaveImage"
    }
}

# Submit it!
response = requests.post(
    "http://127.0.0.1:8188/prompt",
    json={"prompt": workflow, "client_id": str(uuid.uuid4())}
)

if response.status_code == 200:
    result = response.json()
    print(f"✓ Workflow queued! ID: {result['prompt_id']}")
else:
    print(f"✗ Error: {response.json()}")
```

## 🎯 Common Tasks

### Check Queue
```python
response = requests.get("http://127.0.0.1:8188/queue")
queue = response.json()
print(f"Running: {len(queue['queue_running'])}")
print(f"Pending: {len(queue['queue_pending'])}")
```

### Download Latest Image
```python
import requests

# Get recent history
history = requests.get("http://127.0.0.1:8188/history?max_items=1").json()

for prompt_id, data in history.items():
    outputs = data.get('outputs', {})
    for node_id, output in outputs.items():
        if 'images' in output:
            for img in output['images']:
                filename = img['filename']
                
                # Download image
                img_data = requests.get(
                    "http://127.0.0.1:8188/view",
                    params={'filename': filename, 'type': 'output'}
                ).content
                
                # Save it
                with open(filename, 'wb') as f:
                    f.write(img_data)
                
                print(f"✓ Downloaded: {filename}")
```

### Upload Image for img2img
```python
# Upload
with open("input.png", "rb") as f:
    files = {"image": f}
    data = {"type": "input"}
    response = requests.post(
        "http://127.0.0.1:8188/upload/image",
        files=files,
        data=data
    )
    uploaded = response.json()
    print(f"✓ Uploaded: {uploaded['name']}")

# Use in workflow (node 4)
"4": {
    "inputs": {
        "image": uploaded['name'],
        "upload": "image"
    },
    "class_type": "LoadImage"
}
```

### Monitor with WebSocket (Simple)
```python
import websocket
import json
import uuid

client_id = str(uuid.uuid4())

def on_message(ws, message):
    try:
        data = json.loads(message)
        if data['type'] == 'progress':
            info = data['data']
            percent = (info['value'] / info['max'] * 100)
            print(f"Progress: {percent:.1f}%")
        elif data['type'] == 'executing':
            node = data['data'].get('node')
            if node:
                print(f"Executing: node {node}")
            else:
                print("✓ Complete!")
    except:
        pass  # Binary message, skip for now

ws = websocket.WebSocketApp(
    f"ws://127.0.0.1:8188/ws?clientId={client_id}",
    on_message=on_message
)

# Run in background or separate thread
ws.run_forever()
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
