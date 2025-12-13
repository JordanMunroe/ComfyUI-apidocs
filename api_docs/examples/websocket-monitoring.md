# Example: WebSocket Monitoring and Progress Tracking

This example demonstrates how to monitor workflow execution in real-time using WebSocket connections, including progress tracking and preview image handling.

## Overview

WebSocket connections provide real-time updates about workflow execution, including:
- Execution status changes
- Progress updates with step counts
- Preview images during generation
- Completion notifications
- Error messages

## Prerequisites

- ComfyUI server running (default: `http://127.0.0.1:8188`)
- Python 3.7+ with `websockets` library: `pip install websockets`
- Basic understanding of async/await in Python

## Complete Example

```python
import asyncio
import websockets
import json
import uuid
import struct
from PIL import Image
from io import BytesIO

COMFYUI_URL = "127.0.0.1:8188"
client_id = str(uuid.uuid4())

async def monitor_execution():
    """
    Connect to ComfyUI WebSocket and monitor execution in real-time.
    """
    uri = f"ws://{COMFYUI_URL}/ws?clientId={client_id}"
    
    print(f"Connecting to {uri}...")
    
    async with websockets.connect(uri) as websocket:
        print(f"✓ Connected with client ID: {client_id}")
        
        # Wait for messages
        async for message in websocket:
            if isinstance(message, bytes):
                # Binary message (preview image)
                await handle_binary_message(message)
            else:
                # JSON message (status, progress, etc.)
                await handle_json_message(message)

async def handle_json_message(message):
    """
    Handle JSON messages from the WebSocket.
    """
    try:
        data = json.loads(message)
        msg_type = data.get('type')
        msg_data = data.get('data', {})
        
        if msg_type == 'status':
            # Queue status update
            status = msg_data.get('status', {})
            exec_info = status.get('exec_info', {})
            queue_remaining = exec_info.get('queue_remaining', 0)
            print(f"📊 Status: {queue_remaining} items in queue")
            
        elif msg_type == 'execution_start':
            # Workflow execution started
            prompt_id = msg_data.get('prompt_id')
            print(f"▶️  Execution started: {prompt_id}")
            
        elif msg_type == 'executing':
            # Node execution update
            node = msg_data.get('node')
            prompt_id = msg_data.get('prompt_id')
            if node is None:
                print(f"✓ Execution completed: {prompt_id}")
            else:
                print(f"⚙️  Executing node: {node}")
                
        elif msg_type == 'progress':
            # Progress update during node execution
            value = msg_data.get('value', 0)
            max_val = msg_data.get('max', 100)
            node = msg_data.get('node', 'unknown')
            percentage = (value / max_val * 100) if max_val > 0 else 0
            
            # Create progress bar
            bar_length = 30
            filled = int(bar_length * value / max_val) if max_val > 0 else 0
            bar = '█' * filled + '░' * (bar_length - filled)
            
            print(f"📈 Progress [{bar}] {percentage:.1f}% ({value}/{max_val}) - Node {node}")
            
        elif msg_type == 'executed':
            # Node execution completed with outputs
            node = msg_data.get('node')
            output = msg_data.get('output', {})
            print(f"✓ Node {node} completed")
            
            # Check for image outputs
            if 'images' in output:
                image_count = len(output['images'])
                print(f"  📷 Generated {image_count} image(s)")
                for img in output['images']:
                    print(f"     - {img['filename']} ({img['type']})")
                    
        elif msg_type == 'execution_cached':
            # Node output retrieved from cache
            nodes = msg_data.get('nodes', [])
            print(f"💾 Cached: {len(nodes)} node(s) retrieved from cache")
            
        elif msg_type == 'execution_error':
            # Execution error occurred
            prompt_id = msg_data.get('prompt_id')
            node_id = msg_data.get('node_id')
            exception_type = msg_data.get('exception_type', 'Unknown')
            exception_message = msg_data.get('exception_message', '')
            
            print(f"❌ Execution Error!")
            print(f"   Prompt: {prompt_id}")
            print(f"   Node: {node_id}")
            print(f"   Type: {exception_type}")
            print(f"   Message: {exception_message}")
            
        elif msg_type == 'feature_flags':
            # Feature flags from server
            print(f"🚩 Server feature flags: {msg_data}")
            
        else:
            print(f"📨 Unknown message type: {msg_type}")
            
    except json.JSONDecodeError as e:
        print(f"❌ Error parsing JSON: {e}")

async def handle_binary_message(message):
    """
    Handle binary preview images from the WebSocket.
    """
    try:
        # Read event type (first 4 bytes)
        event_type = struct.unpack('>I', message[0:4])[0]
        
        if event_type == 1:
            # PREVIEW_IMAGE
            image_format = struct.unpack('>I', message[4:8])[0]
            format_name = "JPEG" if image_format == 1 else "PNG"
            
            # Extract image data
            image_data = message[8:]
            image = Image.open(BytesIO(image_data))
            
            print(f"🖼️  Preview image received: {image.size} ({format_name})")
            
            # Save preview (optional)
            image.save(f"preview_latest.{format_name.lower()}")
            
        elif event_type == 4:
            # PREVIEW_IMAGE_WITH_METADATA
            metadata_length = struct.unpack('>I', message[4:8])[0]
            
            # Extract metadata
            metadata_json = message[8:8+metadata_length].decode('utf-8')
            metadata = json.loads(metadata_json)
            
            # Extract image
            image_data = message[8+metadata_length:]
            image = Image.open(BytesIO(image_data))
            
            print(f"🖼️  Preview with metadata: {image.size}")
            print(f"   Metadata: {metadata}")
            
            # Save with step number if available
            step = metadata.get('step', 'latest')
            image.save(f"preview_step_{step}.png")
            
        else:
            print(f"📦 Unknown binary event type: {event_type}")
            
    except Exception as e:
        print(f"❌ Error handling binary message: {e}")

# Run the monitor
if __name__ == "__main__":
    try:
        asyncio.run(monitor_execution())
    except KeyboardInterrupt:
        print("\n👋 Disconnected")
```

## JavaScript/Browser Example

For web applications, here's a browser-compatible version:

```javascript
class ComfyUIMonitor {
    constructor(serverUrl = 'ws://127.0.0.1:8188') {
        this.serverUrl = serverUrl;
        this.clientId = this.generateUUID();
        this.ws = null;
    }
    
    generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c == 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
    
    connect() {
        const uri = `${this.serverUrl}/ws?clientId=${this.clientId}`;
        console.log(`Connecting to ${uri}...`);
        
        this.ws = new WebSocket(uri);
        
        this.ws.onopen = () => {
            console.log('✓ WebSocket connected');
        };
        
        this.ws.onmessage = async (event) => {
            if (event.data instanceof Blob) {
                await this.handleBinaryMessage(event.data);
            } else {
                this.handleJsonMessage(event.data);
            }
        };
        
        this.ws.onerror = (error) => {
            console.error('WebSocket error:', error);
        };
        
        this.ws.onclose = () => {
            console.log('WebSocket disconnected');
        };
    }
    
    handleJsonMessage(message) {
        const data = JSON.parse(message);
        const type = data.type;
        const msgData = data.data || {};
        
        switch (type) {
            case 'status':
                const queueRemaining = msgData.status?.exec_info?.queue_remaining || 0;
                console.log(`📊 Queue: ${queueRemaining} items`);
                break;
                
            case 'execution_start':
                console.log(`▶️  Execution started: ${msgData.prompt_id}`);
                break;
                
            case 'executing':
                if (msgData.node === null) {
                    console.log(`✓ Execution completed`);
                } else {
                    console.log(`⚙️  Executing node: ${msgData.node}`);
                }
                break;
                
            case 'progress':
                const percent = (msgData.value / msgData.max * 100).toFixed(1);
                console.log(`📈 Progress: ${percent}% (${msgData.value}/${msgData.max})`);
                
                // Update UI progress bar
                this.updateProgressBar(msgData.value, msgData.max);
                break;
                
            case 'executed':
                console.log(`✓ Node ${msgData.node} completed`);
                if (msgData.output?.images) {
                    console.log(`  📷 ${msgData.output.images.length} image(s) generated`);
                }
                break;
                
            case 'execution_error':
                console.error(`❌ Error in node ${msgData.node_id}:`, msgData.exception_message);
                break;
        }
    }
    
    async handleBinaryMessage(blob) {
        const arrayBuffer = await blob.arrayBuffer();
        const dataView = new DataView(arrayBuffer);
        const eventType = dataView.getUint32(0);
        
        if (eventType === 1) {
            // PREVIEW_IMAGE
            const imageFormat = dataView.getUint32(4);
            const mimeType = imageFormat === 1 ? 'image/jpeg' : 'image/png';
            const imageBlob = new Blob([arrayBuffer.slice(8)], { type: mimeType });
            
            const imageUrl = URL.createObjectURL(imageBlob);
            this.displayPreview(imageUrl);
            
        } else if (eventType === 4) {
            // PREVIEW_IMAGE_WITH_METADATA
            const metadataLength = dataView.getUint32(4);
            const metadataBytes = new Uint8Array(arrayBuffer, 8, metadataLength);
            const metadata = JSON.parse(new TextDecoder().decode(metadataBytes));
            
            const imageBlob = new Blob([arrayBuffer.slice(8 + metadataLength)], {
                type: metadata.image_type || 'image/png'
            });
            
            const imageUrl = URL.createObjectURL(imageBlob);
            this.displayPreview(imageUrl, metadata);
        }
    }
    
    updateProgressBar(value, max) {
        const progressBar = document.getElementById('progress-bar');
        const progressText = document.getElementById('progress-text');
        
        if (progressBar && progressText) {
            const percent = (value / max * 100);
            progressBar.style.width = `${percent}%`;
            progressText.textContent = `${value}/${max} (${percent.toFixed(1)}%)`;
        }
    }
    
    displayPreview(imageUrl, metadata = null) {
        const previewImg = document.getElementById('preview-image');
        if (previewImg) {
            // Revoke old URL to prevent memory leaks
            if (previewImg.dataset.oldUrl) {
                URL.revokeObjectURL(previewImg.dataset.oldUrl);
            }
            
            previewImg.src = imageUrl;
            previewImg.dataset.oldUrl = imageUrl;
            
            if (metadata) {
                console.log('Preview metadata:', metadata);
            }
        }
    }
    
    disconnect() {
        if (this.ws) {
            this.ws.close();
        }
    }
}

// Usage
const monitor = new ComfyUIMonitor();
monitor.connect();
```

## HTML for Browser Example

```html
<!DOCTYPE html>
<html>
<head>
    <title>ComfyUI Monitor</title>
    <style>
        .progress-container {
            width: 100%;
            background-color: #f0f0f0;
            border-radius: 4px;
            margin: 20px 0;
        }
        .progress-bar {
            height: 30px;
            background-color: #4CAF50;
            border-radius: 4px;
            transition: width 0.3s;
            width: 0%;
        }
        #preview-image {
            max-width: 512px;
            border: 1px solid #ccc;
            border-radius: 4px;
        }
    </style>
</head>
<body>
    <h1>ComfyUI Execution Monitor</h1>
    
    <div class="progress-container">
        <div id="progress-bar" class="progress-bar"></div>
    </div>
    <div id="progress-text">Ready</div>
    
    <h2>Preview</h2>
    <img id="preview-image" alt="Preview will appear here">
    
    <script src="comfyui-monitor.js"></script>
</body>
</html>
```

## Key Concepts

### Client ID
The client ID must be consistent between:
1. WebSocket connection (`?clientId=...`)
2. Workflow submission (`"client_id"` in prompt data)

This ensures you receive updates for your specific workflows.

### Message Types

| Type | Format | Description |
|------|--------|-------------|
| `status` | JSON | Queue status updates |
| `execution_start` | JSON | Workflow begins executing |
| `executing` | JSON | Current node being executed |
| `progress` | JSON | Progress percentage |
| `executed` | JSON | Node completed with outputs |
| `execution_cached` | JSON | Cached results used |
| `execution_error` | JSON | Error occurred |
| `PREVIEW_IMAGE` | Binary | Preview image (Type 1) |
| `PREVIEW_IMAGE_WITH_METADATA` | Binary | Preview with metadata (Type 4) |

## Best Practices

1. **Reconnection Logic**: Implement automatic reconnection if the WebSocket disconnects
2. **Memory Management**: Revoke old blob URLs to prevent memory leaks
3. **Error Handling**: Always wrap message parsing in try-catch blocks
4. **UI Updates**: Use progress events to update UI smoothly
5. **Logging**: Log important events for debugging

## Next Steps

- [Simple workflow execution](./simple-workflow-execution.md)
- [Download generated images](./download-outputs.md)
- [Queue management](./queue-management.md)

## Related Documentation

- [WebSocket Connection API](../API.md#websocket-connection)
- [Previews and Progress Tracking](../API.md#previews-and-progress-tracking)
- [Binary Message Formats](../API.md#websocket-binary-messages)
