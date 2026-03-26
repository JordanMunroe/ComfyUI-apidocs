// ImageDescriptor.cs
// Describes a generated image file on the ComfyUI server.

namespace ComfyMinimalExample;

/// <summary>
/// Describes a generated image file returned in node execution outputs.
///
/// This information is passed directly to
/// <see cref="ComfyClient.DownloadImageAsync"/> to retrieve the image from
/// the <c>GET /view</c> endpoint.
/// </summary>
/// <param name="Filename">
/// Filename on the ComfyUI server (e.g. <c>minimal_example_00001_.png</c>).
/// </param>
/// <param name="Subfolder">
/// Subdirectory under ComfyUI's <c>output/</c> root. Often an empty string.
/// </param>
/// <param name="Type">
/// Storage area: <c>"output"</c> for generated images, <c>"input"</c> for
/// previously uploaded images.
/// </param>
public record ImageDescriptor(string Filename, string Subfolder, string Type);
