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
/// Connects to the ComfyUI WebSocket and processes events for a running prompt.
/// Raises <see cref="PreviewImageReceived"/> for each in-progress preview frame
/// and saves it to <see cref="OutputDir"/> asynchronously without blocking the
/// receive loop.
/// </summary>
public class WebSocketMonitor
{
    private readonly ComfyConfig _config;

    /// <summary>Directory where preview images are saved. Defaults to <c>"output"</c>.</summary>
    public string OutputDir { get; init; }

    // 4 MB receive buffer — sufficient for most preview frames.
    private const int BufferSize = 4 * 1024 * 1024;

    /// <summary>
    /// Raised each time the server sends a binary preview frame.
    /// Fires synchronously before the image is written to disk.
    /// </summary>
    public event EventHandler<PreviewReceivedEventArgs>? PreviewImageReceived;

    /// <param name="config">Shared configuration instance.</param>
    /// <param name="outputDir">Directory where preview images are saved.</param>
    public WebSocketMonitor(ComfyConfig config, string outputDir = "output")
    {
        _config   = config;
        OutputDir = outputDir;
    }

    private static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset]     << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8)  |
        buffer[offset + 3];

    // Reads a complete WebSocket message, accumulating continuation frames.
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
                    $"WebSocket message exceeds the {BufferSize / 1024 / 1024} MB receive buffer.");
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

    // Decodes a binary WebSocket frame into a PreviewImage.
    // Type 1 — PREVIEW_IMAGE:          [4B type][4B format][image bytes]
    // Type 4 — PREVIEW_IMAGE_METADATA: [4B type][4B meta len][JSON][image bytes]
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

    // Fire-and-forget disk write for a preview image. Errors are logged, not thrown.
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

    /// <summary>
    /// Connects to the ComfyUI WebSocket and waits until the prompt completes.
    /// Returns a map of node IDs to their output data from <c>executed</c> events.
    /// </summary>
    public async Task<Dictionary<string, JsonElement>> WaitForCompletionAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n→ Connecting to WebSocket …");

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(
            new Uri($"{_config.WsUrl}/ws?clientId={_config.ClientId}"),
            cancellationToken);

        Console.WriteLine("  ✓ WebSocket connected — waiting for results …\n");

        var nodeOutputs = new Dictionary<string, JsonElement>();
        var buffer      = new byte[BufferSize];
        int previewCount = 0;

        Directory.CreateDirectory(OutputDir);

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result =
                await ReceiveFullMessageAsync(ws, buffer, cancellationToken);
            int bytesReceived = result.Count;

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellationToken);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Decode the preview, notify subscribers, then save asynchronously.
                // The receive loop never waits for the disk write to complete.
                var preview = DecodePreviewImage(buffer, bytesReceived);
                if (preview is not null)
                {
                    previewCount++;
                    string savePath = Path.Combine(OutputDir, $"preview_{previewCount}.{preview.Extension}");

                    PreviewImageReceived?.Invoke(this, new PreviewReceivedEventArgs(previewCount, preview, savePath));
                    _ = SavePreviewAsync(preview.ImageBytes, savePath, cancellationToken);
                }
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
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

                        // null node means all nodes finished
                        if (!data.TryGetProperty("node", out var nodeElem) ||
                            nodeElem.ValueKind == JsonValueKind.Null)
                        {
                            Console.WriteLine("\n  ✅ Execution complete");
                            await ws.CloseAsync(
                                WebSocketCloseStatus.NormalClosure, "done", cancellationToken);
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
                        throw new InvalidOperationException($"Execution error: {message}");
                    }

                    case "execution_cached":
                    case "status":
                        break;

                    default:
                        Console.WriteLine($"  [ws] Unknown event: {eventType}");
                        break;
                }
            }
        }

        return nodeOutputs;
    }
}

