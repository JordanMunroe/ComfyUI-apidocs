# Example: Simple Workflow Execution

This example demonstrates how to execute a basic text-to-image workflow using the ComfyUI API.

## Overview

This workflow loads a checkpoint model, encodes a text prompt, and generates an image using the KSampler node. It shows the fundamental pattern of workflow construction and submission.

## Prerequisites

- ComfyUI server running (default: `http://127.0.0.1:8188`)
- Python 3.7+ with `requests` library
- A checkpoint model file in your ComfyUI models directory

## Complete Example

```python
import requests
import json
import uuid

# Configuration
COMFYUI_URL = "http://127.0.0.1:8188"
client_id = str(uuid.uuid4())

# Define the workflow
workflow = {
    "1": {
        "inputs": {
            "ckpt_name": "sd_xl_base_1.0.safetensors"
        },
        "class_type": "CheckpointLoaderSimple"
    },
    "2": {
        "inputs": {
            "text": "a beautiful landscape with mountains and a lake, sunset, highly detailed",
            "clip": ["1", 1]
        },
        "class_type": "CLIPTextEncode"
    },
    "3": {
        "inputs": {
            "text": "blurry, low quality, distorted",
            "clip": ["1", 1]
        },
        "class_type": "CLIPTextEncode"
    },
    "4": {
        "inputs": {
            "width": 1024,
            "height": 1024,
            "batch_size": 1
        },
        "class_type": "EmptyLatentImage"
    },
    "5": {
        "inputs": {
            "seed": 42,
            "steps": 20,
            "cfg": 8.0,
            "sampler_name": "euler",
            "scheduler": "normal",
            "denoise": 1.0,
            "model": ["1", 0],
            "positive": ["2", 0],
            "negative": ["3", 0],
            "latent_image": ["4", 0]
        },
        "class_type": "KSampler"
    },
    "6": {
        "inputs": {
            "samples": ["5", 0],
            "vae": ["1", 2]
        },
        "class_type": "VAEDecode"
    },
    "7": {
        "inputs": {
            "filename_prefix": "ComfyUI",
            "images": ["6", 0]
        },
        "class_type": "SaveImage"
    }
}

# Submit the workflow
response = requests.post(
    f"{COMFYUI_URL}/prompt",
    json={
        "prompt": workflow,
        "client_id": client_id
    }
)

# Check the response
if response.status_code == 200:
    result = response.json()
    prompt_id = result['prompt_id']
    print(f"✓ Workflow queued successfully!")
    print(f"  Prompt ID: {prompt_id}")
    print(f"  Queue Number: {result['number']}")
    
    if result.get('node_errors'):
        print(f"  Warnings: {result['node_errors']}")
else:
    print(f"✗ Error: {response.status_code}")
    print(response.json())
```

## Workflow Breakdown

### Node 1: CheckpointLoaderSimple
Loads the base model checkpoint from disk.

```python
"1": {
    "inputs": {
        "ckpt_name": "sd_xl_base_1.0.safetensors"
    },
    "class_type": "CheckpointLoaderSimple"
}
```

**Outputs:**
- `[0]` - MODEL
- `[1]` - CLIP
- `[2]` - VAE

### Node 2: Positive Prompt (CLIPTextEncode)
Encodes the positive text prompt.

```python
"2": {
    "inputs": {
        "text": "a beautiful landscape with mountains and a lake, sunset, highly detailed",
        "clip": ["1", 1]  # Uses CLIP output from node 1
    },
    "class_type": "CLIPTextEncode"
}
```

### Node 3: Negative Prompt (CLIPTextEncode)
Encodes what you want to avoid in the image.

```python
"3": {
    "inputs": {
        "text": "blurry, low quality, distorted",
        "clip": ["1", 1]
    },
    "class_type": "CLIPTextEncode"
}
```

### Node 4: EmptyLatentImage
Creates a blank latent image of specified dimensions.

```python
"4": {
    "inputs": {
        "width": 1024,
        "height": 1024,
        "batch_size": 1
    },
    "class_type": "EmptyLatentImage"
}
```

### Node 5: KSampler
The core sampling node that generates the image.

```python
"5": {
    "inputs": {
        "seed": 42,              # Random seed for reproducibility
        "steps": 20,             # Number of sampling steps
        "cfg": 8.0,              # Classifier-free guidance scale
        "sampler_name": "euler", # Sampling algorithm
        "scheduler": "normal",   # Noise scheduler
        "denoise": 1.0,          # Denoising strength (1.0 = full)
        "model": ["1", 0],       # MODEL from node 1
        "positive": ["2", 0],    # Positive conditioning from node 2
        "negative": ["3", 0],    # Negative conditioning from node 3
        "latent_image": ["4", 0] # Latent from node 4
    },
    "class_type": "KSampler"
}
```

### Node 6: VAEDecode
Decodes latent space to pixel space.

```python
"6": {
    "inputs": {
        "samples": ["5", 0],  # Latent output from KSampler
        "vae": ["1", 2]       # VAE from node 1
    },
    "class_type": "VAEDecode"
}
```

### Node 7: SaveImage
Saves the final image to disk.

```python
"7": {
    "inputs": {
        "filename_prefix": "ComfyUI",
        "images": ["6", 0]  # Decoded image from node 6
    },
    "class_type": "SaveImage"
}
```

## Understanding Node References

Node inputs can reference outputs from other nodes using the format:
```python
["node_id", output_index]
```

For example, `["1", 1]` means:
- Use output from node ID `"1"`
- Take the output at index `1` (second output, zero-indexed)

## Error Handling

```python
response = requests.post(f"{COMFYUI_URL}/prompt", json={"prompt": workflow, "client_id": client_id})

if response.status_code == 200:
    result = response.json()
    
    # Check for validation errors
    if result.get('node_errors'):
        print("Workflow has errors:")
        for node_id, error_info in result['node_errors'].items():
            print(f"  Node {node_id}:")
            for error in error_info['errors']:
                print(f"    - {error['type']}: {error['message']}")
    else:
        print(f"Success! Prompt ID: {result['prompt_id']}")
else:
    print(f"HTTP Error {response.status_code}")
    error_data = response.json()
    print(f"Error: {error_data.get('error', {}).get('message', 'Unknown error')}")
```

## Next Steps

- [Monitor execution with WebSocket](./websocket-monitoring.md)
- [Upload and use custom images](./image-upload-workflow.md)
- [Retrieve generated images](./download-outputs.md)

## Related Documentation

- [Workflow Execution API](../API.md#workflow-execution)
- [Queue Management API](../API.md#queue-management)
- [Error Handling](../API.md#error-handling)
