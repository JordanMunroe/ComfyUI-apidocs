# ComfyUI API Documentation

The ComfyUI API reference is now organized into focused guides so you can jump straight to the workflows, resources, or operational topics you care about. Use the section index below to navigate the documentation set.

## Section Index

1. **[Overview](./overview.md)** — Base URL, authentication modes, WebSocket usage, and real-time preview streaming.
2. **[Authentication](./authentication.md)** — Setup and request examples for the two real modes: single-user (no auth) and multi-user (`--multi-user` with the `comfy-user` header).
3. **[API Endpoints Reference](./endpoints.md)** — Complete index of every HTTP and WebSocket endpoint, organized by category with links to detailed documentation.
4. **[Core API Endpoints](./core_endpoints.md)** — Workflow execution, queue management, node metadata, and history retrieval.
5. **[Resource Management](./resources.md)** — Discover and manage models, embeddings, uploads, previews, and frontend extensions.
6. **[Operations & Administration](./operations.md)** — User management, settings, system stats, subgraphs/templates, and internal routes.
7. **[Error Handling](./error_handling.md)** — Response formats, status codes, and common failure modes.
8. **[Examples](./examples.md)** — JavaScript quick-start snippets plus links to detailed walkthroughs.
9. **[WebSocket Messages](./websocket_messages.md)** — Full catalog of JSON events and binary signals emitted over `/ws`.
10. **[Preview & Output Retrieval](./previews_and_outputs.md)** — Strategies for streaming previews via WebSocket and downloading final artifacts over HTTP.
11. **[Appendix](./appendix.md)** — Best practices, changelog pointers, and support resources.

## Using This Documentation

- Each markdown file remains self-contained, so you can open a single section without scrolling through a monolithic document.
- Relative links continue to work inside and across sections; the Examples page still points to the deeper guides in `api_docs/examples/`.
- When updating content, edit the relevant section file to keep diffs small and reviews simple.

*Last reorganized: December 13, 2025*
