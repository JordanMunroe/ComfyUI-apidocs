# Appendix

## Best Practices

1. **Use WebSocket for Real-time Updates**: Connect via WebSocket to receive real-time execution updates instead of polling.

2. **Handle Errors Gracefully**: Always check for `node_errors` in responses and handle validation errors before execution.

3. **Clean Up Resources**: Use `/free` endpoint to unload models when switching between different workflows.

4. **Unique Client IDs**: Use unique client IDs (UUIDs) for each client to properly track execution state and receive targeted messages.

5. **Validate Workflows**: Use the validation that happens during `/prompt` POST to catch errors before execution.

6. **Multi-user Considerations**: When running in multi-user mode, always include the `comfy-user` header in requests.

7. **File Path Security**: Never use absolute paths or path traversal patterns (`..`) in file-related endpoints.

8. **Rate Limiting**: Be mindful of queue depth - check queue status before adding many prompts.

9. **Feature Flags**: Exchange feature flags with the server via WebSocket to enable/disable capabilities based on client support.

10. **Binary Messages**: Handle both JSON and binary WebSocket messages for optimal performance, especially for image previews.

11. **Compression**: Include `Accept-Encoding: gzip` header for compressed responses on slower connections.

---

## Changelog

For version history and updates, check the main repository or the `/system_stats` endpoint for current version information.

---

## Support

- **GitHub**: [https://github.com/comfyanonymous/ComfyUI](https://github.com/comfyanonymous/ComfyUI)
- **Discord**: [ComfyUI Discord](https://www.comfy.org/discord)
- **Website**: [https://www.comfy.org/](https://www.comfy.org/)

---

*Last Updated: December 7, 2025*  
*ComfyUI Version: 0.3.76*  
*For the latest updates, check the [ComfyUI GitHub repository](https://github.com/comfyanonymous/ComfyUI)*
