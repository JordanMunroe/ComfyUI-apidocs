# ComfyUI Setup Guide

Configuration guide for using ComfyUI as an API server.

> **Note**: This guide assumes you have already installed ComfyUI. For installation instructions, visit the [official ComfyUI repository](https://github.com/comfyanonymous/ComfyUI).

## 📋 Table of Contents

- [Starting the API Server](#starting-the-api-server)
- [Command Line Arguments](#command-line-arguments)
- [Verifying API Access](#verifying-api-access)
- [Troubleshooting](#troubleshooting)

---

## Starting the API Server

Start ComfyUI from your installation directory:

```bash
# Basic start (local access only)
python main.py

# Server will be available at: http://127.0.0.1:8188
```

---

## Command Line Arguments

### Basic Usage

```bash
# Start server (local access only)
python main.py

# Start server for remote access
python main.py --listen 0.0.0.0

# Combine multiple options
python main.py --listen 0.0.0.0 --port 8080 --enable-cors-header --multi-user
```

### Detailed Arguments

#### `--listen <address>`

**Purpose**: Control which network interfaces the server listens on.

**Default**: `127.0.0.1` (localhost only)

**Usage**:
```bash
# Local access only (default)
python main.py --listen 127.0.0.1

# Allow connections from any network interface
python main.py --listen 0.0.0.0

# Specific IP address
python main.py --listen 192.168.1.100
```

**When to use**:
- **Local only** (`127.0.0.1`): Testing on same machine, maximum security
- **All interfaces** (`0.0.0.0`): API needs to be accessible from other machines/containers/network
- **Specific IP**: Multiple network interfaces, bind to specific one

**Security Note**: Using `0.0.0.0` exposes your API to your entire network. Consider placing ComfyUI behind a reverse proxy that handles authentication, or use `--multi-user` to enable per-user storage isolation.

---

#### `--port <number>`

**Purpose**: Change the port the server listens on.

**Default**: `8188`

**Usage**:
```bash
# Use custom port
python main.py --port 8080
python main.py --port 3000
```

**When to use**:
- Port 8188 is already in use by another application
- Company/network policy requires specific ports
- Running multiple ComfyUI instances on same machine

**Example** - Multiple instances:
```bash
# Instance 1
python main.py --port 8188

# Instance 2 (different terminal/directory)
python main.py --port 8189
```

---

#### `--enable-cors-header`

**Purpose**: Enable Cross-Origin Resource Sharing (CORS) headers.

**Default**: Disabled

**Usage**:
```bash
python main.py --enable-cors-header
```

**When to use**:
- Building a web application that calls ComfyUI API from browser
- Frontend and ComfyUI are on different domains/ports
- Using JavaScript fetch/axios from a webpage

**Example scenario**:
```javascript
// Web app at http://localhost:3000
// ComfyUI at http://localhost:8188

// Without --enable-cors-header: CORS error ❌
// With --enable-cors-header: Works ✓

fetch('http://localhost:8188/system_stats')
  .then(response => response.json())
  .then(data => console.log(data));
```

**Not needed when**:
- Making direct API calls from Python/Node.js scripts (server-side)
- Using same-origin requests (same domain and port)

---

#### `--multi-user`

**Purpose**: Enable per-user storage and isolation **without requiring passwords**.

**Default**: Disabled (single user mode)

**Usage**:
```bash
python main.py --multi-user
```

**What it does**:
1. **Separates user data** - Each user ID gets their own:
   - Settings and preferences
   - User data files (stored in the `userdata/` directory per user)
2. **Shared resources** - The queue, history, and model execution are **shared** across all users
3. **No authentication** - Users identify themselves via `comfy-user` header
4. **Simple isolation** - Perfect for trusted environments where you need per-user settings without password complexity

**API usage with multi-user**:

```python
import requests
import uuid

# Each client can use their own user ID
user_id = "user_alice"  # or generate: str(uuid.uuid4())

# Include user ID in headers
headers = {
    'comfy-user': user_id
}

# Settings and userdata requests will be scoped to this user
response = requests.post(
    'http://127.0.0.1:8188/prompt',
    json={'prompt': workflow, 'client_id': str(uuid.uuid4())},
    headers=headers
)

# Note: queue and history are shared across all users
response = requests.get(
    'http://127.0.0.1:8188/queue',
    headers=headers
)
```

**When to use**:
- **Multiple services/projects**: Different applications using same ComfyUI that need separate settings or user data files
- **Development**: Separate per-user settings between team members
- **Simple isolation**: Need per-user settings/data without authentication overhead
- **Trusted environment**: Internal network where security isn't a primary concern

**When NOT to use**:
- **Public access**: Anyone can claim any user ID (no credential verification)
- **Untrusted users**: Use a reverse proxy with authentication for password protection

> **Note**: ComfyUI does not have a built-in password or bearer-token authentication system. `--multi-user` provides user isolation only — it does not prevent a client from claiming any user ID. If you need to restrict who can access the server, place ComfyUI behind a reverse proxy that handles authentication.

---

### Common Configurations

#### Local Development
```bash
# No auth, local only, default port
python main.py
```

#### Remote Access (Private Network)
```bash
# Accessible from other machines on your network
python main.py --listen 0.0.0.0
```

#### Multi-User (Isolated Queues/History per User)
```bash
# Multiple users/services with per-user storage isolation
python main.py --multi-user
```

#### Web Application Development
```bash
# Enable CORS for browser-based apps
python main.py --listen 0.0.0.0 --enable-cors-header
```

#### Multiple Services (Same Instance)
```bash
# Multiple microservices sharing one ComfyUI
python main.py --multi-user --listen 0.0.0.0

# Each service uses its own user ID:
# Service A: headers={'comfy-user': 'service-a'}
# Service B: headers={'comfy-user': 'service-b'}
```

#### Multiple Instances (Different Ports)
```bash
# Instance 1: Main
python main.py --port 8188

# Instance 2: Testing
python main.py --port 8189 --listen 127.0.0.1
```

---

## Verifying API Access

### 1. Test API Connection

```python
import requests

# Test connection
response = requests.get("http://127.0.0.1:8188/system_stats")
if response.status_code == 200:
    print("✓ API is working!")
    print(response.json())
else:
    print("✗ API not responding")
```

### 2. Test GPU Detection

```python
import requests

response = requests.get("http://127.0.0.1:8188/system_stats")
if response.status_code == 200:
    stats = response.json()
    devices = stats.get('devices', [])
    if devices:
        print(f"✓ GPU detected: {devices[0].get('name', 'Unknown')}")
        print(f"  VRAM: {devices[0].get('vram_total', 0) / 1024**3:.2f} GB")
    else:
        print("⚠ Running on CPU mode")
```

### 3. Test Model Detection

```python
import requests

response = requests.get("http://127.0.0.1:8188/object_info")
if response.status_code == 200:
    info = response.json()
    if "CheckpointLoaderSimple" in info:
        checkpoints = info["CheckpointLoaderSimple"]["input"]["required"]["ckpt_name"][0]
        print(f"✓ Found {len(checkpoints)} checkpoint(s):")
        for ckpt in checkpoints:
            print(f"  - {ckpt}")
    else:
        print("⚠ No models found")
```

---

## Troubleshooting

### Common Issues

#### 1. Server won't start

**Error**: `Address already in use`
```bash
# Find process using port 8188
# Windows:
netstat -ano | findstr :8188
taskkill /PID <PID> /F

# Linux/macOS:
lsof -i :8188
kill -9 <PID>

# Or use a different port:
python main.py --port 8080
```

#### 2. Out of Memory (CUDA OOM)

**Error**: `CUDA out of memory`

**Solution**:
Check the [official ComfyUI documentation](https://github.com/comfyanonymous/ComfyUI) for memory optimization flags.

#### 3. Slow generation

If generation is slow, check GPU is being used via the system stats API:
```python
import requests
response = requests.get("http://127.0.0.1:8188/system_stats")
devices = response.json().get('devices', [])
print(devices)
```

#### 4. Connection refused from remote machine (API Access)

**Error**: Can't connect to API from another computer

**Solutions**:
```bash
# Start server with listen flag (REQUIRED for remote API access)
python main.py --listen 0.0.0.0

# Check firewall settings (Windows):
New-NetFirewallRule -DisplayName "ComfyUI" -Direction Inbound -LocalPort 8188 -Protocol TCP -Action Allow

# Check firewall (Linux):
sudo ufw allow 8188/tcp
```

#### 5. WebSocket connection issues (API Monitoring)

**Error**: WebSocket won't connect for progress monitoring

**Solutions**:
1. Enable CORS if connecting from web app:
   ```bash
   python main.py --enable-cors-header
   ```
2. Verify WebSocket URL format: `ws://127.0.0.1:8188/ws?clientId=<your_client_id>`
3. Check browser console for errors
4. Ensure server is listening on correct interface (`--listen` flag)

---

## Next Steps

After configuration:

1. **[Read the Authentication Guide](api_docs/authentication.md)** - Detailed guide for all authentication modes
2. **[Read the Quick Start Guide](QUICKSTART.md)** - Run your first workflow via API
3. **[Explore API Documentation](api_docs/API.md)** - Learn all endpoints
4. **[Try Examples](api_docs/examples/README.md)** - Working code samples

---

**Ready to start?** → [Quick Start Guide](QUICKSTART.md)
