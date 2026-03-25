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
