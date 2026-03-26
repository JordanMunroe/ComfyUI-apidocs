// ComfyConfig.cs
// Edit this file to point to your ComfyUI server before running the example.

using System;

namespace ComfyMinimalExample;

/// <summary>Configuration shared by all ComfyUI API client classes.</summary>
public sealed class ComfyConfig
{
    /// <summary>HTTP base URL of the ComfyUI server.</summary>
    public string BaseUrl { get; init; } = "http://127.0.0.1:8188";

    /// <summary>WebSocket base URL of the ComfyUI server.</summary>
    public string WsUrl { get; init; } = "ws://127.0.0.1:8188";

    /// <summary>
    /// Set to <see langword="true"/> when ComfyUI is started with <c>--multi-user</c>.
    /// Every request will include the <c>comfy-user</c> header.
    /// </summary>
    public bool MultiUser { get; init; } = false;

    /// <summary>Value sent as the <c>comfy-user</c> header in multi-user mode.</summary>
    public string UserId { get; init; } = "alice";

    /// <summary>
    /// Unique client ID for this session. Must be the same for both the WebSocket
    /// connection and prompt submissions so the server routes events back here.
    /// </summary>
    public string ClientId { get; } = Guid.NewGuid().ToString();
}

