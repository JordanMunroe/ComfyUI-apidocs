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

Workflow prompts submitted to `POST /prompt` are stored in the server's queue and then written to execution history, both of which are readable by other clients (or other users in multi-user mode). Any credentials placed directly inside node inputs are therefore exposed through `GET /queue` and `GET /history`. ComfyUI provides a safe alternative via the `extra_data` field and automatic key stripping.

### What ComfyUI strips automatically

When a prompt is enqueued, ComfyUI removes the following keys from the workflow's node inputs and stores them in a separate, non-public store instead:

| Stripped key | Purpose |
|---|---|
| `auth_token_comfy_org` | Authentication token for Comfy.org API nodes |
| `api_key_comfy_org` | API key for Comfy.org API nodes |

These keys are never exposed through the queue or history endpoints, even if a client submits them inside a node definition.

### Correct pattern — use `extra_data`

Pass secrets inside the top-level `extra_data` object of the `/prompt` request body, not inside node inputs. ComfyUI nodes that need external credentials read them from this field.

**Python**
```python
import requests
import uuid

workflow = { /* ... your node graph ... */ }

response = requests.post(
    "http://127.0.0.1:8188/prompt",
    json={
        "prompt": workflow,
        "client_id": str(uuid.uuid4()),
        "extra_data": {
            "auth_token_comfy_org": "your-comfy-org-token",
            "api_key_comfy_org": "your-api-key",
        },
    },
)
```

**JavaScript (fetch)**
```javascript
const response = await fetch("http://127.0.0.1:8188/prompt", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    prompt: workflow,
    client_id: crypto.randomUUID(),
    extra_data: {
      auth_token_comfy_org: "your-comfy-org-token",
      api_key_comfy_org: "your-api-key",
    },
  }),
});
```

### What NOT to do

Never embed credentials inside a node's `inputs` object:

```json
{
  "prompt": {
    "42": {
      "class_type": "SomeAPINode",
      "inputs": {
        "api_key": "sk-secret-key-here"
      }
    }
  }
}
```

A payload like this stores `sk-secret-key-here` in the queue and history, making it readable by anyone who can call `GET /queue` or `GET /history`.

---

## Token Lifecycle

This section applies to **Authenticated Multi-User Mode** only (`--enable-user-auth`). Single-user and multi-user (header) modes do not issue tokens.

### Token validity

Tokens issued by `POST /api/auth/login` are **session-scoped bearer tokens**. ComfyUI does not currently enforce a rolling expiry clock on tokens, so a token remains valid until one of these events occurs:

- The server process is restarted (all in-memory session state is cleared).
- The token is explicitly revoked via the logout endpoint (see below).
- The user's account is deleted or disabled through the web UI.

Because tokens are long-lived by default, treat them with the same care as passwords: store them in environment variables or a secrets manager, never commit them to source control, and never log them.

### Obtaining a new token (re-authentication)

There is no dedicated token-refresh endpoint. When a token becomes invalid, call `POST /api/auth/login` again with valid credentials to obtain a fresh token.

**Python — automatic re-authentication**
```python
import requests
import uuid


class ComfyClient:
    def __init__(self, base_url: str, username: str, password: str):
        self.base_url = base_url
        self.username = username
        self.password = password
        self.session = requests.Session()
        self._authenticate()

    def _authenticate(self):
        response = self.session.post(
            f"{self.base_url}/api/auth/login",
            json={"username": self.username, "password": self.password},
        )
        response.raise_for_status()
        token = response.json()["token"]
        self.session.headers.update({"Authorization": f"Bearer {token}"})

    def get(self, path: str, **kwargs):
        response = self.session.get(f"{self.base_url}{path}", **kwargs)
        if response.status_code == 401:
            self._authenticate()
            response = self.session.get(f"{self.base_url}{path}", **kwargs)
        response.raise_for_status()
        return response

    def post(self, path: str, **kwargs):
        response = self.session.post(f"{self.base_url}{path}", **kwargs)
        if response.status_code == 401:
            self._authenticate()
            response = self.session.post(f"{self.base_url}{path}", **kwargs)
        response.raise_for_status()
        return response


client = ComfyClient("http://127.0.0.1:8188", "admin", "your_password")
stats = client.get("/system_stats").json()
```

### Revoking a token (logout)

To invalidate a token before a server restart, call the logout endpoint:

**Endpoint:** `POST /api/auth/logout`

**Python**
```python
import requests

headers = {"Authorization": f"Bearer {token}"}
response = requests.post("http://127.0.0.1:8188/api/auth/logout", headers=headers)
# 204 No Content on success — the token is now invalid
```

**JavaScript (fetch)**
```javascript
const response = await fetch("http://127.0.0.1:8188/api/auth/logout", {
  method: "POST",
  headers: { Authorization: `Bearer ${token}` },
});
// 204 No Content on success
```

After logout, any subsequent request that uses the revoked token receives `401 Unauthorized`. The client must log in again to obtain a new token.

### What the server returns for invalid tokens

| Situation | HTTP Status | Recommended action |
|---|---|---|
| No `Authorization` header | `401 Unauthorized` | Add the header with a valid token |
| Malformed header (not `Bearer <token>`) | `401 Unauthorized` | Fix header format |
| Token has been revoked or server restarted | `401 Unauthorized` | Re-authenticate via `POST /api/auth/login` |
| Authenticated but insufficient permissions | `403 Forbidden` | Check user permissions in the web UI |

### Best practices

- **Rotate tokens on a schedule.** Even though tokens do not expire automatically, periodically logging out and re-authenticating limits the window of exposure if a token is compromised.
- **One token per service.** Give each application or automation its own user account so tokens can be revoked independently.
- **Use HTTPS in production.** Without TLS, bearer tokens travel in plaintext. Place ComfyUI behind a TLS-terminating reverse proxy (nginx, Caddy) when the server is reachable from untrusted networks.
- **Environment variables over config files.** Store tokens as environment variables (`COMFY_TOKEN=...`) rather than hard-coding them in source code or configuration files.

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
