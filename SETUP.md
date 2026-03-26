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

### Full Reference

Below is a complete reference of all ComfyUI command-line arguments, sourced directly from `comfy/cli_args.py`.

---

### Networking

#### `--listen [address]`

**Default:** `127.0.0.1`

Specify the IP address(es) to listen on. When provided without an argument, defaults to `0.0.0.0,::` (all IPv4 and IPv6 interfaces). You can provide a comma-separated list of addresses, e.g. `127.2.2.2,127.3.3.3`.

```bash
python main.py --listen           # listens on 0.0.0.0,::
python main.py --listen 0.0.0.0   # all IPv4 interfaces
python main.py --listen 192.168.1.100
```

#### `--port <number>`

**Default:** `8188`

Set the port the server listens on.

```bash
python main.py --port 8080
```

#### `--tls-keyfile <path>` and `--tls-certfile <path>`

Enable TLS (HTTPS). Both flags must be provided together.

```bash
python main.py --tls-keyfile /path/to/key.pem --tls-certfile /path/to/cert.pem
```

#### `--enable-cors-header [origin]`

**Default:** Disabled

Enable CORS headers. The `origin` argument is optional; when omitted it defaults to `*` (allow all origins).

```bash
python main.py --enable-cors-header                      # allow all origins
python main.py --enable-cors-header https://example.com  # restrict to one origin
```

#### `--max-upload-size <MB>`

**Default:** `100`

Set the maximum upload size in MB (affects the `/upload/image` endpoint and the `max_upload_size` server feature flag).

```bash
python main.py --max-upload-size 500
```

---

### Directory and File Paths

#### `--base-directory <path>`

Set the base directory for models, custom_nodes, input, output, temp, and user directories.

#### `--output-directory <path>`

Override the output directory. Takes precedence over `--base-directory`.

#### `--temp-directory <path>`

Override the temp directory. Takes precedence over `--base-directory`.

#### `--input-directory <path>`

Override the input directory. Takes precedence over `--base-directory`.

#### `--user-directory <path>`

Set the user directory with an absolute path. Takes precedence over `--base-directory`.

#### `--extra-model-paths-config <path> [path ...]`

Load one or more `extra_model_paths.yaml` files to add additional model search paths.

#### `--front-end-root <path>`

Use a local directory as the frontend instead of downloading from GitHub. Overrides `--front-end-version`.

---

### Browser and Launch

#### `--auto-launch`

Automatically open ComfyUI in the default browser after starting.

#### `--disable-auto-launch`

Disable automatic browser launch (overrides `--windows-standalone-build` behavior).

#### `--windows-standalone-build`

Enable Windows standalone build mode, which includes auto-launch and other conveniences.

---

### GPU and Device Selection

#### `--cuda-device <device_id>`

Set the CUDA device ID this instance will use. All other devices will be hidden.

#### `--default-device <device_id>`

Set the default device ID. All other devices remain visible.

#### `--directml [device]`

Use torch-directml. Optionally specify the DirectML device index (default: `-1` for the first device).

#### `--oneapi-device-selector <selector>`

Set the oneAPI device selector string for Intel GPU support.

#### `--supports-fp8-compute`

Force ComfyUI to behave as if the device supports fp8 compute.

---

### VRAM Management

These options are mutually exclusive:

| Flag | Description |
|------|-------------|
| `--gpu-only` | Store and run everything on the GPU (text encoders, CLIP, etc.) |
| `--highvram` | Keep models in GPU memory after use instead of unloading to CPU |
| `--normalvram` | Force normal VRAM use (overrides automatic lowvram detection) |
| `--lowvram` | Split the diffusion model to use less VRAM |
| `--novram` | Use when `--lowvram` isn't enough |
| `--cpu` | Use the CPU for all inference (very slow) |

#### `--reserve-vram <GB>`

Reserve the specified amount of VRAM (in GB) for the OS and other software. By default ComfyUI reserves some VRAM depending on the OS.

#### `--async-offload [num_streams]`

Enable async weight offloading. The optional argument controls the number of offload streams (default: `2`). Enabled by default on Nvidia.

#### `--disable-async-offload`

Disable async weight offloading.

#### `--disable-dynamic-vram`

Disable dynamic VRAM allocation and use estimate-based model loading.

#### `--enable-dynamic-vram`

Enable dynamic VRAM on systems where it is not enabled by default.

#### `--disable-smart-memory`

Force aggressive offloading to RAM instead of keeping models in VRAM when possible.

---

### Precision and Data Types

These precision flags apply globally (mutually exclusive):

| Flag | Description |
|------|-------------|
| `--force-fp32` | Force fp32 for all operations |
| `--force-fp16` | Force fp16 for all operations (also sets `--fp16-unet`) |

Diffusion model (UNet) precision (mutually exclusive):

| Flag | Description |
|------|-------------|
| `--fp32-unet` | Run diffusion model in fp32 |
| `--fp64-unet` | Run diffusion model in fp64 |
| `--bf16-unet` | Run diffusion model in bf16 |
| `--fp16-unet` | Run diffusion model in fp16 |
| `--fp8_e4m3fn-unet` | Store UNet weights in fp8_e4m3fn |
| `--fp8_e5m2-unet` | Store UNet weights in fp8_e5m2 |
| `--fp8_e8m0fnu-unet` | Store UNet weights in fp8_e8m0fnu |

VAE precision (mutually exclusive):

| Flag | Description |
|------|-------------|
| `--fp16-vae` | Run VAE in fp16 (may cause black images) |
| `--fp32-vae` | Run VAE in fp32 |
| `--bf16-vae` | Run VAE in bf16 |

#### `--cpu-vae`

Run the VAE on the CPU.

Text encoder precision (mutually exclusive):

| Flag | Description |
|------|-------------|
| `--fp8_e4m3fn-text-enc` | Store text encoder weights in fp8 (e4m3fn) |
| `--fp8_e5m2-text-enc` | Store text encoder weights in fp8 (e5m2) |
| `--fp16-text-enc` | Store text encoder weights in fp16 |
| `--fp32-text-enc` | Store text encoder weights in fp32 |
| `--bf16-text-enc` | Store text encoder weights in bf16 |

#### `--fp16-intermediates`

*(Experimental)* Use fp16 for intermediate tensors between nodes instead of fp32.

#### `--force-channels-last`

Force channels-last memory format when inferencing models.

---

### Attention Optimization

These options are mutually exclusive:

| Flag | Description |
|------|-------------|
| `--use-split-cross-attention` | Split cross attention (ignored when xformers is used) |
| `--use-quad-cross-attention` | Sub-quadratic cross attention (ignored when xformers is used) |
| `--use-pytorch-cross-attention` | PyTorch 2.0 native cross attention |
| `--use-sage-attention` | Sage attention |
| `--use-flash-attention` | FlashAttention |

#### `--disable-xformers`

Disable xformers even if installed.

Attention upcasting (mutually exclusive):

| Flag | Description |
|------|-------------|
| `--force-upcast-attention` | Force attention upcasting (may fix black images) |
| `--dont-upcast-attention` | Disable all attention upcasting |

---

### Caching

These options are mutually exclusive:

| Flag | Description |
|------|-------------|
| `--cache-classic` | Use the old aggressive caching strategy |
| `--cache-lru <N>` | LRU cache with max N node results cached |
| `--cache-none` | No caching; every node re-executes on each run |
| `--cache-ram [threshold_gb]` | RAM pressure caching; removes large cache entries when free RAM drops below threshold (default: `4.0` GB) |

---

### Preview and Sampling

#### `--preview-method <method>`

**Default:** `none`

Set the default latent preview method for sampler nodes. Options: `none`, `auto`, `latent2rgb`, `taesd`.

```bash
python main.py --preview-method latent2rgb
```

#### `--preview-size <pixels>`

**Default:** `512`

Set the maximum preview image size (in pixels) for sampler nodes.

---

### Performance

#### `--fast [feature ...]`

Enable experimental performance optimizations. Without arguments, enables all. With arguments, enables only the specified ones.

Valid features: `fp16_accumulation`, `fp8_matrix_mult`, `cublas_ops`, `autotune`

```bash
python main.py --fast                     # enable all
python main.py --fast fp16_accumulation   # enable only fp16_accumulation
```

⚠️ These optimizations are untested and may reduce image quality or crash ComfyUI.

#### `--deterministic`

Make PyTorch use slower deterministic algorithms where possible. Note this may not make images fully deterministic in all cases.

#### `--force-non-blocking`

Force non-blocking tensor operations. May improve performance on some non-Nvidia systems but can cause issues with some workflows.

#### `--cuda-malloc` / `--disable-cuda-malloc`

Explicitly enable or disable `cudaMallocAsync`. Enabled by default for PyTorch 2.0+.

#### `--disable-pinned-memory`

Disable pinned (page-locked) memory use.

#### `--mmap-torch-files`

Use memory-mapped I/O when loading `.ckpt`/`.pt` files.

#### `--disable-mmap`

Disable memory-mapped I/O when loading `.safetensors` files.

#### `--default-hashing-function <function>`

**Default:** `sha256`

Hash function used for duplicate filename/content comparison. Options: `md5`, `sha1`, `sha256`, `sha512`.

#### `--disable-ipex-optimize`

Disable `ipex.optimize` when loading models with Intel Extension for PyTorch.

---

### Multi-User and Data

#### `--multi-user`

Enable per-user storage. Each user identified by the `comfy-user` HTTP header gets their own settings and `userdata/` directory. The queue and history are still shared.

---

### Custom Nodes and Extensions

#### `--disable-all-custom-nodes`

Prevent loading any custom nodes.

#### `--whitelist-custom-nodes <folder> [folder ...]`

Specify custom node folder names to load even when `--disable-all-custom-nodes` is set.

#### `--disable-api-nodes`

Disable API node types and prevent the frontend from communicating with the internet.

#### `--disable-metadata`

Disable saving workflow/prompt metadata in generated image files.

---

### Manager

#### `--enable-manager`

Enable the ComfyUI-Manager feature.

Manager UI options (mutually exclusive):

| Flag | Description |
|------|-------------|
| `--disable-manager-ui` | Disable only the Manager UI and its endpoints; background tasks still run |
| `--enable-manager-legacy-ui` | Enable the legacy Manager UI |

---

### Frontend

#### `--front-end-version <owner/repo@version>`

**Default:** `comfyanonymous/ComfyUI@latest`

Specify the frontend version to use. Requires internet access to download from GitHub releases. Version can be `latest` or a specific semver string (e.g. `1.0.0`).

```bash
python main.py --front-end-version comfyanonymous/ComfyUI@1.3.0
```

#### `--front-end-root <path>`

Use a local directory as the frontend. Overrides `--front-end-version`.

---

### Logging and Debugging

#### `--verbose [level]`

**Default:** `INFO`

Set the logging level. When provided without argument, defaults to `DEBUG`. Options: `DEBUG`, `INFO`, `WARNING`, `ERROR`, `CRITICAL`.

```bash
python main.py --verbose           # DEBUG level
python main.py --verbose WARNING   # WARNING and above only
```

#### `--log-stdout`

Send normal process output to stdout instead of stderr.

#### `--dont-print-server`

Suppress server startup output.

#### `--quick-test-for-ci`

Enable CI quick test mode.

---

### Networking and API

#### `--comfy-api-base <url>`

**Default:** `https://api.comfy.org`

Set the base URL for the ComfyUI cloud API.

#### `--database-url <url>`

**Default:** `sqlite:///user/comfyui.db`

Specify the database URL. Supports SQLite (default) and other SQLAlchemy-compatible databases.

```bash
python main.py --database-url sqlite:///:memory:
```

#### `--enable-assets`

Enable the assets system (API routes, database synchronization, and background scanning). Exposes `/assets/` endpoints.

#### `--enable-compress-response-body`

Enable gzip compression of response bodies.

---

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

#### Multi-User (Per-User Settings/Data Isolation)
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
