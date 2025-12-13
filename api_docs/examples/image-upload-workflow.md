# Example: Image Upload and Image-to-Image Workflow

This example demonstrates how to upload an image to ComfyUI and use it in an image-to-image (img2img) workflow.

## Overview

Image-to-image workflows start with an existing image and modify it according to a text prompt. This is useful for:
- Refining existing images
- Changing styles while preserving composition
- Upscaling or enhancing images
- Creative variations of source material

## Prerequisites

- ComfyUI server running (default: `http://127.0.0.1:8188`)
- Python 3.7+ with `requests` library
- An input image file
- A checkpoint model in your ComfyUI models directory

## Complete Example

```python
import requests
import json
import uuid
from pathlib import Path

COMFYUI_URL = "http://127.0.0.1:8188"
client_id = str(uuid.uuid4())

def upload_image(image_path, subfolder="", image_type="input", overwrite=False):
    """
    Upload an image to ComfyUI server.
    
    Args:
        image_path: Path to the image file
        subfolder: Optional subfolder within the type directory
        image_type: "input", "temp", or "output"
        overwrite: Whether to overwrite existing files
    
    Returns:
        dict: Upload response with filename, subfolder, and type
    """
    url = f"{COMFYUI_URL}/upload/image"
    
    # Prepare the file
    with open(image_path, 'rb') as f:
        files = {
            'image': (Path(image_path).name, f, 'image/png')
        }
        
        # Prepare form data
        data = {
            'type': image_type,
            'subfolder': subfolder
        }
        
        if overwrite:
            data['overwrite'] = 'true'
        
        # Upload
        response = requests.post(url, files=files, data=data)
        
    if response.status_code == 200:
        result = response.json()
        print(f"✓ Image uploaded successfully!")
        print(f"  Name: {result['name']}")
        print(f"  Subfolder: {result['subfolder']}")
        print(f"  Type: {result['type']}")
        return result
    else:
        print(f"✗ Upload failed: {response.status_code}")
        print(response.text)
        return None

def create_img2img_workflow(uploaded_image, prompt, negative_prompt, denoise_strength=0.75):
    """
    Create an image-to-image workflow.
    
    Args:
        uploaded_image: Response from upload_image()
        prompt: Positive text prompt
        negative_prompt: Negative text prompt
        denoise_strength: How much to modify the image (0.0-1.0)
    
    Returns:
        dict: Workflow definition
    """
    return {
        "1": {
            "inputs": {
                "ckpt_name": "sd_xl_base_1.0.safetensors"
            },
            "class_type": "CheckpointLoaderSimple"
        },
        "2": {
            "inputs": {
                "text": prompt,
                "clip": ["1", 1]
            },
            "class_type": "CLIPTextEncode"
        },
        "3": {
            "inputs": {
                "text": negative_prompt,
                "clip": ["1", 1]
            },
            "class_type": "CLIPTextEncode"
        },
        "4": {
            "inputs": {
                "image": uploaded_image['name'],
                "upload": "image"
            },
            "class_type": "LoadImage"
        },
        "5": {
            "inputs": {
                "pixels": ["4", 0],
                "vae": ["1", 2]
            },
            "class_type": "VAEEncode"
        },
        "6": {
            "inputs": {
                "seed": 42,
                "steps": 20,
                "cfg": 7.0,
                "sampler_name": "euler_ancestral",
                "scheduler": "normal",
                "denoise": denoise_strength,
                "model": ["1", 0],
                "positive": ["2", 0],
                "negative": ["3", 0],
                "latent_image": ["5", 0]
            },
            "class_type": "KSampler"
        },
        "7": {
            "inputs": {
                "samples": ["6", 0],
                "vae": ["1", 2]
            },
            "class_type": "VAEDecode"
        },
        "8": {
            "inputs": {
                "filename_prefix": "img2img",
                "images": ["7", 0]
            },
            "class_type": "SaveImage"
        }
    }

def execute_workflow(workflow):
    """
    Submit workflow to ComfyUI for execution.
    """
    response = requests.post(
        f"{COMFYUI_URL}/prompt",
        json={
            "prompt": workflow,
            "client_id": client_id
        }
    )
    
    if response.status_code == 200:
        result = response.json()
        print(f"✓ Workflow queued!")
        print(f"  Prompt ID: {result['prompt_id']}")
        return result
    else:
        print(f"✗ Failed to queue workflow: {response.status_code}")
        print(response.json())
        return None

# Main execution
if __name__ == "__main__":
    # Step 1: Upload the image
    image_path = "input_image.png"
    print(f"Uploading {image_path}...")
    uploaded = upload_image(image_path)
    
    if not uploaded:
        exit(1)
    
    # Step 2: Create workflow
    print("\nCreating workflow...")
    workflow = create_img2img_workflow(
        uploaded_image=uploaded,
        prompt="masterpiece, best quality, turn into oil painting style, vivid colors",
        negative_prompt="blurry, low quality, distorted, bad art",
        denoise_strength=0.6  # 0.6 = moderate changes, preserves original
    )
    
    # Step 3: Execute workflow
    print("\nExecuting workflow...")
    result = execute_workflow(workflow)
    
    if result:
        print(f"\n🎨 Generation started!")
        print(f"Monitor execution with client_id: {client_id}")
```

## Workflow Breakdown

### Node 4: LoadImage
Loads the uploaded image from the server.

```python
"4": {
    "inputs": {
        "image": uploaded_image['name'],  # Filename from upload response
        "upload": "image"
    },
    "class_type": "LoadImage"
}
```

**Outputs:**
- `[0]` - IMAGE (pixel space)
- `[1]` - MASK (alpha channel if present)

### Node 5: VAEEncode
Converts the pixel-space image to latent space.

```python
"5": {
    "inputs": {
        "pixels": ["4", 0],  # Image from LoadImage
        "vae": ["1", 2]      # VAE from checkpoint
    },
    "class_type": "VAEEncode"
}
```

### Denoise Strength

The `denoise` parameter in KSampler controls how much the image changes:

| Value | Effect |
|-------|--------|
| 0.0 | No changes (returns original) |
| 0.3-0.5 | Minor refinements |
| 0.6-0.7 | Moderate changes |
| 0.8-0.9 | Major transformations |
| 1.0 | Complete regeneration |

## Advanced: Upload with Mask (Inpainting)

For inpainting workflows, you can upload a mask:

```python
def upload_mask(mask_path, original_image_info, subfolder="", image_type="input"):
    """
    Upload a mask for inpainting.
    
    Args:
        mask_path: Path to mask image (white = modify, black = keep)
        original_image_info: Info about the original image
        subfolder: Optional subfolder
        image_type: Directory type
    """
    url = f"{COMFYUI_URL}/upload/mask"
    
    with open(mask_path, 'rb') as f:
        files = {
            'image': (Path(mask_path).name, f, 'image/png')
        }
        
        # Reference to original image
        original_ref = {
            'filename': original_image_info['name'],
            'type': original_image_info['type'],
            'subfolder': original_image_info['subfolder']
        }
        
        data = {
            'original_ref': json.dumps(original_ref),
            'type': image_type,
            'subfolder': subfolder
        }
        
        response = requests.post(url, files=files, data=data)
    
    if response.status_code == 200:
        result = response.json()
        print(f"✓ Mask uploaded: {result['name']}")
        return result
    else:
        print(f"✗ Mask upload failed: {response.status_code}")
        return None

# Usage
original = upload_image("original.png")
mask = upload_mask("mask.png", original)
```

## Checking Uploaded Images

You can verify uploaded images exist:

```python
def view_image(filename, image_type="input", subfolder=""):
    """
    Retrieve an uploaded image.
    """
    url = f"{COMFYUI_URL}/view"
    params = {
        'filename': filename,
        'type': image_type,
        'subfolder': subfolder
    }
    
    response = requests.get(url, params=params)
    
    if response.status_code == 200:
        # Save or display the image
        with open(f"downloaded_{filename}", 'wb') as f:
            f.write(response.content)
        print(f"✓ Downloaded: {filename}")
        return True
    else:
        print(f"✗ Failed to download: {response.status_code}")
        return False

# Usage
view_image(uploaded['name'])
```

## Image Format Conversion

Upload endpoint accepts various image formats:

```python
def upload_with_format_info(image_path):
    """
    Upload image and show format handling.
    """
    from PIL import Image
    
    # Check format before upload
    img = Image.open(image_path)
    print(f"Original format: {img.format}")
    print(f"Size: {img.size}")
    print(f"Mode: {img.mode}")
    
    # Upload
    result = upload_image(image_path)
    
    # ComfyUI may convert to PNG internally
    return result
```

**Supported Formats:**
- PNG (recommended, preserves transparency)
- JPEG/JPG
- WEBP
- BMP
- And other PIL-supported formats

## Error Handling

```python
def safe_upload(image_path, max_retries=3):
    """
    Upload with retry logic.
    """
    for attempt in range(max_retries):
        try:
            result = upload_image(image_path)
            if result:
                return result
            print(f"Retry {attempt + 1}/{max_retries}...")
        except Exception as e:
            print(f"Error on attempt {attempt + 1}: {e}")
            if attempt < max_retries - 1:
                import time
                time.sleep(1)
    
    return None
```

## Overwrite Behavior

By default, ComfyUI won't overwrite existing files:

```python
# First upload
result1 = upload_image("test.png")  # Saves as "test.png"

# Second upload (same file)
result2 = upload_image("test.png")  # Saves as "test (1).png"

# With overwrite
result3 = upload_image("test.png", overwrite=True)  # Replaces "test.png"
```

## File Deduplication

ComfyUI uses content hashing to avoid storing duplicates:

```python
# Upload same image twice (different filenames)
result1 = upload_image("image1.png")  # Uploads and saves
result2 = upload_image("image1_copy.png")  # Same content

# If images are identical, only one copy is stored
# Both results will reference the same stored file
```

## Next Steps

- [Download generated outputs](./download-outputs.md)
- [Monitor execution progress](./websocket-monitoring.md)
- [Queue management](./queue-management.md)

## Related Documentation

- [Image Upload API](../API.md#upload-image)
- [Image View API](../API.md#view-image)
- [LoadImage Node](../API.md#node-information)
