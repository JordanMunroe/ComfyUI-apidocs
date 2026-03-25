# Authentication Guide

ComfyUI supports three authentication modes. Choose the one that matches your deployment scenario, then follow the steps to configure the server and send authenticated requests.

---

## Authentication Modes at a Glance

| Mode | Flag | Requires Password | Best For |
|------|------|-------------------|----------|
| [Single-User (Default)](#single-user-mode-no-authentication) | *(none)* | No | Local development |
| [Multi-User](#multi-user-mode-comfy-user-header) | `--multi-user` | No | Trusted internal networks |
| [Authenticated Multi-User](#authenticated-multi-user-mode-bearer-token) | `--enable-user-auth` | Yes | Production / public-facing APIs |

---

## Single-User Mode (No Authentication)

This is the default when you start ComfyUI without any authentication flags. No credentials are needed; all requests are accepted from any client that can reach the server.

### Setup

```bash
# Local access only (safest — reachable only from the same machine)
python main.py

# Reachable from other machines on your network (use with caution)
python main.py --listen 0.0.0.0
```

> **Security note**: When listening on `0.0.0.0` without authentication, anyone on your network can submit workflows. Restrict network access or add a firewall rule when exposing the server beyond localhost.

### Sending Requests

No special headers or tokens are required.

**Python**
```python
import requests

response = requests.get("http://127.0.0.1:8188/system_stats")
print(response.json())
```

**JavaScript (fetch)**
```javascript
const response = await fetch("http://127.0.0.1:8188/system_stats");
const data = await response.json();
console.log(data);
```

---

## Multi-User Mode (`comfy-user` Header)

Multi-user mode isolates each user's queue, history, and settings without requiring passwords. Clients identify themselves by including a `comfy-user` header. Because there is no credential verification, this mode is suited for **trusted environments** (private networks, internal services) where you want separation but not full authentication.

### Setup

```bash
# Enable multi-user mode
python main.py --multi-user

# With network access (trusted network only)
python main.py --multi-user --listen 0.0.0.0
```

### Sending Requests

Include the `comfy-user` header with the user's identifier on every request.

**Python**
```python
import requests
import uuid

# Use a fixed string or generate a unique ID per user/service
user_id = "alice"  # or str(uuid.uuid4()) for a random ID

headers = {"comfy-user": user_id}

# Submit a workflow
response = requests.post(
    "http://127.0.0.1:8188/prompt",
    json={"prompt": workflow, "client_id": str(uuid.uuid4())},
    headers=headers,
)
print(response.json())

# Check this user's queue
queue = requests.get("http://127.0.0.1:8188/queue", headers=headers)
print(queue.json())
```

**JavaScript (fetch)**
```javascript
const userId = "alice"; // or crypto.randomUUID()
const headers = { "comfy-user": userId, "Content-Type": "application/json" };

// Submit a workflow
const response = await fetch("http://127.0.0.1:8188/prompt", {
  method: "POST",
  headers,
  body: JSON.stringify({ prompt: workflow, client_id: crypto.randomUUID() }),
});
console.log(await response.json());

// Check this user's queue
const queue = await fetch("http://127.0.0.1:8188/queue", { headers });
console.log(await queue.json());
```

> **Security note**: Any client can send any value in the `comfy-user` header. If users must not be able to impersonate each other, use [Authenticated Multi-User Mode](#authenticated-multi-user-mode-bearer-token) instead.

---

## Authenticated Multi-User Mode (Bearer Token)

This mode adds password-based login on top of multi-user isolation. Clients must first obtain a token by logging in, then include that token as a Bearer credential on subsequent requests. Use this for production deployments or any scenario where you need to control who can access the server.

### Setup

```bash
# Enable authenticated multi-user mode
python main.py --enable-user-auth

# Production example: public-facing server with CORS support
python main.py --listen 0.0.0.0 --enable-user-auth --enable-cors-header
```

On first launch, ComfyUI creates a default admin account:

| Field | Value |
|-------|-------|
| Username | `admin` |
| Password | `admin` |

> **Important**: Change the default admin password immediately after first login.

### Step 1 — Log In to Obtain a Token

**Endpoint:** `POST /api/auth/login`

**Python**
```python
import requests

response = requests.post(
    "http://127.0.0.1:8188/api/auth/login",
    json={"username": "admin", "password": "your_password"},
)
response.raise_for_status()
token = response.json()["token"]
print(f"Token: {token}")
```

**JavaScript (fetch)**
```javascript
const response = await fetch("http://127.0.0.1:8188/api/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ username: "admin", password: "your_password" }),
});
const { token } = await response.json();
console.log("Token:", token);
```

### Step 2 — Use the Token in API Requests

Include the token as a `Bearer` credential in the `Authorization` header on every subsequent request.

**Python**
```python
import requests
import uuid

headers = {"Authorization": f"Bearer {token}"}

# Submit a workflow
response = requests.post(
    "http://127.0.0.1:8188/prompt",
    json={"prompt": workflow, "client_id": str(uuid.uuid4())},
    headers=headers,
)
print(response.json())

# Retrieve queue status
queue = requests.get("http://127.0.0.1:8188/queue", headers=headers)
print(queue.json())
```

**JavaScript (fetch)**
```javascript
const headers = {
  Authorization: `Bearer ${token}`,
  "Content-Type": "application/json",
};

// Submit a workflow
const response = await fetch("http://127.0.0.1:8188/prompt", {
  method: "POST",
  headers,
  body: JSON.stringify({ prompt: workflow, client_id: crypto.randomUUID() }),
});
console.log(await response.json());

// Retrieve queue status
const queue = await fetch("http://127.0.0.1:8188/queue", { headers });
console.log(await queue.json());
```

### Complete Python Example

```python
import requests
import uuid


def create_client(base_url: str, username: str, password: str) -> requests.Session:
    """Log in and return an authenticated session."""
    session = requests.Session()

    response = session.post(
        f"{base_url}/api/auth/login",
        json={"username": username, "password": password},
    )
    response.raise_for_status()
    token = response.json()["token"]
    session.headers.update({"Authorization": f"Bearer {token}"})
    return session


BASE_URL = "http://127.0.0.1:8188"
session = create_client(BASE_URL, "admin", "your_password")

# All subsequent requests use the token automatically
stats = session.get(f"{BASE_URL}/system_stats").json()
print("System stats:", stats)

result = session.post(
    f"{BASE_URL}/prompt",
    json={"prompt": workflow, "client_id": str(uuid.uuid4())},
).json()
print("Queued prompt ID:", result["prompt_id"])
```

### Managing Users

After enabling authentication, use the ComfyUI web interface to:
- Create additional user accounts
- Set user permissions
- Reset passwords
- View user activity

---

## Secrets in Workflow Nodes

Regardless of authentication mode, never embed API keys or credentials directly inside node definitions submitted to the `/prompt` endpoint. ComfyUI automatically strips `auth_token_comfy_org` and `api_key_comfy_org` fields from queued prompts and stores them separately. Place any secrets in the `extra_data` field of the prompt request body, not inside node inputs.

---

## Security Recommendations

| Scenario | Recommended Configuration |
|----------|--------------------------|
| Local development | `python main.py` (default, no flags) |
| Private network, multiple services | `python main.py --multi-user --listen 0.0.0.0` |
| Shared server, controlled access | `python main.py --enable-user-auth --listen 192.168.1.100` |
| Public-facing API | `python main.py --enable-user-auth --listen 0.0.0.0 --enable-cors-header` |

Additional hardening steps for production deployments:
- Place ComfyUI behind a reverse proxy (nginx, Caddy) that terminates TLS so tokens are encrypted in transit.
- Use `--enable-origin-check-only` to reject requests from unexpected origins.
- Rotate tokens regularly and revoke them when a user should lose access.

---

## Related Documentation

- [Setup Guide](../SETUP.md) — Full reference for all command-line flags
- [API Overview](./overview.md) — Base URL, CORS, and WebSocket connection details
- [Operations & Administration](./operations.md) — User management endpoints
- [Error Handling](./error_handling.md) — Authentication-related error codes
