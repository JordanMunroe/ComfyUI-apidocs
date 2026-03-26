// PreviewImage.cs
// Represents a binary preview image decoded from a WebSocket message.

using System.Text.Json;

namespace ComfyMinimalExample;

/// <summary>
/// Represents a preview image decoded from a binary WebSocket message sent
/// by the ComfyUI server during workflow execution.
///
/// ComfyUI supports two binary event formats:
/// <list type="bullet">
///   <item><description>
///     Type 1 — PREVIEW_IMAGE:
///     <c>[4B event type][4B image format (1=JPEG, 2=PNG)][image bytes]</c>
///   </description></item>
///   <item><description>
///     Type 4 — PREVIEW_IMAGE_WITH_METADATA:
///     <c>[4B event type][4B metadata length][UTF-8 JSON metadata][image bytes]</c>
///   </description></item>
/// </list>
/// </summary>
/// <param name="Extension">
/// File extension derived from the image format: <c>"jpg"</c> or <c>"png"</c>.
/// </param>
/// <param name="ImageBytes">Raw image bytes ready to write to disk.</param>
/// <param name="Metadata">
/// Optional JSON metadata element present only in Type 4 messages.
/// May include fields such as <c>node_id</c>, <c>prompt_id</c>, and
/// <c>image_type</c>.
/// </param>
public record PreviewImage(string Extension, byte[] ImageBytes, JsonElement? Metadata);
