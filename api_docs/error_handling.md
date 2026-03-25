# Error Handling

Proper error handling is crucial for building robust ComfyUI integrations. ComfyUI uses standard HTTP status codes combined with detailed JSON error responses to help you understand and recover from failures. Errors can occur at multiple levels: HTTP transport errors, workflow validation errors, node execution errors, or resource access errors. The API provides structured error information that includes error types, human-readable messages, and contextual details to help with debugging and user feedback.

**Error Handling Strategy:**
1. Check HTTP status code first
2. Parse the error response JSON
3. Look for `node_errors` for validation issues
4. Display user-friendly messages based on error type
5. Log full error details for debugging

## HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 204 | No Content (successful deletion) |
| 400 | Bad Request (invalid parameters or validation error) |
| 401 | Unauthorized (missing or invalid authentication token) |
| 403 | Forbidden (security violation) |
| 404 | Not Found (resource doesn't exist) |
| 500 | Internal Server Error |

## Error Response Format

```json
{
  "error": {
    "type": "error_type",
    "message": "Human readable error message",
    "details": "Detailed error information",
    "extra_info": {}
  },
  "node_errors": {
    "node_id": {
      "errors": [
        {
          "type": "required_input_missing",
          "message": "Input 'param' is required"
        }
      ],
      "dependent_outputs": ["node_2", "node_3"]
    }
  }
}
```

## Common Error Types

- `prompt_error`: Invalid workflow configuration
- `validation_error`: Node validation failed
- `required_input_missing`: Required input not provided
- `invalid_input_type`: Input type mismatch
- `value_not_in_list`: Input value not in allowed list
- `no_prompt`: No prompt provided in request
- `duplicate_username`: Username already exists (user management)
- `invalid_directory_type`: Invalid directory type specified

## Error Handling Example

```javascript
async function queuePrompt(workflow) {
  const response = await fetch("http://127.0.0.1:8188/prompt", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ prompt: workflow, client_id: crypto.randomUUID() }),
  });

  if (response.status === 400) {
    const body = await response.json();
    // Report per-node validation errors
    if (body.node_errors && Object.keys(body.node_errors).length > 0) {
      for (const [nodeId, nodeErr] of Object.entries(body.node_errors)) {
        for (const err of nodeErr.errors) {
          console.error(`Node ${nodeId} – ${err.type}: ${err.message}`);
        }
      }
    } else {
      console.error(`Prompt error: ${body.error?.message}`);
    }
    throw new Error("Workflow validation failed");
  }

  if (response.status === 401) throw new Error("Unauthorized – check your credentials");
  if (response.status === 403) throw new Error("Forbidden – security violation");
  if (!response.ok) throw new Error(`Unexpected error: HTTP ${response.status}`);

  return await response.json(); // { prompt_id, number, node_errors }
}
```
