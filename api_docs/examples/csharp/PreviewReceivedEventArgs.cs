// PreviewReceivedEventArgs.cs

using System;
using System.Text.Json;

namespace ComfyMinimalExample;

/// <summary>Event args for <see cref="WebSocketMonitor.PreviewImageReceived"/>.</summary>
public sealed class PreviewReceivedEventArgs : EventArgs
{
    /// <summary>1-based index of this preview within the current execution.</summary>
    public int Index { get; }

    /// <summary>File extension: <c>"jpg"</c> or <c>"png"</c>.</summary>
    public string Extension { get; }

    /// <summary>Raw image bytes from the WebSocket frame.</summary>
    public byte[] ImageBytes { get; }

    /// <summary>
    /// Optional JSON metadata from Type 4 frames (e.g. <c>node_id</c>, <c>image_type</c>).
    /// <see langword="null"/> for Type 1 frames.
    /// </summary>
    public JsonElement? Metadata { get; }

    /// <summary>
    /// Path where the image will be saved. The file is written asynchronously
    /// and may not exist yet when the event fires.
    /// </summary>
    public string SavePath { get; }

    internal PreviewReceivedEventArgs(int index, PreviewImage preview, string savePath)
    {
        Index      = index;
        Extension  = preview.Extension;
        ImageBytes = preview.ImageBytes;
        Metadata   = preview.Metadata;
        SavePath   = savePath;
    }
}

