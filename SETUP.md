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
python main.py --listen 0.0.0.0 --port 8080 --enable-cors-header --enable-user-auth
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

**Security Note**: Using `0.0.0.0` exposes your API to your entire network. Consider using `--enable-user-auth` for security.

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
   - Queue (executions run independently)
   - History (isolated generation history)
   - Settings and preferences
2. **No authentication** - Users identify themselves via `comfy-user` header
3. **Simple isolation** - Perfect for trusted environments where you need separation without password complexity

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

# All API calls with this header will be isolated to this user
response = requests.post(
    'http://127.0.0.1:8188/prompt',
    json={'prompt': workflow, 'client_id': str(uuid.uuid4())},
    headers=headers
)

# Check this user's queue
response = requests.get(
    'http://127.0.0.1:8188/queue',
    headers=headers
)
```

**When to use**:
- **Multiple services/projects**: Different applications using same ComfyUI without interfering
- **Development**: Separate testing from production workflows
- **Simple isolation**: Need user separation without authentication overhead
- **Trusted environment**: Internal network where security isn't a primary concern

**When NOT to use**:
- **Public access**: Anyone can claim any user ID (no security)
- **Untrusted users**: Use `--enable-user-auth` instead for password protection

---

#### `--enable-user-auth`

**Purpose**: Enable user authentication and multi-user mode **with password protection**.

**Default**: Disabled (no authentication required)

**Usage**:
```bash
python main.py --enable-user-auth
```

**What it does**:
1. **Creates user system** - Separate user accounts with passwords
2. **Isolates workflows** - Each user has their own:
   - Queue (executions run independently)
   - History (can't see other users' generations)
   - Settings and preferences
3. **Requires login** - API requests must include authentication token
4. **Admin interface** - User management via web UI
5. **Automatically enables** `--multi-user` with security

**First-time setup**:
```bash
# Start with auth enabled
python main.py --enable-user-auth

# On first launch, default admin account is created:
# Username: admin
# Password: admin

# IMPORTANT: Change the default password immediately!
```

**API usage with authentication**:

1. **Login to get token**:
```python
import requests

# Login
response = requests.post('http://127.0.0.1:8188/api/auth/login', json={
    'username': 'admin',
    'password': 'your_password'
})

token = response.json()['token']
```

2. **Use token in API requests**:
```python
# Include token in headers
headers = {
    'Authorization': f'Bearer {token}'
}

# All API calls need the token
response = requests.post(
    'http://127.0.0.1:8188/prompt',
    json={'prompt': workflow},
    headers=headers
)
```

**Managing users**:

After enabling auth, access the web UI to:
- Create new user accounts
- Set permissions
- Reset passwords
- View user activity

**When to use**:
- **Multiple users**: Different people/services using same ComfyUI instance
- **Production deployment**: Securing your API from unauthorized access
- **Shared servers**: Running ComfyUI on a shared machine/network
- **Isolation needed**: Different projects/clients shouldn't interfere with each other

**When NOT to use**:
- Personal use on local machine (adds complexity)
- Already behind authentication layer (reverse proxy with auth)
- Testing/development on isolated network (use `--multi-user` instead)

**Comparison: `--multi-user` vs `--enable-user-auth`**:

| Feature | `--multi-user` | `--enable-user-auth` |
|---------|----------------|----------------------|
| User isolation | ✅ Yes | ✅ Yes |
| Password required | ❌ No | ✅ Yes |
| Security | ⚠️ Trust-based | ✅ Authenticated |
| Setup complexity | Simple | Requires user mgmt |
| Best for | Internal/dev | Production/public |

**Security considerations**:
```bash
# ✓ Safe: Local only, no auth needed
python main.py --listen 127.0.0.1

# ✓ Good: Multi-user on private network (no passwords)
python main.py --listen 0.0.0.0 --multi-user

# ✓ Good: Auth + limited network access
python main.py --listen 192.168.1.100 --enable-user-auth

# ✓ Best: Auth for public-facing API
python main.py --listen 0.0.0.0 --enable-user-auth

# ⚠ Warning: No isolation, exposed to network
python main.py --listen 0.0.0.0
```

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

#### Multi-User (No Passwords)
```bash
# Multiple users/services with simple isolation
python main.py --multi-user
```

#### Remote Access (Secured)
```bash
# With authentication for security
python main.py --listen 0.0.0.0 --enable-user-auth
```

#### Web Application Development
```bash
# Enable CORS for browser-based apps
python main.py --listen 0.0.0.0 --enable-cors-header
```

#### Production Deployment
```bash
# Full security setup
python main.py --listen 0.0.0.0 --port 8080 --enable-cors-header --enable-user-auth
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
# Instance 1: Main production
python main.py --port 8188 --enable-user-auth

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

1. **[Read the Quick Start Guide](QUICKSTART.md)** - Run your first workflow via API
2. **[Explore API Documentation](api_docs/API.md)** - Learn all endpoints
3. **[Try Examples](api_docs/examples/README.md)** - Working code samples

---

**Ready to start?** → [Quick Start Guide](QUICKSTART.md)
