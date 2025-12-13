# Multi-User Mode Guide

## Overview

ComfyUI supports multi-user mode, allowing multiple users to work simultaneously on the same server. Each user has their own isolated workspace, queue, and execution context. This guide covers how to use ComfyUI in multi-user mode with practical txt2img and img2img examples.

---

## Table of Contents

1. [Enabling Multi-User Mode](#enabling-multi-user-mode)
2. [User Authentication](#user-authentication)
3. [Client ID Management](#client-id-management)
4. [Text-to-Image in Multi-User Mode](#text-to-image-in-multi-user-mode)
5. [Image-to-Image in Multi-User Mode](#image-to-image-in-multi-user-mode)
6. [User Settings Management](#user-settings-management)
7. [Monitoring User Queues](#monitoring-user-queues)
8. [Best Practices](#best-practices)
9. [Troubleshooting](#troubleshooting)

---

## Enabling Multi-User Mode

### Starting the Server

```bash
# Enable multi-user mode with authentication
python main.py --multi-user

# With custom port
python main.py --multi-user --port 8188

# With listen address for network access
python main.py --multi-user --listen 0.0.0.0
```

### Configuration

Multi-user mode automatically:
- Creates isolated user workspaces
- Manages separate queues per user
- Handles concurrent execution
- Provides user-specific settings

---

## User Authentication

### Creating a User Session

When multi-user mode is enabled, you need to create or retrieve a user ID:

```python
import requests
import json

BASE_URL = "http://127.0.0.1:8188"

def create_user(username: str) -> dict:
    """Create or retrieve a user ID."""
    response = requests.post(
        f"{BASE_URL}/users",
        json={"username": username}
    )
    
    if response.status_code == 200:
        user_data = response.json()
        print(f"User ID: {user_data['user_id']}")
        print(f"Username: {user_data['username']}")
        return user_data
    else:
        raise Exception(f"Failed to create user: {response.text}")

# Example usage
user = create_user("artist_001")
user_id = user['user_id']
```

### Getting User Information

```python
def get_user_info(user_id: str) -> dict:
    """Retrieve user information."""
    response = requests.get(f"{BASE_URL}/users/{user_id}")
    
    if response.status_code == 200:
        return response.json()
    else:
        raise Exception(f"Failed to get user info: {response.text}")

# Example
info = get_user_info(user_id)
print(f"User settings: {info['settings']}")
print(f"Active workflows: {info['active_workflows']}")
```

---

## Client ID Management

Each user should use a unique client ID for WebSocket connections and workflow execution.

```python
import uuid

class MultiUserClient:
    """Client for multi-user ComfyUI operations."""
    
    def __init__(self, base_url: str, username: str):
        self.base_url = base_url
        self.username = username
        self.client_id = str(uuid.uuid4())
        self.user_id = None
        
        # Create or get user
        self._initialize_user()
    
    def _initialize_user(self):
        """Initialize user session."""
        response = requests.post(
            f"{self.base_url}/users",
            json={"username": self.username}
        )
        
        if response.status_code == 200:
            user_data = response.json()
            self.user_id = user_data['user_id']
            print(f"✓ Initialized user: {self.username} (ID: {self.user_id})")
        else:
            raise Exception(f"Failed to initialize user: {response.text}")
    
    def get_headers(self) -> dict:
        """Get headers with user authentication."""
        return {
            "Content-Type": "application/json",
            "X-User-ID": self.user_id,
            "X-Client-ID": self.client_id
        }

# Create client instances for different users
alice = MultiUserClient(BASE_URL, "alice")
bob = MultiUserClient(BASE_URL, "bob")
```

---

## Text-to-Image in Multi-User Mode

### Complete txt2img Example

```python
import requests
import json
import uuid
from typing import Dict, Any

class MultiUserTxt2Img:
    """Text-to-image workflow for multi-user ComfyUI."""
    
    def __init__(self, base_url: str, username: str):
        self.base_url = base_url
        self.username = username
        self.client_id = str(uuid.uuid4())
        self.user_id = self._get_user_id()
    
    def _get_user_id(self) -> str:
        """Get or create user ID."""
        response = requests.post(
            f"{self.base_url}/users",
            json={"username": self.username}
        )
        
        if response.status_code == 200:
            return response.json()['user_id']
        else:
            raise Exception(f"Failed to get user ID: {response.text}")
    
    def create_workflow(
        self,
        prompt: str,
        negative_prompt: str = "",
        width: int = 512,
        height: int = 512,
        steps: int = 20,
        cfg: float = 7.0,
        seed: int = -1,
        model: str = "sd_xl_base_1.0.safetensors"
    ) -> Dict[str, Any]:
        """Create a text-to-image workflow."""
        
        # Use random seed if not specified
        if seed == -1:
            seed = int(uuid.uuid4().int % (2**32))
        
        workflow = {
            "1": {
                "inputs": {
                    "ckpt_name": model
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
                    "width": width,
                    "height": height,
                    "batch_size": 1
                },
                "class_type": "EmptyLatentImage"
            },
            "5": {
                "inputs": {
                    "seed": seed,
                    "steps": steps,
                    "cfg": cfg,
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
                    "filename_prefix": f"{self.username}_txt2img",
                    "images": ["6", 0]
                },
                "class_type": "SaveImage"
            }
        }
        
        return workflow
    
    def execute(
        self,
        prompt: str,
        negative_prompt: str = "",
        **kwargs
    ) -> dict:
        """Execute text-to-image generation."""
        
        # Create workflow
        workflow = self.create_workflow(
            prompt=prompt,
            negative_prompt=negative_prompt,
            **kwargs
        )
        
        # Submit to queue with user authentication
        response = requests.post(
            f"{self.base_url}/prompt",
            headers={
                "Content-Type": "application/json",
                "X-User-ID": self.user_id,
                "X-Client-ID": self.client_id
            },
            json={
                "prompt": workflow,
                "client_id": self.client_id
            }
        )
        
        if response.status_code == 200:
            result = response.json()
            print(f"✓ Workflow queued for user '{self.username}'")
            print(f"  Prompt ID: {result['prompt_id']}")
            print(f"  Queue Position: {result.get('number', 'N/A')}")
            return result
        else:
            raise Exception(f"Failed to execute workflow: {response.text}")

# Example: Multiple users generating images simultaneously
def multi_user_txt2img_example():
    """Example of multiple users generating images."""
    
    # User 1: Alice generates a landscape
    alice = MultiUserTxt2Img(BASE_URL, "alice")
    alice_result = alice.execute(
        prompt="a serene mountain landscape at sunset, 4k, detailed",
        negative_prompt="blurry, low quality",
        width=1024,
        height=768,
        steps=25,
        cfg=7.5
    )
    
    # User 2: Bob generates a portrait
    bob = MultiUserTxt2Img(BASE_URL, "bob")
    bob_result = bob.execute(
        prompt="portrait of a wise old wizard, highly detailed, fantasy art",
        negative_prompt="cartoon, anime, low quality",
        width=768,
        height=1024,
        steps=30,
        cfg=8.0
    )
    
    # User 3: Carol generates abstract art
    carol = MultiUserTxt2Img(BASE_URL, "carol")
    carol_result = carol.execute(
        prompt="abstract geometric patterns, vibrant colors, modern art",
        negative_prompt="realistic, photographic",
        width=512,
        height=512,
        steps=20,
        cfg=6.5
    )
    
    print("\nAll users' workflows submitted successfully!")
    return {
        "alice": alice_result,
        "bob": bob_result,
        "carol": carol_result
    }

# Run the example
if __name__ == "__main__":
    results = multi_user_txt2img_example()
```

---

## Image-to-Image in Multi-User Mode

### Complete img2img Example

```python
import requests
import json
import uuid
from pathlib import Path
from typing import Dict, Any, Optional

class MultiUserImg2Img:
    """Image-to-image workflow for multi-user ComfyUI."""
    
    def __init__(self, base_url: str, username: str):
        self.base_url = base_url
        self.username = username
        self.client_id = str(uuid.uuid4())
        self.user_id = self._get_user_id()
    
    def _get_user_id(self) -> str:
        """Get or create user ID."""
        response = requests.post(
            f"{self.base_url}/users",
            json={"username": self.username}
        )
        
        if response.status_code == 200:
            return response.json()['user_id']
        else:
            raise Exception(f"Failed to get user ID: {response.text}")
    
    def upload_image(
        self,
        image_path: str,
        subfolder: str = "",
        overwrite: bool = False
    ) -> dict:
        """Upload an image for the current user."""
        
        file_path = Path(image_path)
        
        if not file_path.exists():
            raise FileNotFoundError(f"Image not found: {image_path}")
        
        # Prepare multipart form data
        with open(file_path, 'rb') as f:
            files = {
                'image': (file_path.name, f, 'image/png')
            }
            
            data = {
                'subfolder': subfolder,
                'overwrite': str(overwrite).lower()
            }
            
            # Upload with user authentication
            response = requests.post(
                f"{self.base_url}/upload/image",
                headers={
                    "X-User-ID": self.user_id,
                    "X-Client-ID": self.client_id
                },
                files=files,
                data=data
            )
        
        if response.status_code == 200:
            result = response.json()
            print(f"✓ Image uploaded for user '{self.username}'")
            print(f"  Name: {result['name']}")
            print(f"  Subfolder: {result.get('subfolder', 'root')}")
            return result
        else:
            raise Exception(f"Failed to upload image: {response.text}")
    
    def create_workflow(
        self,
        image_name: str,
        prompt: str,
        negative_prompt: str = "",
        denoise_strength: float = 0.75,
        steps: int = 20,
        cfg: float = 7.0,
        seed: int = -1,
        model: str = "sd_xl_base_1.0.safetensors",
        subfolder: str = ""
    ) -> Dict[str, Any]:
        """Create an image-to-image workflow."""
        
        # Use random seed if not specified
        if seed == -1:
            seed = int(uuid.uuid4().int % (2**32))
        
        workflow = {
            "1": {
                "inputs": {
                    "ckpt_name": model
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
                    "image": image_name,
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
                    "seed": seed,
                    "steps": steps,
                    "cfg": cfg,
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
                    "filename_prefix": f"{self.username}_img2img",
                    "images": ["7", 0]
                },
                "class_type": "SaveImage"
            }
        }
        
        return workflow
    
    def execute(
        self,
        image_path: str,
        prompt: str,
        negative_prompt: str = "",
        denoise_strength: float = 0.75,
        **kwargs
    ) -> dict:
        """Execute image-to-image generation."""
        
        # Upload the image
        upload_result = self.upload_image(image_path)
        image_name = upload_result['name']
        subfolder = upload_result.get('subfolder', '')
        
        # Create workflow
        workflow = self.create_workflow(
            image_name=image_name,
            prompt=prompt,
            negative_prompt=negative_prompt,
            denoise_strength=denoise_strength,
            subfolder=subfolder,
            **kwargs
        )
        
        # Submit to queue with user authentication
        response = requests.post(
            f"{self.base_url}/prompt",
            headers={
                "Content-Type": "application/json",
                "X-User-ID": self.user_id,
                "X-Client-ID": self.client_id
            },
            json={
                "prompt": workflow,
                "client_id": self.client_id
            }
        )
        
        if response.status_code == 200:
            result = response.json()
            print(f"✓ img2img workflow queued for user '{self.username}'")
            print(f"  Prompt ID: {result['prompt_id']}")
            print(f"  Denoise Strength: {denoise_strength}")
            return result
        else:
            raise Exception(f"Failed to execute workflow: {response.text}")

# Example: Multiple users doing img2img simultaneously
def multi_user_img2img_example():
    """Example of multiple users processing images."""
    
    # User 1: Alice enhances a landscape
    alice = MultiUserImg2Img(BASE_URL, "alice")
    alice_result = alice.execute(
        image_path="landscape.png",
        prompt="enhance this landscape, make it more vibrant and detailed, 4k",
        negative_prompt="blurry, dull colors",
        denoise_strength=0.6,
        steps=25,
        cfg=7.5
    )
    
    # User 2: Bob stylizes a portrait
    bob = MultiUserImg2Img(BASE_URL, "bob")
    bob_result = bob.execute(
        image_path="portrait.jpg",
        prompt="transform into oil painting style, renaissance art",
        negative_prompt="photographic, modern, digital",
        denoise_strength=0.8,
        steps=30,
        cfg=8.0
    )
    
    # User 3: Carol upscales and enhances
    carol = MultiUserImg2Img(BASE_URL, "carol")
    carol_result = carol.execute(
        image_path="photo.png",
        prompt="highly detailed, sharp, professional photography",
        negative_prompt="blurry, low resolution, artifacts",
        denoise_strength=0.4,
        steps=20,
        cfg=6.5
    )
    
    print("\nAll users' img2img workflows submitted!")
    return {
        "alice": alice_result,
        "bob": bob_result,
        "carol": carol_result
    }

# Run the example
if __name__ == "__main__":
    results = multi_user_img2img_example()
```

---

## User Settings Management

### Managing User-Specific Settings

```python
class UserSettingsManager:
    """Manage user-specific settings in multi-user mode."""
    
    def __init__(self, base_url: str, user_id: str):
        self.base_url = base_url
        self.user_id = user_id
    
    def get_settings(self) -> dict:
        """Get current user settings."""
        response = requests.get(
            f"{self.base_url}/users/{self.user_id}/settings",
            headers={"X-User-ID": self.user_id}
        )
        
        if response.status_code == 200:
            return response.json()
        else:
            raise Exception(f"Failed to get settings: {response.text}")
    
    def update_settings(self, settings: dict) -> dict:
        """Update user settings."""
        response = requests.post(
            f"{self.base_url}/users/{self.user_id}/settings",
            headers={
                "Content-Type": "application/json",
                "X-User-ID": self.user_id
            },
            json=settings
        )
        
        if response.status_code == 200:
            return response.json()
        else:
            raise Exception(f"Failed to update settings: {response.text}")
    
    def set_default_model(self, model_name: str):
        """Set default model for user."""
        settings = {
            "default_model": model_name
        }
        return self.update_settings(settings)
    
    def set_preferred_sampler(self, sampler: str, scheduler: str):
        """Set preferred sampler and scheduler."""
        settings = {
            "preferred_sampler": sampler,
            "preferred_scheduler": scheduler
        }
        return self.update_settings(settings)

# Example usage
alice_settings = UserSettingsManager(BASE_URL, alice.user_id)
alice_settings.set_default_model("sd_xl_base_1.0.safetensors")
alice_settings.set_preferred_sampler("euler_ancestral", "karras")
```

---

## Monitoring User Queues

### Checking User-Specific Queue Status

```python
class UserQueueMonitor:
    """Monitor queue status for a specific user."""
    
    def __init__(self, base_url: str, user_id: str):
        self.base_url = base_url
        self.user_id = user_id
    
    def get_queue(self) -> dict:
        """Get current user's queue status."""
        response = requests.get(
            f"{self.base_url}/queue",
            headers={"X-User-ID": self.user_id}
        )
        
        if response.status_code == 200:
            queue_data = response.json()
            
            # Filter for this user's items (if needed)
            print(f"\n📊 Queue Status for User {self.user_id}")
            print(f"Running: {len(queue_data.get('queue_running', []))}")
            print(f"Pending: {len(queue_data.get('queue_pending', []))}")
            
            return queue_data
        else:
            raise Exception(f"Failed to get queue: {response.text}")
    
    def get_history(self, limit: int = 10) -> dict:
        """Get user's execution history."""
        response = requests.get(
            f"{self.base_url}/history",
            headers={"X-User-ID": self.user_id},
            params={"limit": limit}
        )
        
        if response.status_code == 200:
            return response.json()
        else:
            raise Exception(f"Failed to get history: {response.text}")
    
    def cancel_all(self) -> dict:
        """Cancel all pending workflows for this user."""
        # Get current queue
        queue = self.get_queue()
        
        # Cancel each pending item
        for item in queue.get('queue_pending', []):
            prompt_id = item[1]  # Assuming [number, prompt_id, ...]
            self.cancel_workflow(prompt_id)
        
        print(f"✓ Cancelled all pending workflows for user")
    
    def cancel_workflow(self, prompt_id: str) -> dict:
        """Cancel a specific workflow."""
        response = requests.post(
            f"{self.base_url}/queue",
            headers={
                "Content-Type": "application/json",
                "X-User-ID": self.user_id
            },
            json={
                "delete": [prompt_id]
            }
        )
        
        if response.status_code == 200:
            print(f"✓ Cancelled workflow: {prompt_id}")
            return response.json()
        else:
            raise Exception(f"Failed to cancel workflow: {response.text}")

# Example usage
alice_monitor = UserQueueMonitor(BASE_URL, alice.user_id)
alice_queue = alice_monitor.get_queue()
alice_history = alice_monitor.get_history(limit=5)
```

---

## Best Practices

### 1. Always Use Unique Client IDs

```python
# ✓ Good - Each instance has unique client ID
client1 = MultiUserClient(BASE_URL, "user1")
client2 = MultiUserClient(BASE_URL, "user2")

# ✗ Bad - Reusing client IDs can cause conflicts
shared_client_id = str(uuid.uuid4())
```

### 2. Handle User Authentication Properly

```python
def safe_execute_workflow(client, workflow):
    """Safely execute workflow with proper error handling."""
    try:
        # Verify user session is valid
        user_info = requests.get(
            f"{client.base_url}/users/{client.user_id}",
            headers=client.get_headers()
        )
        
        if user_info.status_code != 200:
            # Re-initialize user if session expired
            client._initialize_user()
        
        # Execute workflow
        return client.execute(workflow)
        
    except Exception as e:
        print(f"✗ Error executing workflow: {e}")
        return None
```

### 3. Use Descriptive Filename Prefixes

```python
# Include username in output filenames
workflow["save_node"] = {
    "inputs": {
        "filename_prefix": f"{username}_{timestamp}_{project_name}",
        "images": ["previous_node", 0]
    },
    "class_type": "SaveImage"
}
```

### 4. Monitor Resource Usage Per User

```python
def get_user_stats(base_url: str, user_id: str) -> dict:
    """Get resource usage statistics for a user."""
    response = requests.get(
        f"{base_url}/users/{user_id}/stats",
        headers={"X-User-ID": user_id}
    )
    
    if response.status_code == 200:
        stats = response.json()
        print(f"\n📈 User Statistics")
        print(f"Total Workflows: {stats.get('total_workflows', 0)}")
        print(f"Active Workflows: {stats.get('active_workflows', 0)}")
        print(f"Images Generated: {stats.get('images_generated', 0)}")
        print(f"Storage Used: {stats.get('storage_mb', 0)} MB")
        return stats
    else:
        return {}
```

### 5. Implement Proper WebSocket Handling

```python
import asyncio
import websockets
import json

async def monitor_user_execution(base_url: str, client_id: str, user_id: str):
    """Monitor execution progress for a specific user."""
    
    ws_url = base_url.replace('http', 'ws') + '/ws'
    
    async with websockets.connect(
        ws_url,
        extra_headers={
            "X-User-ID": user_id,
            "X-Client-ID": client_id
        }
    ) as websocket:
        
        print(f"✓ WebSocket connected for user: {user_id}")
        
        while True:
            try:
                message = await websocket.recv()
                
                # Handle binary messages
                if isinstance(message, bytes):
                    await handle_binary_message(message)
                else:
                    # Handle JSON messages
                    data = json.loads(message)
                    await handle_json_message(data, user_id)
                    
            except websockets.exceptions.ConnectionClosed:
                print("WebSocket connection closed")
                break

async def handle_json_message(data: dict, user_id: str):
    """Handle JSON WebSocket messages."""
    msg_type = data.get('type')
    
    if msg_type == 'executing':
        node = data.get('data', {}).get('node')
        print(f"[{user_id}] Executing node: {node}")
    
    elif msg_type == 'progress':
        value = data.get('data', {}).get('value')
        max_val = data.get('data', {}).get('max')
        print(f"[{user_id}] Progress: {value}/{max_val}")
    
    elif msg_type == 'executed':
        node = data.get('data', {}).get('node')
        print(f"[{user_id}] ✓ Completed node: {node}")
```

---

## Troubleshooting

### Common Issues and Solutions

#### 1. User Session Expired

```python
def refresh_user_session(client):
    """Refresh user session if expired."""
    try:
        # Try to get user info
        response = requests.get(
            f"{client.base_url}/users/{client.user_id}",
            headers=client.get_headers()
        )
        
        if response.status_code == 401:
            print("Session expired, re-authenticating...")
            client._initialize_user()
            return True
        
        return False
        
    except Exception as e:
        print(f"Error checking session: {e}")
        return False
```

#### 2. Queue Conflicts

```python
def check_queue_conflicts(base_url: str, user_id: str):
    """Check for queue conflicts."""
    response = requests.get(
        f"{base_url}/queue",
        headers={"X-User-ID": user_id}
    )
    
    if response.status_code == 200:
        queue = response.json()
        
        # Check if queue is too long
        pending = len(queue.get('queue_pending', []))
        if pending > 10:
            print(f"⚠ Warning: {pending} workflows in queue")
            print("Consider waiting or canceling some workflows")
```

#### 3. Image Upload Fails

```python
def robust_image_upload(client, image_path: str, max_retries: int = 3):
    """Upload image with retry logic."""
    
    for attempt in range(max_retries):
        try:
            return client.upload_image(image_path)
        except Exception as e:
            if attempt < max_retries - 1:
                print(f"Upload failed (attempt {attempt + 1}), retrying...")
                time.sleep(2 ** attempt)  # Exponential backoff
            else:
                raise Exception(f"Failed to upload after {max_retries} attempts: {e}")
```

#### 4. WebSocket Connection Issues

```python
async def robust_websocket_connection(ws_url: str, user_id: str, max_retries: int = 5):
    """Establish WebSocket connection with retries."""
    
    for attempt in range(max_retries):
        try:
            async with websockets.connect(
                ws_url,
                extra_headers={"X-User-ID": user_id},
                ping_interval=30,
                ping_timeout=10
            ) as websocket:
                
                print(f"✓ WebSocket connected (attempt {attempt + 1})")
                return websocket
                
        except Exception as e:
            if attempt < max_retries - 1:
                wait_time = 2 ** attempt
                print(f"Connection failed, retrying in {wait_time}s...")
                await asyncio.sleep(wait_time)
            else:
                raise Exception(f"Failed to connect after {max_retries} attempts: {e}")
```

---

## Complete Multi-User Application Example

Here's a complete example that ties everything together:

```python
import requests
import asyncio
import websockets
import json
import uuid
from typing import Dict, Any, Optional
from pathlib import Path

class ComfyUIMultiUserApp:
    """Complete multi-user ComfyUI application."""
    
    def __init__(self, base_url: str, username: str):
        self.base_url = base_url
        self.username = username
        self.client_id = str(uuid.uuid4())
        self.user_id = None
        self.ws_connection = None
        
        # Initialize user
        self._initialize()
    
    def _initialize(self):
        """Initialize user and settings."""
        # Create/get user
        response = requests.post(
            f"{self.base_url}/users",
            json={"username": self.username}
        )
        
        if response.status_code == 200:
            self.user_id = response.json()['user_id']
            print(f"✓ Initialized user: {self.username}")
        else:
            raise Exception(f"Failed to initialize: {response.text}")
    
    def txt2img(
        self,
        prompt: str,
        negative_prompt: str = "",
        **kwargs
    ) -> str:
        """Generate image from text."""
        
        generator = MultiUserTxt2Img(self.base_url, self.username)
        generator.user_id = self.user_id
        generator.client_id = self.client_id
        
        result = generator.execute(
            prompt=prompt,
            negative_prompt=negative_prompt,
            **kwargs
        )
        
        return result['prompt_id']
    
    def img2img(
        self,
        image_path: str,
        prompt: str,
        **kwargs
    ) -> str:
        """Transform image."""
        
        generator = MultiUserImg2Img(self.base_url, self.username)
        generator.user_id = self.user_id
        generator.client_id = self.client_id
        
        result = generator.execute(
            image_path=image_path,
            prompt=prompt,
            **kwargs
        )
        
        return result['prompt_id']
    
    async def monitor_progress(self):
        """Monitor workflow progress via WebSocket."""
        
        ws_url = self.base_url.replace('http', 'ws') + '/ws'
        
        async with websockets.connect(
            ws_url,
            extra_headers={
                "X-User-ID": self.user_id,
                "X-Client-ID": self.client_id
            }
        ) as websocket:
            
            print(f"✓ Monitoring started for {self.username}")
            
            while True:
                message = await websocket.recv()
                
                if isinstance(message, bytes):
                    # Handle binary preview
                    print(f"[{self.username}] Received preview image")
                else:
                    # Handle JSON
                    data = json.loads(message)
                    self._handle_message(data)
    
    def _handle_message(self, data: dict):
        """Handle WebSocket messages."""
        msg_type = data.get('type')
        
        if msg_type == 'progress':
            progress_data = data.get('data', {})
            value = progress_data.get('value', 0)
            max_val = progress_data.get('max', 100)
            percentage = (value / max_val * 100) if max_val > 0 else 0
            print(f"[{self.username}] Progress: {percentage:.1f}%")
        
        elif msg_type == 'executed':
            print(f"[{self.username}] ✓ Workflow completed!")

# Example: Running multiple users
async def run_multi_user_demo():
    """Demo with multiple users working simultaneously."""
    
    # Create user applications
    alice_app = ComfyUIMultiUserApp(BASE_URL, "alice")
    bob_app = ComfyUIMultiUserApp(BASE_URL, "bob")
    
    # Alice: Generate landscape
    alice_prompt_id = alice_app.txt2img(
        prompt="beautiful mountain landscape, sunset, 4k",
        width=1024,
        height=768
    )
    
    # Bob: Transform his photo
    bob_prompt_id = bob_app.img2img(
        image_path="portrait.jpg",
        prompt="oil painting style, renaissance art"
    )
    
    # Monitor both users' progress
    await asyncio.gather(
        alice_app.monitor_progress(),
        bob_app.monitor_progress()
    )

# Run the demo
if __name__ == "__main__":
    BASE_URL = "http://127.0.0.1:8188"
    asyncio.run(run_multi_user_demo())
```

---

## Summary

Multi-user mode in ComfyUI enables:

- ✅ **Isolated Workspaces**: Each user has their own queue and settings
- ✅ **Concurrent Execution**: Multiple users can generate images simultaneously
- ✅ **User Management**: Track usage, settings, and history per user
- ✅ **Secure Authentication**: User ID-based request authentication
- ✅ **Resource Isolation**: Separate storage and queue management

This guide provides everything you need to build multi-user ComfyUI applications with both text-to-image and image-to-image workflows.

---

**Next Steps:**
- [Simple Workflow Execution](simple-workflow-execution.md) - Basic workflow concepts
- [WebSocket Monitoring](websocket-monitoring.md) - Real-time progress tracking
- [Queue Management](queue-management.md) - Advanced queue control
- [API Reference](../API.md) - Complete API documentation
