# ComfyUI API Examples

This directory contains practical, ready-to-use examples demonstrating common ComfyUI API patterns and use cases.

## Available Examples

### 🚀 [Simple Workflow Execution](./simple-wo## 📚 Additional Resources

- [Main API Documentation](../API.md)low-execution.md)
Learn the fundamentals of workflow construction and submission.

**Topics Covered:**
- Basic workflow structure
- Node connections and references
- Submitting workflows to the queue
- Error handling
- Understanding workflow components

**Best For:** Beginners getting started with the ComfyUI API

---

### 📡 [WebSocket Monitoring and Progress Tracking](./websocket-monitoring.md)
Real-time monitoring of workflow execution with WebSocket connections.

**Topics Covered:**
- WebSocket connection setup
- Handling JSON and binary messages
- Progress tracking and display
- Preview image decoding
- JavaScript/Browser implementation
- Python async implementation

**Best For:** Building interactive UIs that show real-time progress

---

### 🖼️ [Image Upload and Image-to-Image Workflow](./image-upload-workflow.md)
Upload images and use them in img2img workflows for modification and enhancement.

**Topics Covered:**
- Image upload API
- Image-to-image workflows
- VAE encoding/decoding
- Denoise strength control
- Mask upload for inpainting
- Format conversion

**Best For:** Applications that modify existing images

---

### 👥 [Multi-User Mode Guide](./multi-user-mode.md)
Complete guide for using ComfyUI in multi-user mode with isolated workspaces.

**Topics Covered:**
- Enabling and configuring multi-user mode
- User authentication and session management
- Client ID management
- Text-to-image in multi-user mode
- Image-to-image in multi-user mode
- User settings and preferences
- Queue monitoring per user
- Concurrent workflow execution
- Resource isolation

**Best For:** Multi-tenant applications, team environments, and production deployments

---

### 💾 [Download Generated Images](./download-outputs.md)
Retrieve and download generated images from ComfyUI history.

**Topics Covered:**
- Querying execution history
- Downloading output images
- Format conversion on-the-fly
- Alpha channel extraction
- Batch downloading
- Wait for completion patterns

**Best For:** Automated pipelines and result retrieval

---

### ⚙️ [Queue Management](./queue-management.md)
Manage the ComfyUI execution queue with monitoring, cancellation, and throttling.

**Topics Covered:**
- Queue status checking
- Clearing and deleting items
- Interrupting executions
- Priority submission (front of queue)
- Queue throttling and limits
- Monitoring and analysis

**Best For:** Building robust queue management systems

---

## Quick Start

All examples are self-contained and include:
- Complete working code
- Detailed explanations
- Prerequisites
- Usage instructions
- Related documentation links

### Prerequisites

Most examples require:
```bash
pip install requests websockets pillow
```

### Running Examples

1. Ensure ComfyUI server is running (default: `http://127.0.0.1:8188`)
2. Copy the example code
3. Update configuration variables (model names, paths, etc.)
4. Run the script

## Example Usage Flow

For a complete workflow from start to finish:

1. **[Simple Workflow Execution](./simple-workflow-execution.md)** - Submit a text-to-image workflow
2. **[WebSocket Monitoring](./websocket-monitoring.md)** - Monitor the execution in real-time
3. **[Download Outputs](./download-outputs.md)** - Retrieve the generated images

Or for image modification:

1. **[Image Upload](./image-upload-workflow.md)** - Upload an image and create img2img workflow
2. **[WebSocket Monitoring](./websocket-monitoring.md)** - Watch the progress
3. **[Download Outputs](./download-outputs.md)** - Get the modified image

## Language Support

### Python
All examples include complete Python implementations using standard libraries:
- `requests` for HTTP API calls
- `websockets` for WebSocket connections
- `PIL/Pillow` for image handling

### JavaScript/Browser
WebSocket monitoring includes a complete browser-based implementation showing:
- Native WebSocket API usage
- Blob/ArrayBuffer handling
- UI updates and progress bars
- Memory management (URL cleanup)

## Common Patterns

### Error Handling
```python
response = requests.post(url, json=data)
if response.status_code == 200:
    result = response.json()
    if 'node_errors' in result:
        # Handle validation errors
    else:
        # Success
else:
    # Handle HTTP errors
```

### Client ID Usage
```python
import uuid
client_id = str(uuid.uuid4())

# Use in WebSocket
ws_url = f"ws://127.0.0.1:8188/ws?clientId={client_id}"

# Use in workflow submission
prompt_data = {
    "prompt": workflow,
    "client_id": client_id  # Same ID!
}
```

### Async/Await Pattern (Python)
```python
import asyncio

async def main():
    # Your async code here
    pass

if __name__ == "__main__":
    asyncio.run(main())
```

## Troubleshooting

### Connection Refused
- Ensure ComfyUI server is running
- Check the correct host and port (default: 127.0.0.1:8188)
- Verify no firewall blocking

### WebSocket Disconnects
- Implement reconnection logic
- Check for network stability
- Monitor for server restarts

### Image Upload Fails
- Verify image path is correct
- Check image format is supported
- Ensure sufficient disk space

### No Preview Images
- Verify client_id matches between WebSocket and workflow
- Check that nodes support previews (e.g., KSampler)
- Ensure WebSocket is connected before submitting workflow

## Additional Resources

- [Main API Documentation](../API.md)
- [ComfyUI GitHub Repository](https://github.com/comfyanonymous/ComfyUI)
- [ComfyUI Discord Community](https://www.comfy.org/discord)

## Contributing Examples

Have a useful example? Consider contributing:
1. Follow the existing format and style
2. Include complete, working code
3. Add detailed explanations
4. Test thoroughly
5. Submit a pull request

## License

These examples are provided as-is for educational purposes. Use and modify freely in your own projects.
