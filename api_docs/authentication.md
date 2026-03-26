# Authentication Guide

ComfyUI supports two authentication modes. Choose the one that matches your deployment scenario, then follow the steps to configure the server and send requests.

---

## Authentication Modes at a Glance

| Mode | Flag | Requires Password | Best For |
|------|------|-------------------|----------|
| [Single-User (Default)](#single-user-mode-no-authentication) | *(none)* | No | Local development |
| [Multi-User](#multi-user-mode-comfy-user-header) | `--multi-user` | No | Trusted internal networks |

> **Note**: ComfyUI does not have a built-in password or bearer-token authentication system. If you need to restrict who can access the server, place it behind a reverse proxy (e.g., nginx, Caddy) that handles authentication for you.

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

> **Security note**: Any client can send any value in the `comfy-user` header — there is no credential verification. This mode is intentionally trust-based. For environments where you need to restrict access to the server itself, place ComfyUI behind a reverse proxy that handles authentication.

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

## Security Recommendations

| Scenario | Recommended Configuration |
|----------|--------------------------|
| Local development | `python main.py` (default, no flags) |
| Private network, multiple services | `python main.py --multi-user --listen 0.0.0.0` |
| Shared server, restricted access | Place ComfyUI behind a reverse proxy (nginx, Caddy) that handles authentication |
| Public-facing API | Use a reverse proxy with authentication; do not expose ComfyUI directly |

Additional hardening steps for any networked deployment:
- Place ComfyUI behind a reverse proxy (nginx, Caddy) that terminates TLS and enforces authentication.
- Use firewall rules or network-level access controls to limit who can reach the server port.
- Run ComfyUI bound to `127.0.0.1` and let the reverse proxy handle external traffic.

---

## Related Documentation

- [Setup Guide](../SETUP.md) — Full reference for all command-line flags
- [API Overview](./overview.md) — Base URL, CORS, and WebSocket connection details
- [Operations & Administration](./operations.md) — User management endpoints
- [Error Handling](./error_handling.md) — Authentication-related error codes
