// WebSocketMonitor.cs
// Real-time WebSocket monitor for ComfyUI workflow execution.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ComfyMinimalExample;

/// <summary>
/// Monitors a ComfyUI workflow execution via WebSocket.
///
/// Opens a persistent WebSocket connection and processes all server-pushed
/// events, including:
/// <list type="bullet">
///   <item><description>JSON lifecycle events: <c>execution_start</c>, <c>executing</c>, <c>executed</c>, <c>execution_error</c></description></item>
///   <item><description>JSON progress events: <c>progress</c></description></item>
///   <item><description>Binary frames: encoded preview images (Type 1 and Type 4)</description></item>
/// </list>
///
/// When a preview frame arrives, <see cref="PreviewImageReceived"/> is raised
/// immediately (synchronous notification) and the image bytes are written to
/// <see cref="OutputDir"/> asynchronously so the receive loop is never blocked.
/// </summary>
/// <example>
/// <code>
/// var monitor = new WebSocketMonitor(config);
///
/// // Subscribe before calling WaitForCompletionAsync
/// monitor.PreviewImageReceived += (_, args) =>
///     Console.WriteLine($"Preview #{args.Index} incoming → {args.SavePath}");
///
/// var nodeOutputs = await monitor.WaitForCompletionAsync(promptId);
/// </code>
/// </example>
public class WebSocketMonitor
{
    private readonly ComfyConfig _config;

    /// <summary>
    /// Local directory where preview images are saved during execution.
    /// </summary>
    /// <value>Defaults to <c>"output"</c>.</value>
    public string OutputDir { get; init; }

    // Receive buffer — 4 MB handles most preview images comfortably.
    // Messages larger than this will throw an InvalidOperationException.
    // Increase BufferSize or switch to a MemoryStream if you need larger previews.
    private const int BufferSize = 4 * 1024 * 1024;

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Raised on the calling thread each time the server sends a binary preview
    /// frame during workflow execution.
    ///
    /// The event fires synchronously before the image is written to disk.
    /// Subscribers can inspect <see cref="PreviewReceivedEventArgs.ImageBytes"/>
    /// immediately or wait for the file to appear at
    /// <see cref="PreviewReceivedEventArgs.SavePath"/>.
    ///
    /// Subscribers must not perform long-running or blocking work inline;
    /// use <c>Task.Run</c> if async processing is required.
    /// </summary>
    public event EventHandler<PreviewReceivedEventArgs>? PreviewImageReceived;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initialises a new <see cref="WebSocketMonitor"/>.
    /// </summary>
    /// <param name="config">Shared <see cref="ComfyConfig"/> instance.</param>
    /// <param name="outputDir">Directory where preview images are saved.</param>
    public WebSocketMonitor(ComfyConfig config, string outputDir = "output")
    {
        _config   = config;
        OutputDir = outputDir;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads an unsigned 32-bit integer from a byte array in big-endian byte order.
    /// </summary>
    /// <param name="buffer">Source byte array.</param>
    /// <param name="offset">Zero-based start offset in <paramref name="buffer"/>.</param>
    /// <returns>The decoded <see cref="uint"/> value.</returns>
    private static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset]     << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8)  |
        buffer[offset + 3];

    /// <summary>
    /// Receives a complete (possibly multi-frame) WebSocket message into
    /// <paramref name="buffer"/> and returns the final
    /// <see cref="WebSocketReceiveResult"/> with the total byte count.
    ///
    /// Continuation frames are accumulated sequentially until
    /// <see cref="WebSocketReceiveResult.EndOfMessage"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="ws">The open <see cref="ClientWebSocket"/>.</param>
    /// <param name="buffer">Pre-allocated receive buffer.</param>
    /// <param name="cancellationToken">Token to cancel the receive.</param>
    /// <returns>
    /// A <see cref="WebSocketReceiveResult"/> whose <c>Count</c> reflects the
    /// total number of bytes written to <paramref name="buffer"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the complete message size exceeds <see cref="BufferSize"/>.
    /// </exception>
    private static async Task<WebSocketReceiveResult> ReceiveFullMessageAsync(
        ClientWebSocket ws,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        WebSocketReceiveResult result =
            await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

        while (!result.EndOfMessage)
        {
            int offset = result.Count;
            if (offset >= buffer.Length)
            {
                throw new InvalidOperationException(
                    $"WebSocket message exceeds the {BufferSize / 1024 / 1024} MB receive " +
                    "buffer. Increase BufferSize or use a MemoryStream for larger messages.");
            }

            var continuation = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
            var next = await ws.ReceiveAsync(continuation, cancellationToken);
            result = new WebSocketReceiveResult(
                result.Count + next.Count,
                next.MessageType,
                next.EndOfMessage,
                next.CloseStatus,
                next.CloseStatusDescription);
        }

        return result;
    }

    /// <summary>
    /// Decodes a binary WebSocket message into a <see cref="PreviewImage"/>.
    ///
    /// Two binary event types are supported:
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
    /// <param name="buffer">Raw binary data received from the WebSocket.</param>
    /// <param name="length">Number of valid bytes in <paramref name="buffer"/>.</param>
    /// <returns>
    /// A decoded <see cref="PreviewImage"/>, or <see langword="null"/> for
    /// unrecognised event types.
    /// </returns>
    private static PreviewImage? DecodePreviewImage(byte[] buffer, int length)
    {
        if (length < 8) return null;

        uint eventType = ReadUInt32BigEndian(buffer, 0);

        if (eventType == 1)
        {
            // PREVIEW_IMAGE: format code immediately follows the event type
            uint formatCode = ReadUInt32BigEndian(buffer, 4);
            string extension = formatCode == 1 ? "jpg" : "png";
            byte[] imageBytes = buffer[8..length];
            return new PreviewImage(extension, imageBytes, null);
        }

        if (eventType == 4)
        {
            // PREVIEW_IMAGE_WITH_METADATA: JSON metadata prepended to image data
            uint metadataLength = ReadUInt32BigEndian(buffer, 4);
            int  metadataStart  = 8;
            int  metadataEnd    = metadataStart + (int)metadataLength;

            string metadataJson = Encoding.UTF8.GetString(
                buffer, metadataStart, (int)metadataLength);
            var metadata = JsonDocument.Parse(metadataJson).RootElement;
            byte[] imageBytes = buffer[metadataEnd..length];

            // Use image_type from metadata when available; fall back to PNG
            string mimeType = "image/png";
            if (metadata.TryGetProperty("image_type", out var imgType))
            {
                mimeType = imgType.GetString() ?? mimeType;
            }

            string extension = mimeType == "image/jpeg" ? "jpg" : "png";
            return new PreviewImage(extension, imageBytes, metadata);
        }

        return null; // Unknown binary event type — skip silently
    }

    /// <summary>
    /// Writes preview image bytes to disk asynchronously.
    ///
    /// This method is fire-and-forget from the receive loop — the returned
    /// <see cref="Task"/> is tracked in <c>pendingSaves</c> and awaited in bulk
    /// before <see cref="WaitForCompletionAsync"/> returns, so no preview is
    /// silently dropped.
    /// </summary>
    /// <param name="imageBytes">Raw bytes to write.</param>
    /// <param name="savePath">Destination file path.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    private static async Task SavePreviewAsync(
        byte[] imageBytes, string savePath, CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllBytesAsync(savePath, imageBytes, cancellationToken);
            Console.WriteLine($"  📷 Preview saved → {savePath}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"  ⚠ Failed to save preview {savePath}: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens a WebSocket connection to ComfyUI and waits until the specified
    /// prompt finishes executing.
    ///
    /// As preview frames arrive, <see cref="PreviewImageReceived"/> is raised
    /// immediately and each image is saved to <see cref="OutputDir"/>
    /// asynchronously — the receive loop is never blocked by disk I/O.
    ///
    /// The method returns once the <c>executing</c> event with
    /// <c>node: null</c> is received (all nodes done).  All pending disk writes
    /// are awaited before returning so previews are guaranteed to be flushed.
    ///
    /// It throws if the server reports an <c>execution_error</c> for this prompt.
    /// </summary>
    /// <param name="promptId">
    /// The <c>prompt_id</c> returned by <see cref="ComfyClient.QueueWorkflowAsync"/>.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>
    /// A dictionary mapping node IDs to their output <see cref="JsonElement"/>
    /// objects as reported by <c>executed</c> events.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the server reports an execution error for this prompt.
    /// </exception>
    public async Task<Dictionary<string, JsonElement>> WaitForCompletionAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n→ Connecting to WebSocket …");

        // The clientId in the URL must match the one used when queuing the
        // prompt so the server routes events for this prompt to this connection.
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(
            new Uri($"{_config.WsUrl}/ws?clientId={_config.ClientId}"),
            cancellationToken);

        Console.WriteLine("  ✓ WebSocket connected — waiting for results …\n");

        // Accumulate node outputs here; filled in as 'executed' events arrive.
        var nodeOutputs = new Dictionary<string, JsonElement>();
        var buffer      = new byte[BufferSize];
        int previewCount = 0;

        // Track all in-flight async disk writes so we can await them in bulk
        // before returning. This guarantees every preview is flushed to disk.
        var pendingSaves = new List<Task>();

        Directory.CreateDirectory(OutputDir);

        while (ws.State == WebSocketState.Open)
        {
            // Receive the next complete message (accumulates continuation frames internally).
            WebSocketReceiveResult result =
                await ReceiveFullMessageAsync(ws, buffer, cancellationToken);
            int bytesReceived = result.Count;

            if (result.MessageType == WebSocketMessageType.Close)
            {
                // Server initiated a graceful close
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellationToken);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // ------------------------------------------------------------------
                // Binary frame: preview image generated during sampling.
                //
                // 1. Decode the frame immediately (synchronous, zero-copy slice).
                // 2. Raise PreviewImageReceived so subscribers are notified at once.
                // 3. Start the disk write asynchronously and track it — the receive
                //    loop continues without waiting for I/O to complete.
                // ------------------------------------------------------------------
                var preview = DecodePreviewImage(buffer, bytesReceived);
                if (preview is not null)
                {
                    previewCount++;
                    string savePath = Path.Combine(OutputDir, $"preview_{previewCount}.{preview.Extension}");

                    // Raise the event synchronously — subscribers are notified before
                    // the file exists on disk (SavePath documents this behaviour).
                    var args = new PreviewReceivedEventArgs(previewCount, preview, savePath);
                    PreviewImageReceived?.Invoke(this, args);

                    // Fire-and-forget the save; track the Task so we can await it later.
                    pendingSaves.Add(SavePreviewAsync(preview.ImageBytes, savePath, cancellationToken));
                }
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                // ------------------------------------------------------------------
                // JSON text frame: lifecycle / progress / output events
                // ------------------------------------------------------------------
                string json = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string eventType = root.GetProperty("type").GetString() ?? "";
                var data = root.TryGetProperty("data", out var d) ? d : default;

                switch (eventType)
                {
                    case "execution_start":
                        if (data.ValueKind != JsonValueKind.Undefined &&
                            data.TryGetProperty("prompt_id", out var startPid) &&
                            startPid.GetString() == promptId)
                        {
                            Console.WriteLine("  ▶ Execution started");
                        }
                        break;

                    case "executing":
                    {
                        if (data.ValueKind == JsonValueKind.Undefined) break;
                        if (!data.TryGetProperty("prompt_id", out var exPid) ||
                            exPid.GetString() != promptId) break;

                        // null node signals that all nodes have finished
                        if (!data.TryGetProperty("node", out var nodeElem) ||
                            nodeElem.ValueKind == JsonValueKind.Null)
                        {
                            Console.WriteLine("\n  ✅ Execution complete");
                            await ws.CloseAsync(
                                WebSocketCloseStatus.NormalClosure, "done", cancellationToken);

                            // Flush all in-flight preview saves before returning.
                            await Task.WhenAll(pendingSaves);
                            return nodeOutputs;
                        }

                        Console.WriteLine($"  ⚙  Executing node {nodeElem.GetString()} …");
                        break;
                    }

                    case "progress":
                    {
                        if (data.ValueKind == JsonValueKind.Undefined) break;
                        if (!data.TryGetProperty("prompt_id", out var progPid) ||
                            progPid.GetString() != promptId) break;

                        int value = data.GetProperty("value").GetInt32();
                        int max   = data.GetProperty("max").GetInt32();
                        double percent = (double)value / max * 100.0;
                        // Overwrite the same console line to avoid spamming the terminal
                        Console.Write($"\r  ⏳ Sampling: {percent:F1}% (step {value}/{max})    ");
                        break;
                    }

                    case "executed":
                    {
                        if (data.ValueKind == JsonValueKind.Undefined) break;
                        if (!data.TryGetProperty("prompt_id", out var execPid) ||
                            execPid.GetString() != promptId) break;

                        if (data.TryGetProperty("node", out var execNode) &&
                            data.TryGetProperty("output", out var output))
                        {
                            // Clone the element so it outlives this JsonDocument's scope
                            nodeOutputs[execNode.GetString() ?? "?"] = output.Clone();
                        }
                        break;
                    }

                    case "execution_error":
                    {
                        if (data.ValueKind == JsonValueKind.Undefined) break;
                        if (!data.TryGetProperty("prompt_id", out var errPid) ||
                            errPid.GetString() != promptId) break;

                        string message = data.TryGetProperty("exception_message", out var msg)
                            ? msg.GetString() ?? "unknown error"
                            : "unknown error";

                        await ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure, "error", cancellationToken);

                        // Still await pending saves so partially-written files are not lost
                        await Task.WhenAll(pendingSaves);
                        throw new InvalidOperationException($"Execution error: {message}");
                    }

                    case "execution_cached":
                        // Node output served from cache — no action required
                        break;

                    case "status":
                        // Overall queue status — useful for diagnostics, not needed here
                        break;

                    default:
                        Console.WriteLine($"  [ws] Unknown event: {eventType}");
                        break;
                }
            }
        }

        // Flush any pending saves if the loop exited via the Close message path
        await Task.WhenAll(pendingSaves);
        return nodeOutputs;
    }
}

