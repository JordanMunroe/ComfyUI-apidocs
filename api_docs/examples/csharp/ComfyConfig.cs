// ComfyConfig.cs
// Configuration for the ComfyUI API client.
//
// Edit this file to point to your ComfyUI server and to choose between
// single-user and multi-user mode before running the example.

using System;

namespace ComfyMinimalExample;

/// <summary>
/// Holds all configuration values used by the ComfyUI API client classes.
///
/// A single instance is created in <see cref="Program"/> and passed into every
/// class that needs it, providing one place to change any setting.
/// </summary>
public sealed class ComfyConfig
{
    /// <summary>
    /// HTTP base URL of the ComfyUI server.
    /// </summary>
    /// <value>Defaults to <c>http://127.0.0.1:8188</c>.</value>
    public string BaseUrl { get; init; } = "http://127.0.0.1:8188";

    /// <summary>
    /// WebSocket base URL of the ComfyUI server.
    /// </summary>
    /// <value>Defaults to <c>ws://127.0.0.1:8188</c>.</value>
    public string WsUrl { get; init; } = "ws://127.0.0.1:8188";

    /// <summary>
    /// Whether the server is running in multi-user mode (<c>--multi-user</c>).
    /// When <see langword="true"/>, every request includes the
    /// <c>comfy-user</c> header so the server can isolate each user's
    /// settings and output files.
    /// </summary>
    /// <value>Defaults to <see langword="false"/> (single-user mode).</value>
    public bool MultiUser { get; init; } = false;

    /// <summary>
    /// User identifier sent as the <c>comfy-user</c> header in multi-user mode.
    /// Any non-empty string is valid — username, UUID, or email address.
    /// </summary>
    /// <value>Defaults to <c>"alice"</c>.</value>
    public string UserId { get; init; } = "alice";

    /// <summary>
    /// Unique client identifier for this session.
    /// The same ID must be used for both the WebSocket connection and prompt
    /// submissions so the server routes preview images and events back to
    /// this specific client.
    /// </summary>
    /// <value>A freshly generated UUID, fixed for the lifetime of the instance.</value>
    public string ClientId { get; } = Guid.NewGuid().ToString();
}
