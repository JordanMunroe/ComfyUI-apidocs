# ComfyUI API Examples

This directory contains practical code examples for integrating with the ComfyUI API. Each example is self-contained and demonstrates best practices for a specific task.

---

## 🚀 Start Here

### [Minimal API Example](./minimal-api-example.md)

The recommended starting point. A complete, single-file implementation in both **JavaScript** and **C#** that covers:

- Single-user and multi-user authentication modes
- Checking server status
- Building and queuing a text-to-image workflow
- Receiving real-time progress updates and preview images via WebSocket
- Downloading the final generated image

> **Runnable files:**
> - [`javascript/minimal-example.js`](./javascript/minimal-example.js)
> - [`csharp/MinimalExample.cs`](./csharp/MinimalExample.cs)

---

## All Examples

| Example | Description | Key Concepts |
|---------|-------------|--------------|
| **[Minimal API Example](./minimal-api-example.md)** | End-to-end guide covering all core tasks | Status check, queue, WebSocket, download |
| **[Simple Workflow Execution](./simple-workflow-execution.md)** | Deep dive into workflow construction | Node anatomy, references, validation |
| **[WebSocket Monitoring](./websocket-monitoring.md)** | Real-time progress and preview images | Binary frames, event types, progress UI |
| **[Image Upload & img2img](./image-upload-workflow.md)** | Upload images and run img2img pipelines | Multipart upload, VAE encode, inpainting |
| **[Download Generated Images](./download-outputs.md)** | Retrieve and save all generated outputs | History query, batch download |
| **[Queue Management](./queue-management.md)** | Inspect, cancel, and reprioritise queue entries | Queue state, interrupt, delete |

---

## Quick Reference

### Endpoint Summary

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/system_stats` | GET | Check server version and health |
| `/prompt` | POST | Queue a workflow for execution |
| `/queue` | GET | Inspect the current queue |
| `/history` | GET | Retrieve execution history |
| `/history/{prompt_id}` | GET | Retrieve a specific prompt's history |
| `/view` | GET | Download a generated image |
| `/upload/image` | POST | Upload an input image |
| `/interrupt` | POST | Interrupt the currently running prompt |
| `/ws?clientId=…` | WebSocket | Real-time events and preview images |

### Multi-User Mode

When ComfyUI is started with `--multi-user`, add the `comfy-user` header to every request:

```http
comfy-user: alice
```

No password is required — any string value identifies the user. See the [Authentication Guide](../authentication.md) for details.

---

## Learning Path

### 🟢 Beginner
1. Read the [Minimal API Example](./minimal-api-example.md)
2. Run the JavaScript or C# runnable file
3. Modify the prompt and checkpoint name

### 🟡 Intermediate
4. Study [Simple Workflow Execution](./simple-workflow-execution.md) to understand node graphs
5. Implement live previews with [WebSocket Monitoring](./websocket-monitoring.md)
6. Try [Image Upload & img2img](./image-upload-workflow.md)

### 🔴 Advanced
7. Batch workflows with [Queue Management](./queue-management.md)
8. Build custom node graphs programmatically
9. Handle binary preview formats (see [WebSocket Messages](../websocket_messages.md))
