# Example: Download Generated Images

This example demonstrates how to retrieve generated images from ComfyUI's execution history and download them to your local system.

## Overview

After a workflow completes, ComfyUI stores the results in its history. You can query the history to find generated images and download them using the view endpoint.

## Prerequisites

- ComfyUI server running (default: `http://127.0.0.1:8188`)
- Python 3.7+ with `requests` library
- A completed workflow execution with outputs

## Complete Example

```python
import requests
import json
from pathlib import Path
import time

COMFYUI_URL = "http://127.0.0.1:8188"

def get_history(prompt_id=None, max_items=None):
    """
    Retrieve execution history.
    
    Args:
        prompt_id: Specific prompt ID to retrieve (optional)
        max_items: Maximum number of history items (optional)
    
    Returns:
        dict: History data
    """
    if prompt_id:
        url = f"{COMFYUI_URL}/history/{prompt_id}"
        params = {}
    else:
        url = f"{COMFYUI_URL}/history"
        params = {}
        if max_items:
            params['max_items'] = max_items
    
    response = requests.get(url, params=params)
    
    if response.status_code == 200:
        return response.json()
    else:
        print(f"✗ Failed to get history: {response.status_code}")
        return None

def download_image(filename, output_dir="downloads", image_type="output", subfolder=""):
    """
    Download an image from ComfyUI.
    
    Args:
        filename: Name of the image file
        output_dir: Local directory to save to
        image_type: "output", "input", or "temp"
        subfolder: Subfolder within the type directory
    
    Returns:
        str: Path to downloaded file
    """
    url = f"{COMFYUI_URL}/view"
    params = {
        'filename': filename,
        'type': image_type,
        'subfolder': subfolder
    }
    
    response = requests.get(url, params=params)
    
    if response.status_code == 200:
        # Create output directory
        Path(output_dir).mkdir(parents=True, exist_ok=True)
        
        # Save file
        output_path = Path(output_dir) / filename
        with open(output_path, 'wb') as f:
            f.write(response.content)
        
        print(f"✓ Downloaded: {output_path}")
        return str(output_path)
    else:
        print(f"✗ Failed to download {filename}: {response.status_code}")
        return None

def download_outputs_from_history(prompt_id, output_dir="downloads"):
    """
    Download all output images from a specific workflow execution.
    
    Args:
        prompt_id: The prompt ID from workflow execution
        output_dir: Directory to save downloaded images
    
    Returns:
        list: Paths to downloaded files
    """
    print(f"Fetching history for prompt: {prompt_id}")
    history = get_history(prompt_id)
    
    if not history or prompt_id not in history:
        print(f"✗ No history found for prompt {prompt_id}")
        return []
    
    prompt_data = history[prompt_id]
    outputs = prompt_data.get('outputs', {})
    
    downloaded_files = []
    
    # Iterate through all nodes that produced outputs
    for node_id, node_output in outputs.items():
        if 'images' in node_output:
            images = node_output['images']
            print(f"\nNode {node_id} generated {len(images)} image(s)")
            
            for img_info in images:
                filename = img_info['filename']
                subfolder = img_info.get('subfolder', '')
                image_type = img_info.get('type', 'output')
                
                print(f"  Downloading: {filename}")
                path = download_image(
                    filename=filename,
                    output_dir=output_dir,
                    image_type=image_type,
                    subfolder=subfolder
                )
                
                if path:
                    downloaded_files.append(path)
    
    return downloaded_files

def get_latest_outputs(output_dir="downloads", count=5):
    """
    Download images from the most recent workflow executions.
    
    Args:
        output_dir: Directory to save images
        count: Number of recent executions to check
    
    Returns:
        list: Downloaded file paths
    """
    print(f"Fetching {count} most recent executions...")
    history = get_history(max_items=count)
    
    if not history:
        print("✗ No history available")
        return []
    
    all_downloads = []
    
    for prompt_id in history.keys():
        print(f"\n{'='*60}")
        print(f"Prompt ID: {prompt_id}")
        
        downloads = download_outputs_from_history(prompt_id, output_dir)
        all_downloads.extend(downloads)
    
    return all_downloads

def download_with_preview_conversion(filename, format='webp', quality=90, output_dir="downloads"):
    """
    Download image with format conversion and quality adjustment.
    
    Args:
        filename: Original filename
        format: Target format ('webp', 'jpeg')
        quality: Quality level (1-100)
        output_dir: Output directory
    
    Returns:
        str: Path to converted file
    """
    url = f"{COMFYUI_URL}/view"
    params = {
        'filename': filename,
        'type': 'output',
        'preview': f'{format};{quality}'
    }
    
    response = requests.get(url, params=params)
    
    if response.status_code == 200:
        Path(output_dir).mkdir(parents=True, exist_ok=True)
        
        # Change extension based on format
        base_name = Path(filename).stem
        output_filename = f"{base_name}_preview.{format}"
        output_path = Path(output_dir) / output_filename
        
        with open(output_path, 'wb') as f:
            f.write(response.content)
        
        print(f"✓ Downloaded with conversion: {output_path}")
        return str(output_path)
    else:
        print(f"✗ Conversion failed: {response.status_code}")
        return None

def download_alpha_channel(filename, output_dir="downloads"):
    """
    Download only the alpha channel of an image.
    
    Args:
        filename: Image filename
        output_dir: Output directory
    
    Returns:
        str: Path to alpha channel file
    """
    url = f"{COMFYUI_URL}/view"
    params = {
        'filename': filename,
        'type': 'output',
        'channel': 'a'  # Alpha channel only
    }
    
    response = requests.get(url, params=params)
    
    if response.status_code == 200:
        Path(output_dir).mkdir(parents=True, exist_ok=True)
        
        base_name = Path(filename).stem
        output_filename = f"{base_name}_alpha.png"
        output_path = Path(output_dir) / output_filename
        
        with open(output_path, 'wb') as f:
            f.write(response.content)
        
        print(f"✓ Downloaded alpha channel: {output_path}")
        return str(output_path)
    else:
        print(f"✗ Failed to download alpha: {response.status_code}")
        return None

def wait_for_completion_and_download(prompt_id, timeout=300, poll_interval=2):
    """
    Wait for a workflow to complete and download results.
    
    Args:
        prompt_id: The prompt ID to monitor
        timeout: Maximum time to wait (seconds)
        poll_interval: How often to check (seconds)
    
    Returns:
        list: Downloaded file paths
    """
    print(f"Waiting for prompt {prompt_id} to complete...")
    start_time = time.time()
    
    while (time.time() - start_time) < timeout:
        history = get_history(prompt_id)
        
        if history and prompt_id in history:
            prompt_data = history[prompt_id]
            status = prompt_data.get('status', {})
            
            if status.get('completed', False):
                print(f"✓ Workflow completed!")
                
                # Check for errors
                if status.get('status_str') == 'error':
                    print(f"✗ Workflow failed")
                    messages = status.get('messages', [])
                    for msg in messages:
                        print(f"  Error: {msg}")
                    return []
                
                # Download outputs
                return download_outputs_from_history(prompt_id)
        
        time.sleep(poll_interval)
        print(".", end="", flush=True)
    
    print(f"\n✗ Timeout after {timeout} seconds")
    return []

# Usage Examples

if __name__ == "__main__":
    # Example 1: Download by prompt ID
    print("Example 1: Download specific prompt outputs")
    print("-" * 60)
    prompt_id = "550e8400-e29b-41d4-a716-446655440000"  # Replace with actual ID
    files = download_outputs_from_history(prompt_id, "output/specific")
    print(f"\nDownloaded {len(files)} file(s)")
    
    # Example 2: Download latest outputs
    print("\n\nExample 2: Download latest outputs")
    print("-" * 60)
    latest_files = get_latest_outputs("output/latest", count=3)
    print(f"\nTotal files downloaded: {len(latest_files)}")
    
    # Example 3: Download with format conversion
    print("\n\nExample 3: Download with conversion")
    print("-" * 60)
    # Assumes you have a file named "ComfyUI_00001_.png"
    converted = download_with_preview_conversion(
        "ComfyUI_00001_.png",
        format='webp',
        quality=85,
        output_dir="output/converted"
    )
    
    # Example 4: Extract alpha channel
    print("\n\nExample 4: Extract alpha channel")
    print("-" * 60)
    alpha = download_alpha_channel(
        "ComfyUI_00001_.png",
        output_dir="output/alpha"
    )
```

## Understanding History Structure

The history response has this structure:

```json
{
  "prompt_id_1": {
    "prompt": [queue_number, workflow_data],
    "outputs": {
      "node_id": {
        "images": [
          {
            "filename": "ComfyUI_00001_.png",
            "subfolder": "",
            "type": "output"
          }
        ]
      }
    },
    "status": {
      "status_str": "success",
      "completed": true,
      "messages": []
    }
  }
}
```

## Batch Download Script

Here's a complete script for batch downloading:

```python
#!/usr/bin/env python3
"""
Batch download all images from ComfyUI history.
"""
import argparse
from pathlib import Path

def batch_download(max_items=100, output_dir="batch_output"):
    """Download all images from history."""
    history = get_history(max_items=max_items)
    
    if not history:
        print("No history found")
        return
    
    total_downloads = 0
    total_prompts = len(history)
    
    print(f"Found {total_prompts} workflows in history")
    print(f"Downloading to: {output_dir}")
    print("=" * 60)
    
    for idx, (prompt_id, data) in enumerate(history.items(), 1):
        # Create subdirectory for each prompt
        prompt_dir = Path(output_dir) / f"prompt_{idx}_{prompt_id[:8]}"
        
        print(f"\n[{idx}/{total_prompts}] Processing: {prompt_id}")
        
        files = download_outputs_from_history(prompt_id, str(prompt_dir))
        total_downloads += len(files)
        
        # Save workflow metadata
        metadata_file = prompt_dir / "workflow.json"
        with open(metadata_file, 'w') as f:
            json.dump(data, f, indent=2)
        print(f"  Saved workflow metadata to {metadata_file}")
    
    print(f"\n{'='*60}")
    print(f"✓ Complete! Downloaded {total_downloads} images from {total_prompts} workflows")
    print(f"  Output directory: {Path(output_dir).absolute()}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Batch download ComfyUI outputs")
    parser.add_argument('--max', type=int, default=100, help='Maximum history items')
    parser.add_argument('--output', default='batch_output', help='Output directory')
    
    args = parser.parse_args()
    batch_download(max_items=args.max, output_dir=args.output)
```

## Download Statistics

Track download progress and statistics:

```python
class DownloadStats:
    def __init__(self):
        self.total_files = 0
        self.total_bytes = 0
        self.failed = 0
    
    def add_download(self, file_path):
        if file_path and Path(file_path).exists():
            self.total_files += 1
            self.total_bytes += Path(file_path).stat().st_size
        else:
            self.failed += 1
    
    def report(self):
        mb = self.total_bytes / (1024 * 1024)
        print(f"\n📊 Download Statistics:")
        print(f"  Files downloaded: {self.total_files}")
        print(f"  Total size: {mb:.2f} MB")
        print(f"  Failed: {self.failed}")
        print(f"  Average size: {mb/self.total_files:.2f} MB" if self.total_files > 0 else "  N/A")
```

## Image Verification

Verify downloaded images:

```python
from PIL import Image

def verify_downloaded_image(file_path):
    """
    Verify that a downloaded file is a valid image.
    """
    try:
        img = Image.open(file_path)
        img.verify()
        
        # Reopen for metadata (verify() closes the file)
        img = Image.open(file_path)
        
        print(f"✓ Valid image: {file_path}")
        print(f"  Format: {img.format}")
        print(f"  Size: {img.size}")
        print(f"  Mode: {img.mode}")
        
        return True
    except Exception as e:
        print(f"✗ Invalid image {file_path}: {e}")
        return False
```

## Next Steps

- [Simple workflow execution](./simple-workflow-execution.md)
- [WebSocket monitoring](./websocket-monitoring.md)
- [Queue management](./queue-management.md)

## Related Documentation

- [History API](../API.md#history)
- [View Image API](../API.md#view-image)
- [Image Management](../API.md#images)
