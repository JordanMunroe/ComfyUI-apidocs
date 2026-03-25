# Operations and Administration

## User Management

User management enables multi-user ComfyUI installations where different users can have isolated workflows, settings, and data. When running in multi-user mode (enabled with the `--multi-user` flag), each user gets their own namespace for settings, saved workflows, and user data files. This is particularly useful for shared installations, team environments, or when hosting ComfyUI as a service. In single-user mode (default), user management endpoints still exist but operate on a single default user.

**Important:** User management features require the `--multi-user` command-line flag to be fully functional.

### List Users

**Endpoint:** `GET /users`

**Response (Multi-user mode):**
```json
{
  "storage": "server",
  "users": {
    "user_id_1": "Username 1",
    "user_id_2": "Username 2"
  }
}
```

**Response (Single-user mode):**
```json
{
  "storage": "server",
  "migrated": true
}
```

**Example:**
```javascript
const response = await fetch("http://127.0.0.1:8188/users");
const data = await response.json();
console.log("Users:", data.users);
```

### Create User

**Endpoint:** `POST /users`

**Request Body:**
```json
{
  "username": "New User"
}
```

**Response (Success):**
```json
"new_user_id_uuid"
```

**Response (Error - 400):**
```json
{
  "error": "Duplicate username."
}
```

**Note:** Only available in multi-user mode.

**Example:**
```javascript
const response = await fetch("http://127.0.0.1:8188/users", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ username: "New User" }),
});
if (!response.ok) {
  const { error } = await response.json();
  throw new Error(error);
}
const newUserId = await response.json();
console.log("Created user ID:", newUserId);
```

### User Data Management

User data provides persistent storage for each user's custom files, such as saved workflows, presets, configurations, or any other JSON/binary data your application needs to store. This is separate from the global model/image directories and is scoped per user in multi-user mode. The user data API provides a simple file-system-like interface with support for directories, file metadata, moving files, and both text and binary content.

**Use Cases:**
- Saving and loading custom workflows
- Storing user preferences or presets
- Managing project files
- Caching user-specific data

#### List User Files

**Endpoint:** `GET /userdata`

**Query Parameters:**
- `dir` (optional): Subdirectory to list (default: root)
- `recurse` (optional): "true" to recurse subdirectories
- `full_info` (optional): "true" to include file metadata

**Response (basic):**
```json
[
  "file1.json",
  "folder1/"
]
```

**Response (with full_info):**
```json
[
  {
    "path": "file1.json",
    "size": 1024,
    "modified": 1701234567.89,
    "created": 1701234560.00
  }
]
```

#### V2: List User Files (Enhanced)

**Endpoint:** `GET /v2/userdata`

**Query Parameters:**
- `dir` (optional): Subdirectory to list
- `recurse` (optional): "true" to recurse
- `split` (optional): "true" to split folders and files
- `sort_by` (optional): "name", "modified", "created", "size", "type"
- `sort_order` (optional): "asc" or "desc" (default: "asc")

**Response (with split):**
```json
{
  "folders": [
    {
      "path": "folder1",
      "size": 0,
      "modified": 1701234567.89,
      "created": 1701234560.00
    }
  ],
  "files": [
    {
      "path": "file1.json",
      "size": 1024,
      "modified": 1701234567.89,
      "created": 1701234560.00
    }
  ]
}
```

#### Get User File

**Endpoint:** `GET /userdata/{file}`

**Parameters:**
- `file`: File path (URL-encoded if contains special characters)

**Response:** File content

#### Save User File

**Endpoint:** `POST /userdata/{file}`

**Content-Type:** `application/json` or `multipart/form-data`

**JSON Request:**
```json
{
  "any": "json data"
}
```

**Multipart Request:**
- Form field `file`: File to upload
- `overwrite` (optional): "true" to overwrite

**Response:**
```json
{
  "status": "success"
}
```

**Example:**
```javascript
// Save a JSON file to user data
await fetch("http://127.0.0.1:8188/userdata/my-workflow.json", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ nodes: [], links: [] }),
});

// Read the file back
const response = await fetch("http://127.0.0.1:8188/userdata/my-workflow.json");
const data = await response.json();
console.log("Loaded workflow:", data);
```

#### Delete User File

**Endpoint:** `DELETE /userdata/{file}`

**Parameters:**
- `file`: File path to delete

**Response:** `204 No Content`

**Example:**
```javascript
await fetch("http://127.0.0.1:8188/userdata/my-workflow.json", { method: "DELETE" });
```

#### Move User File

**Endpoint:** `POST /userdata/{file}/move/{dest}`

**Parameters:**
- `file`: Source file path
- `dest`: Destination file path

**Response:**
```json
{
  "status": "success"
}
```

---

## Settings

Settings control ComfyUI's behavior and appearance. They're stored per-user (in multi-user mode) or globally (in single-user mode). Settings can include UI preferences, default values, feature toggles, and custom configurations added by extensions or custom nodes. The settings system is schema-based, meaning each setting has a defined type and validation rules. Changes to settings are persisted across sessions.

**Common Setting Types:**
- Boolean flags (enable/disable features)
- String values (paths, IDs, text)
- Number values (timeouts, limits, scales)
- Complex objects (configurations, presets)

### Get All Settings

**Endpoint:** `GET /settings`

**Response:**
```json
{
  "setting_id_1": { /* setting value */ },
  "setting_id_2": { /* setting value */ }
}
```

**Example:**
```javascript
const response = await fetch("http://127.0.0.1:8188/settings");
const settings = await response.json();
console.log("All settings:", settings);
```

### Get Specific Setting

**Endpoint:** `GET /settings/{id}`

**Parameters:**
- `id`: Setting identifier

**Response:**
```json
{
  "value": "setting value"
}
```

**Example:**
```javascript
const response = await fetch("http://127.0.0.1:8188/settings/Comfy.ColorPalette");
const value = await response.json();
console.log("Color palette setting:", value);
```

### Save All Settings

**Endpoint:** `POST /settings`

**Request Body:**
```json
{
  "setting_id_1": { /* setting value */ },
  "setting_id_2": { /* setting value */ }
}
```

**Response:** `200 OK`

**Example:**
```javascript
await fetch("http://127.0.0.1:8188/settings", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ "Comfy.ColorPalette": "dark", "Comfy.UseNewMenu": true }),
});
```

### Save Specific Setting

**Endpoint:** `POST /settings/{id}`

**Parameters:**
- `id`: Setting identifier

**Request Body:**
```json
{
  "value": "new setting value"
}
```

**Response:** `200 OK`

**Example:**
```javascript
await fetch("http://127.0.0.1:8188/settings/Comfy.ColorPalette", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify("dark"),
});
```

---

## System Information

System information endpoints provide visibility into ComfyUI's runtime environment, available resources, and capabilities. This information is essential for monitoring system health, debugging performance issues, understanding version compatibility, and making intelligent decisions about resource-intensive operations. The system stats endpoint is particularly useful for checking available VRAM before queuing heavy workflows or displaying system status in monitoring dashboards.

### Get System Stats

**Endpoint:** `GET /system_stats`

**Response:**
```json
{
  "system": {
    "os": "linux",
    "ram_total": 34359738368,
    "ram_free": 17179869184,
    "comfyui_version": "0.3.76",
    "required_frontend_version": "1.0.0",
    "installed_templates_version": "1.0.0",
    "required_templates_version": "1.0.0",
    "python_version": "3.11.5 (main, Aug 24 2023, 15:09:45) [GCC 11.3.0]",
    "pytorch_version": "2.1.0+cu121",
    "embedded_python": false,
    "argv": ["main.py", "--listen"]
  },
  "devices": [
    {
      "name": "NVIDIA GeForce RTX 4090",
      "type": "cuda",
      "index": 0,
      "vram_total": 25769803776,
      "vram_free": 23622320128,
      "torch_vram_total": 25769803776,
      "torch_vram_free": 23622320128
    }
  ]
}
```

**Notes:**
- RAM and VRAM values are in bytes
- `pytorch_version` includes CUDA version if applicable (e.g., "+cu121" for CUDA 12.1)
- `embedded_python` indicates if using a bundled Python environment
- Multiple devices may be listed if available (e.g., multiple GPUs)

**Example:**
```javascript
const response = await fetch("http://127.0.0.1:8188/system_stats");
const { system, devices } = await response.json();
console.log(`ComfyUI ${system.comfyui_version} on ${system.os}`);
devices.forEach((d) => {
  const vramFreeMB = Math.round(d.vram_free / 1024 / 1024);
  console.log(`${d.name}: ${vramFreeMB} MB VRAM free`);
});
```

### Get Feature Flags

**Endpoint:** `GET /features`

**Response:**
```json
{
  "feature_name": true,
  "another_feature": false
}
```

**Example:**
```javascript
const response = await fetch("http://127.0.0.1:8188/features");
const features = await response.json();
console.log("Enabled features:", Object.keys(features).filter((k) => features[k]));
```

---

## Subgraphs/Templates

Subgraphs (also called templates) are reusable workflow components that encapsulate common patterns or complex node arrangements. They allow you to package a group of nodes as a single reusable unit, similar to functions in programming. Global subgraphs are available system-wide and can be provided by custom nodes or the core system. Workflow templates are pre-built complete workflows that users can load as starting points. These features promote workflow reusability and help users get started quickly.

**Benefits:**
- Reduce complexity by hiding implementation details
- Share common patterns across workflows
- Provide starting points for new users
- Enable modular workflow design

### List Global Subgraphs

**Endpoint:** `GET /global_subgraphs`

**Response:**
```json
[
  {
    "id": "subgraph_id",
    "name": "Subgraph Name",
    "module": "custom_nodes.module_name"
  }
]
```

### Get Subgraph by ID

**Endpoint:** `GET /global_subgraphs/{id}`

**Parameters:**
- `id`: Subgraph identifier

**Response:**
```json
{
  "id": "subgraph_id",
  "name": "Subgraph Name",
  "data": { /* subgraph data */ }
}
```

### List Workflow Templates

**Endpoint:** `GET /workflow_templates`

**Response:**
```json
{
  "custom_node_name": [
    {
      "name": "Template 1",
      "path": "/path/to/template1.json"
    }
  ]
}
```

### Get Internationalization Data

**Endpoint:** `GET /i18n`

**Query Parameters:**
- `language` (optional): Language code (default: "en")

**Response:**
```json
{
  "nodeDefs": {
    "NodeClassName": {
      "name": "Translated Name",
      "description": "Translated description",
      "inputs": {
        "input_name": "Translated input label"
      },
      "outputs": {
        "output_name": "Translated output label"
      }
    }
  },
  "commands": { /* translated commands */ },
  "settings": { /* translated settings */ }
}
```

**Note:** Returns internationalization (i18n) data for the ComfyUI frontend, including node definitions, commands, and settings translations. Custom nodes can provide their own i18n data.

---

## Internal Routes

**Base Path:** `/internal/`

Internal routes are designed specifically for the ComfyUI frontend and internal tooling. These endpoints may change without notice, have different stability guarantees, or expose implementation details not meant for external consumption. While they can be useful for debugging or building tightly integrated tools, production applications should prefer the stable public API endpoints whenever possible.

**⚠️ Warning:** These endpoints are for internal ComfyUI use only and should not be relied upon in external applications. They may change or be removed in future versions without following normal API versioning practices.

**When to Use Internal Routes:**
- Building ComfyUI frontend extensions
- Debugging and development
- Internal tooling and automation
- When explicitly directed by ComfyUI documentation

### Get Logs

**Endpoint:** `GET /internal/logs`

**Response:** Plain text log entries

### Get Raw Logs

**Endpoint:** `GET /internal/logs/raw`

**Response:**
```json
{
  "entries": [
    {
      "t": "2024-01-01 12:00:00",
      "m": "Log message"
    }
  ],
  "size": {
    "cols": 80,
    "rows": 24
  }
}
```

### Subscribe to Logs

**Endpoint:** `PATCH /internal/logs/subscribe`

**Request Body:**
```json
{
  "clientId": "client-uuid",
  "enabled": true
}
```

**Response:** `200 OK`

**Note:** When enabled, log messages will be pushed to the specified client via WebSocket. This is useful for real-time log monitoring in the frontend.

### Get Folder Paths

**Endpoint:** `GET /internal/folder_paths`

**Response:**
```json
{
  "checkpoints": ["/path/to/checkpoints"],
  "vae": ["/path/to/vae"],
  ...
}
```

### List Files in Directory

**Endpoint:** `GET /internal/files/{directory_type}`

**Parameters:**
- `directory_type`: "output", "input", or "temp"

**Response:**
```json
[
  "newest_file.png",
  "older_file.jpg",
  "oldest_file.png"
]
```

**Note:** Files are sorted by modification time (newest first). Returns only files, not subdirectories.
