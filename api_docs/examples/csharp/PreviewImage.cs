// PreviewImage.cs

using System.Text.Json;

namespace ComfyMinimalExample;

/// <summary>
/// A preview image decoded from a binary WebSocket frame.
/// Type 1 — PREVIEW_IMAGE:         [4B type][4B format (1=JPEG, 2=PNG)][image bytes]
/// Type 4 — PREVIEW_IMAGE_METADATA:[4B type][4B meta len][UTF-8 JSON][image bytes]
/// </summary>
public record PreviewImage(string Extension, byte[] ImageBytes, JsonElement? Metadata);

