// PreviewReceivedEventArgs.cs
// Event arguments raised when the WebSocket monitor receives a preview image.

using System;
using System.Text.Json;

namespace ComfyMinimalExample;

/// <summary>
/// Event arguments delivered by <see cref="WebSocketMonitor.PreviewImageReceived"/>
/// each time the ComfyUI server sends a binary preview frame.
/// </summary>
/// <remarks>
/// Subscribers receive this object synchronously as the preview arrives, before
/// the image bytes are written to disk. You can read <see cref="ImageBytes"/>
/// directly or wait for the file to appear at <see cref="SavePath"/>.
/// </remarks>
public sealed class PreviewReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Sequential 1-based index of this preview within the current execution.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// File extension of the image: <c>"jpg"</c> or <c>"png"</c>.
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Raw image bytes decoded from the WebSocket binary frame.
    /// </summary>
    public byte[] ImageBytes { get; }

    /// <summary>
    /// Optional JSON metadata included in Type 4 (PREVIEW_IMAGE_WITH_METADATA) frames.
    /// Contains fields such as <c>node_id</c>, <c>prompt_id</c>, and <c>image_type</c>.
    /// <see langword="null"/> for Type 1 (PREVIEW_IMAGE) frames.
    /// </summary>
    public JsonElement? Metadata { get; }

    /// <summary>
    /// Local file path where the image will be (or has been) saved to disk.
    /// The file is written asynchronously by <see cref="WebSocketMonitor"/>; it
    /// may not exist yet at the moment the event fires.
    ///
    /// Subscribers who need to react <em>after</em> the file is on disk can
    /// monitor it with <see cref="System.IO.FileSystemWatcher"/>, poll the path
    /// with <see cref="System.IO.File.Exists"/>, or keep their own
    /// <see cref="System.Threading.Tasks.Task"/> by starting an async write
    /// against <see cref="ImageBytes"/> independently.
    /// </summary>
    public string SavePath { get; }

    /// <summary>
    /// Initialises a new <see cref="PreviewReceivedEventArgs"/>.
    /// </summary>
    /// <param name="index">1-based preview index.</param>
    /// <param name="preview">Decoded preview image from the WebSocket frame.</param>
    /// <param name="savePath">
    /// Local file path where the image bytes will be saved asynchronously.
    /// </param>
    internal PreviewReceivedEventArgs(int index, PreviewImage preview, string savePath)
    {
        Index      = index;
        Extension  = preview.Extension;
        ImageBytes = preview.ImageBytes;
        Metadata   = preview.Metadata;
        SavePath   = savePath;
    }
}
