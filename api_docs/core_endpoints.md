# Core API Endpoints

This section covers the primary endpoints for interacting with ComfyUI. These endpoints allow you to execute workflows, manage the execution queue, query available nodes, and access execution history. Understanding these core endpoints is essential for building applications that leverage ComfyUI's powerful workflow execution capabilities.

## Frontend

### Serve Frontend Application

**Endpoint:** `GET /`

**Response:** HTML file (index.html)

**Cache Headers:**
- `Cache-Control: no-cache`
- `Pragma: no-cache`
- `Expires: 0`

**Note:** This endpoint serves the main ComfyUI web interface.

---

## Workflow Execution

Workflow execution is the heart of ComfyUI's functionality. Workflows are defined as directed graphs of nodes, where each node represents an operation (like loading a model, encoding text, or generating an image). When you submit a workflow, it's added to an execution queue and processed node by node. The system automatically handles dependencies, caching, and parallel execution where possible.

### Building Prompts & Workflows

Use these steps to design reliable workflows before you call the queue endpoints:

1. **Clarify the outputs** – List the artifacts you need (e.g., images, embeddings) and which nodes must act as outputs. This keeps the graph focused and prevents unused branches.
2. **Inventory available nodes** – Query `/object_info` to confirm the exact input/output signatures, defaults, and categories for the nodes you plan to use. Align versions between your client and server to avoid mismatched parameters.
3. **Sketch the dependency graph** – On paper or in a diagram tool, lay out how data should flow. Start from required inputs (text prompts, models, control images) and work toward the outputs, ensuring each node has its prerequisites satisfied.
4. **Define stable node IDs** – Assign deterministic string IDs ("1", "text_encoder", etc.) so you can reuse cached results, patch individual nodes, and reference targets in `partial_execution_targets`.
5. **Normalize inputs** – Encode file paths, prompt text, seeds, and control weights consistently. If you must pass secrets (tokens, API keys), rely on the `extra_data` sanitization and never place them inside node definitions.
6. **Stage metadata early** – Fill `extra_data.extra_pnginfo.workflow` (or similar) with the exact workflow graph. This makes downstream auditing and UI inspection easier because the queue/history endpoints echo the metadata.
7. **Test incrementally** – Use `partial_execution_targets` to run only the upstream segments while you debug. Confirm intermediate tensors/images via history outputs before enabling the full graph.
8. **Handle branching outputs** – When multiple outputs are expected, set `outputs_to_execute` on the prompt so you can track which branch produced which artifact, then read the matching entries from `/history`.
9. **Version control prompts** – Store workflow JSON next to your codebase or in a database so changes are reviewable. Include the ComfyUI commit hash or `/system_stats` snapshot for reproducibility.

Once the workflow blueprint is stable, serialize it into the `prompt` map, include any `client_id` required for synchronization with your frontend, and submit via `/prompt`. Example skeleton:

```json
{
  "prompt": {
    "load_checkpoint": {
      "class_type": "CheckpointLoaderSimple",
      "inputs": {
        "ckpt_name": "SDXL.safetensors"
      }
    },
    "text_positive": {
      "class_type": "CLIPTextEncode",
      "inputs": {
        "text": "an ornate glass terrarium, volumetric lighting"
      }
    },
    "k_sampler": {
      "class_type": "KSampler",
      "inputs": {
        "model": ["load_checkpoint", 0],
        "positive": ["text_positive", 0],
        "seed": 123456789,
        "cfg": 7
      }
    }
  },
  "client_id": "frontend-session-42",
  "extra_data": {
    "extra_pnginfo": {
      "workflow": "v1.0-terraruim" 
    }
  }
}
```

This prompt map can be extended with additional branches or updated IDs without rewriting the entire graph, enabling safe iteration and CI-driven regression runs.

### Queue Prompt

Execute a workflow by adding it to the queue.

**Endpoint:** `POST /prompt`

**Request Body:**
```json
{
  "prompt": {
    "1": {
      "inputs": { /* node inputs */ },
      "class_type": "NodeClassName"
    },
    "2": { /* ... */ }
  },
  "client_id": "unique-client-id",
  "extra_data": {
    "extra_pnginfo": { /* workflow metadata */ }
  },
  "front": false,
  "number": 1,
  "prompt_id": "optional-custom-prompt-id",
  "partial_execution_targets": ["node_id1", "node_id2"]
}
```

**Parameters:**
- `prompt` (required): Object containing workflow nodes with their inputs
- `client_id` (optional): Client identifier for tracking execution
- `extra_data` (optional): Additional metadata to store with execution
- `front` (optional): If true, add to front of queue
- `number` (optional): Custom queue number
- `prompt_id` (optional): Custom prompt ID (UUID generated if not provided)
- `partial_execution_targets` (optional): Array of node IDs to execute (partial execution)

**Response (Success - 200):**
```json
{
  "prompt_id": "550e8400-e29b-41d4-a716-446655440000",
  "number": 1,
  "node_errors": {}
}
```

**Note:** Sensitive extra data keys (`auth_token_comfy_org`, `api_key_comfy_org`) are automatically removed from the queue and stored separately for security.

**Response (Error - 400):**
```json
{
  "error": {
    "type": "prompt_error",
    "message": "Invalid node configuration",
    "details": "...",
    "extra_info": {}
  },
  "node_errors": {
    "node_id": {
      "errors": [
        {
          "type": "error_type",
          "message": "error message"
        }
      ],
      "dependent_outputs": ["other_node_id"]
    }
  }
}
```

### Get Current Prompt Status

**Endpoint:** `GET /prompt`

**Response:**
```json
{
  "exec_info": {
    "queue_remaining": 5
  }
}
```

---

## Queue Management

The queue system manages all workflow executions in ComfyUI. It maintains two separate queues: one for currently running workflows and one for pending workflows waiting to execute. Understanding queue management is crucial for building responsive applications, as it allows you to monitor execution status, cancel pending jobs, clear the queue, or interrupt running executions. The queue processes items in order (FIFO by default), but you can prioritize specific items by adding them to the front.

### Get Queue Status

**Endpoint:** `GET /queue`

**Response:**
```json
{
  "queue_running": [
    [1, "prompt_id_1", "workflow_data", { "client_id": "..." }, ["output_nodes"]],
    ...
  ],
  "queue_pending": [
    [2, "prompt_id_2", "workflow_data", { "client_id": "..." }, ["output_nodes"]],
    ...
  ]
}
```

**Note:** Queue items are returned with only the first 5 elements (number, prompt_id, prompt, extra_data, outputs_to_execute). Sensitive data is removed for security.

### Manage Queue

**Endpoint:** `POST /queue`

**Clear Queue:**
```json
{
  "clear": true
}
```

**Delete Specific Items:**
```json
{
  "delete": ["prompt_id_1", "prompt_id_2"]
}
```

**Response:** `200 OK`

### Interrupt Execution

**Endpoint:** `POST /interrupt`

**Request Body (optional):**
```json
{
  "prompt_id": "specific-prompt-id-to-interrupt"
}
```

**Response:** `200 OK`

**Note:** 
- If `prompt_id` is provided, only interrupts that specific prompt if it's currently running.
- If no `prompt_id` is provided, performs a global interrupt of the currently executing prompt.
- If the specified `prompt_id` is not currently running, no interrupt occurs (logged but no error).

### Free Memory

**Endpoint:** `POST /free`

**Request Body:**
```json
{
  "unload_models": true,
  "free_memory": true
}
```

**Parameters:**
- `unload_models` (optional): Unload all models from memory (default: false)
- `free_memory` (optional): Free up system memory (default: false)

**Response:** `200 OK`

**Note:** This endpoint sets flags that are processed by the execution queue. The actual memory freeing happens asynchronously during queue processing.

---

## Node Information

Nodes are the building blocks of ComfyUI workflows. Each node represents a specific operation with defined inputs and outputs. The node information endpoints provide comprehensive metadata about all available nodes, including their input parameters, output types, categories, and documentation. This information is essential for building dynamic workflow editors, validating workflows before execution, or creating node selection interfaces. Node information is generated from the actual Python classes, ensuring it's always accurate and up-to-date.

### Get All Node Information

**Endpoint:** `GET /object_info`

**Response:**
```json
{
  "NodeClassName": {
    "input": {
      "required": {
        "param_name": ["TYPE", { "default": value }]
      },
      "optional": {
        "param_name": ["TYPE"]
      }
    },
    "input_order": {
      "required": ["param1", "param2"],
      "optional": ["param3"]
    },
    "output": ["OUTPUT_TYPE1", "OUTPUT_TYPE2"],
    "output_is_list": [false, false],
    "output_name": ["Output 1", "Output 2"],
    "output_tooltips": ["Description of output 1", "Description of output 2"],
    "name": "NodeClassName",
    "display_name": "Human Readable Name",
    "description": "Node description",
    "category": "category/subcategory",
    "output_node": false,
    "python_module": "nodes",
    "deprecated": false,
    "experimental": false,
    "api_node": false
  }
}
```

**Field Descriptions:**
- `input`: Required and optional input parameters with their types and defaults
- `input_order`: Order of inputs for UI rendering
- `output`: Return types for each output
- `output_is_list`: Whether each output is a list (array) of values
- `output_name`: Human-readable names for outputs
- `output_tooltips`: Descriptions for each output (if provided)
- `name`: Class name of the node
- `display_name`: Display name shown in UI
- `description`: Node description
- `category`: Category path (e.g., "image/transform")
- `output_node`: Whether this is an output/save node
- `python_module`: Python module containing the node
- `deprecated`: Whether the node is deprecated
- `experimental`: Whether the node is experimental
- `api_node`: Whether this is an API-specific node

### Get Specific Node Information

**Endpoint:** `GET /object_info/{node_class}`

**Parameters:**
- `node_class`: The class name of the node

**Response:** Same format as above, but only for the specified node.

---

## History

The history system maintains a record of all completed workflow executions, including their results, outputs, and status. This is invaluable for tracking past generations, retrieving previously created images, debugging failed executions, or implementing undo/redo functionality. History entries include the complete workflow definition, all output artifacts (like generated images), execution status, and any error messages. You can query history by prompt ID, retrieve recent items with pagination, or clear old entries to manage storage.

### Get Execution History

**Endpoint:** `GET /history`

**Query Parameters:**
- `max_items` (optional): Maximum number of items to return
- `offset` (optional): Offset for pagination (default: -1 which means no offset)

**Response:**
```json
{
  "prompt_id_1": {
    "prompt": [ /* queue number */, /* workflow */ ],
    "outputs": {
      "node_id": {
        "images": [
          {
            "filename": "image.png",
            "subfolder": "",
            "type": "output"
          }
        ]
      }
    },
    "status": {
      "status_str": "success",
      "completed": true,
      "messages": []
    }
  }
}
```

**Note:** The response is a dictionary where keys are prompt IDs and values contain execution details.

### Get Specific Prompt History

**Endpoint:** `GET /history/{prompt_id}`

**Parameters:**
- `prompt_id`: The prompt ID to retrieve

**Response:** Same format as above, filtered to the specified prompt.

### Manage History

**Endpoint:** `POST /history`

**Clear All History:**
```json
{
  "clear": true
}
```

**Delete Specific Items:**
```json
{
  "delete": ["prompt_id_1", "prompt_id_2"]
}
```

**Response:** `200 OK`
