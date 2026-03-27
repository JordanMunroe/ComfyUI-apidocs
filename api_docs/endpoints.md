# API Endpoints Reference

A complete index of every HTTP endpoint exposed by the ComfyUI server, organized by category. Use this page as a quick lookup when you know the path you need; follow the links in the **Details** column for full parameter and response documentation.

**Base URL:** `http://127.0.0.1:8188` (default)  
**API prefix:** Most endpoints are also reachable at `/api/<path>` for frontend compatibility.

### Authentication key

| Value | Meaning |
|-------|---------|
| **None** | No authentication required. The endpoint is open in both single-user and multi-user mode. |
| **`comfy-user`** | Send a `comfy-user: <user_id>` header when running in multi-user mode (`--multi-user`). The header scopes the response to that user's data. The endpoint still responds in single-user mode without the header. |

ComfyUI has no built-in password or token system. To restrict who can reach the server, place it behind a reverse proxy. See the [Authentication Guide](./authentication.md) for full details.

---

## Frontend

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/` | None | Serve the ComfyUI web interface (index.html) | [Core Endpoints](./core_endpoints.md#frontend) |

---

## Workflow Execution

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `POST` | `/prompt` | None | Queue a workflow for execution | [Core Endpoints](./core_endpoints.md#queue-prompt) |
| `GET` | `/prompt` | None | Get current queue depth (`queue_remaining`) | [Core Endpoints](./core_endpoints.md#get-current-prompt-status) |

---

## Queue Management

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/queue` | None | Get running and pending queue items | [Core Endpoints](./core_endpoints.md#get-queue-status) |
| `POST` | `/queue` | None | Clear the queue or delete specific items | [Core Endpoints](./core_endpoints.md#manage-queue) |
| `POST` | `/interrupt` | None | Interrupt the currently running (or a specific) prompt | [Core Endpoints](./core_endpoints.md#interrupt-execution) |
| `POST` | `/free` | None | Unload models and/or free system memory | [Core Endpoints](./core_endpoints.md#free-memory) |

---

## Node Information

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/object_info` | None | Return metadata for every registered node | [Core Endpoints](./core_endpoints.md#get-all-node-information) |
| `GET` | `/object_info/{node_class}` | None | Return metadata for a single node class | [Core Endpoints](./core_endpoints.md#get-specific-node-information) |

---

## Execution History

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/history` | None | List completed executions (supports `max_items` and `offset`) | [Core Endpoints](./core_endpoints.md#get-execution-history) |
| `GET` | `/history/{prompt_id}` | None | Get the history entry for a specific prompt | [Core Endpoints](./core_endpoints.md#get-specific-prompt-history) |
| `POST` | `/history` | None | Clear all history or delete specific entries | [Core Endpoints](./core_endpoints.md#manage-history) |

---

## Models

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/models` | None | List available model folder types | [Resource Management](./resources.md#list-model-types) |
| `GET` | `/models/{folder}` | None | List model files in a specific folder | [Resource Management](./resources.md#list-models-in-folder) |
| `GET` | `/view_metadata/{folder_name}?filename=` | None | Read safetensors header metadata for a model | [Resource Management](./resources.md#get-model-metadata-safetensors) |
| `GET` | `/experiment/models` | None | *(Experimental)* List model folders with filesystem paths | [Resource Management](./resources.md#experimental-get-model-folders-with-paths) |
| `GET` | `/experiment/models/{folder}` | None | *(Experimental)* List model files with size and timestamps | [Resource Management](./resources.md#experimental-get-model-files-with-details) |
| `GET` | `/experiment/models/preview/{folder}/{path_index}/{filename}` | None | *(Experimental)* Get embedded preview image for a model | [Resource Management](./resources.md#experimental-get-model-preview) |

---

## Embeddings

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/embeddings` | None | List all available textual-inversion embeddings | [Resource Management](./resources.md#list-embeddings) |

---

## Image Upload and Retrieval

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `POST` | `/upload/image` | None | Upload an image to the input directory | [Resource Management](./resources.md#upload-image) |
| `POST` | `/upload/mask` | None | Upload a mask and composite it onto an original image | [Resource Management](./resources.md#upload-mask) |
| `GET` | `/view` | None | Download a generated, input, or temp image (supports on-the-fly conversion) | [Resource Management](./resources.md#view-image) |

---

## Extensions

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/extensions` | None | List JavaScript extension files loaded by the frontend | [Resource Management](./resources.md#extensions) |

---

## User Management

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/users` | None | List users (multi-user mode) or confirm single-user storage | [Operations](./operations.md#list-users) |
| `POST` | `/users` | None | Create a new user (multi-user mode only) | [Operations](./operations.md#create-user) |

---

## User Data

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/userdata` | `comfy-user` | List files in a user data subdirectory | [Operations](./operations.md#list-user-files) |
| `GET` | `/v2/userdata` | `comfy-user` | List user data with directory/file type metadata | [Operations](./operations.md#v2-list-user-files-enhanced) |
| `GET` | `/userdata/{file}` | `comfy-user` | Read a user data file | [Operations](./operations.md#get-user-file) |
| `POST` | `/userdata/{file}` | `comfy-user` | Create or overwrite a user data file | [Operations](./operations.md#save-user-file) |
| `DELETE` | `/userdata/{file}` | `comfy-user` | Delete a user data file | [Operations](./operations.md#delete-user-file) |
| `POST` | `/userdata/{file}/move/{dest}` | `comfy-user` | Move or rename a user data file | [Operations](./operations.md#move-user-file) |

---

## Settings

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/settings` | `comfy-user` | Get all settings as a key/value map | [Operations](./operations.md#get-all-settings) |
| `GET` | `/settings/{id}` | `comfy-user` | Get the value of a single setting | [Operations](./operations.md#get-specific-setting) |
| `POST` | `/settings` | `comfy-user` | Save multiple settings at once | [Operations](./operations.md#save-all-settings) |
| `POST` | `/settings/{id}` | `comfy-user` | Save the value of a single setting | [Operations](./operations.md#save-specific-setting) |

---

## System Information

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/system_stats` | None | Get OS, RAM, VRAM, and version information | [Operations](./operations.md#get-system-stats) |
| `GET` | `/features` | None | Get enabled feature flags | [Operations](./operations.md#get-feature-flags) |

---

## Subgraphs and Templates

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/global_subgraphs` | None | List all globally registered subgraphs | [Operations](./operations.md#list-global-subgraphs) |
| `GET` | `/global_subgraphs/{id}` | None | Get a specific subgraph by ID | [Operations](./operations.md#get-subgraph-by-id) |
| `GET` | `/workflow_templates` | None | List workflow templates provided by custom nodes | [Operations](./operations.md#list-workflow-templates) |
| `GET` | `/i18n` | None | Get frontend internationalization (i18n) data | [Operations](./operations.md#get-internationalization-data) |

---

## WebSocket

| Protocol | Path | Auth | Description | Details |
|----------|------|------|-------------|---------|
| `WS` | `/ws?clientId={id}` | None | Open a real-time event stream for execution updates and preview images | [Overview](./overview.md#websocket-connection) · [WebSocket Messages](./websocket_messages.md) |

---

## Internal Routes

> **⚠️ Unstable:** These endpoints are used internally by the ComfyUI frontend. They may change or be removed in any release without prior notice. Prefer the public endpoints above for external integrations.

| Method | Path | Auth | Description | Details |
|--------|------|------|-------------|---------|
| `GET` | `/internal/logs` | None | Get all log entries as a concatenated string | [Operations](./operations.md#get-logs) |
| `GET` | `/internal/logs/raw` | None | Get structured log entries with timestamps | [Operations](./operations.md#get-raw-logs) |
| `PATCH` | `/internal/logs/subscribe` | None | Subscribe or unsubscribe a client to live log streaming via WebSocket | [Operations](./operations.md#subscribe-to-logs) |
| `GET` | `/internal/folder_paths` | None | Get filesystem paths for each model folder type | [Operations](./operations.md#get-folder-paths) |
| `GET` | `/internal/files/{directory_type}` | None | List files in the output, input, or temp directory (sorted by newest) | [Operations](./operations.md#list-files-in-directory) |

---

## Related Documentation

- **[Overview](./overview.md)** — Base URL, authentication modes, compression, and caching
- **[Authentication](./authentication.md)** — Single-user vs multi-user setup and request examples
- **[Error Handling](./error_handling.md)** — HTTP status codes, error response formats, and common failure modes
- **[WebSocket Messages](./websocket_messages.md)** — Full catalog of JSON events and binary message formats
- **[Preview & Output Retrieval](./previews_and_outputs.md)** — Strategies for streaming previews and downloading outputs
- **[Examples](./examples.md)** — Quick-start code snippets for common workflows
