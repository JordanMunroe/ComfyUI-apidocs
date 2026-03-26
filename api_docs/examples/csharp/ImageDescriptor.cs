// ImageDescriptor.cs

namespace ComfyMinimalExample;

/// <summary>Identifies a generated image on the ComfyUI server for download via <c>GET /view</c>.</summary>
public record ImageDescriptor(string Filename, string Subfolder, string Type);

