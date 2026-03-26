// ComfyUI Minimal API Example — C#
//
// Demonstrates:
//   - Single-user mode  (no comfy-user header)
//   - Multi-user mode   (comfy-user header on every request)
//   - Getting server status
//   - Queuing a text-to-image workflow
//   - Receiving real-time progress updates and preview images via WebSocket
//   - Downloading the final generated image over HTTP
//
// Requirements:
//   - .NET 8 or newer
//   - NuGet packages (add to your .csproj or run the install commands below):
//       dotnet add package System.Net.Http.Json
//       dotnet add package System.Text.Json
//   - The WebSocket and HttpClient types are part of the BCL — no extra package needed.
//
// Usage:
//   dotnet run
//
// Before running, make sure ComfyUI is started:
//   python main.py                # single-user mode (default)
//   python main.py --multi-user   # multi-user mode

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

/// <summary>
/// Set to <see langword="true"/> when ComfyUI is started with --multi-user.
/// In multi-user mode every request must include the <c>comfy-user</c> header
/// so the server can isolate each user's settings and output files.
/// In single-user mode the header is simply omitted.
/// </summary>
const bool MultiUser = false;

/// <summary>
/// Base HTTP URL of the ComfyUI server.
/// </summary>
const string BaseUrl = "http://127.0.0.1:8188";

/// <summary>
/// WebSocket URL of the ComfyUI server.
/// </summary>
const string WsUrl = "ws://127.0.0.1:8188";

/// <summary>
/// Stable client identifier for this session.
/// Use the same ID for both the WebSocket connection and prompt submissions so
/// the server routes live previews back to this specific client.
/// </summary>
string clientId = Guid.NewGuid().ToString();

/// <summary>
/// User identifier used in multi-user mode.
/// Any non-empty string is valid — username, UUID, or email address.
/// </summary>
const string UserId = "alice";

// ---------------------------------------------------------------------------
// Program entry point
// ---------------------------------------------------------------------------

Console.WriteLine("=== ComfyUI Minimal API Example ===");
Console.WriteLine($"Mode    : {(MultiUser ? $"multi-user (user: {UserId})" : "single-user")}");
Console.WriteLine($"ClientID: {clientId}\n");

// Shared HttpClient — reuse a single instance across all requests to benefit
// from connection pooling and avoid socket exhaustion under load.
using var httpClient = new HttpClient();

// Apply the comfy-user header globally when running in multi-user mode so we
// never accidentally forget it on individual calls.
#pragma warning disable CS0162 // Unreachable code — expected when MultiUser is const false
if (MultiUser)
{
    httpClient.DefaultRequestHeaders.Add("comfy-user", UserId);
}
#pragma warning restore CS0162

// 1. Verify the server is reachable
await GetServerStatusAsync(httpClient);

// 2. Build a simple text-to-image workflow graph
var workflow = BuildWorkflow();

// 3. Submit the workflow to the execution queue
string promptId = await QueueWorkflowAsync(httpClient, workflow, clientId);

// 4. Connect via WebSocket and wait for completion, collecting previews
var nodeOutputs = await WaitForCompletionAsync(promptId, clientId);

// 5. Download all generated images
var images = ExtractImages(nodeOutputs);
if (images.Count == 0)
{
    Console.WriteLine("\n⚠ No images found in node outputs.");
}
else
{
    foreach (var img in images)
    {
        await DownloadImageAsync(httpClient, img.Filename, img.Subfolder, img.Type);
    }
    Console.WriteLine($"\n✅ Done! {images.Count} image(s) downloaded to ./output/");
}

// ---------------------------------------------------------------------------
// Step 1 — Check server status
// ---------------------------------------------------------------------------

/// <summary>
/// Fetches system statistics from the ComfyUI server.
/// Use this to confirm the server is reachable before submitting work.
/// </summary>
/// <param name="client">Shared <see cref="HttpClient"/> instance.</param>
/// <returns>A task that completes when the status check is done.</returns>
/// <exception cref="HttpRequestException">
/// Thrown when the server is unreachable or returns a non-success status code.
/// </exception>
static async Task GetServerStatusAsync(HttpClient client)
{
    Console.WriteLine("→ Checking server status …");

    var stats = await client.GetFromJsonAsync<JsonElement>($"{BaseUrl}/system_stats");
    string version = "unknown";
    if (stats.TryGetProperty("system", out var sys) &&
        sys.TryGetProperty("comfyui_version", out var ver))
    {
        version = ver.GetString() ?? "unknown";
    }

    Console.WriteLine($"  ✓ Server online — ComfyUI {version}");
}

// ---------------------------------------------------------------------------
// Step 2 — Build a minimal text-to-image workflow
// ---------------------------------------------------------------------------

/// <summary>
/// Builds a minimal Stable Diffusion text-to-image workflow graph.
/// </summary>
/// <remarks>
/// Node graph:
/// <list type="bullet">
///   <item><description>"1" CheckpointLoaderSimple — loads model, CLIP encoder, and VAE</description></item>
///   <item><description>"2" CLIPTextEncode         — encodes the positive text prompt</description></item>
///   <item><description>"3" CLIPTextEncode         — encodes the negative text prompt</description></item>
///   <item><description>"4" EmptyLatentImage       — creates a blank latent canvas</description></item>
///   <item><description>"5" KSampler               — runs the diffusion sampling loop</description></item>
///   <item><description>"6" VAEDecode              — converts latent tensor to pixel space</description></item>
///   <item><description>"7" SaveImage              — writes the final PNG to disk</description></item>
/// </list>
///
/// Node references use the format ["node_id", output_index]:
/// <list type="bullet">
///   <item><description>["1", 0] → MODEL output of CheckpointLoaderSimple</description></item>
///   <item><description>["1", 1] → CLIP  output of CheckpointLoaderSimple</description></item>
///   <item><description>["1", 2] → VAE   output of CheckpointLoaderSimple</description></item>
/// </list>
/// </remarks>
/// <param name="positivePrompt">Text describing what to generate.</param>
/// <param name="negativePrompt">Text describing what to avoid.</param>
/// <param name="checkpointName">Model filename inside ComfyUI's models/ directory.</param>
/// <returns>An anonymous object representing the workflow graph.</returns>
static object BuildWorkflow(
    string positivePrompt = "a beautiful sunset over mountains, golden hour, photorealistic",
    string negativePrompt = "blurry, low quality, watermark",
    string checkpointName = "sd_xl_base_1.0.safetensors")
{
    int seed = new Random().Next();

    return new Dictionary<string, object>
    {
        ["1"] = new
        {
            class_type = "CheckpointLoaderSimple",
            inputs = new { ckpt_name = checkpointName },
        },
        ["2"] = new
        {
            class_type = "CLIPTextEncode",
            inputs = new
            {
                text = positivePrompt,
                clip = new object[] { "1", 1 }, // CLIP from CheckpointLoader
            },
        },
        ["3"] = new
        {
            class_type = "CLIPTextEncode",
            inputs = new
            {
                text = negativePrompt,
                clip = new object[] { "1", 1 }, // CLIP from CheckpointLoader
            },
        },
        ["4"] = new
        {
            class_type = "EmptyLatentImage",
            inputs = new { width = 512, height = 512, batch_size = 1 },
        },
        ["5"] = new
        {
            class_type = "KSampler",
            inputs = new
            {
                seed,
                steps = 20,
                cfg = 7.0,                           // classifier-free guidance scale
                sampler_name = "euler",
                scheduler = "normal",
                denoise = 1.0,                       // 1.0 = full denoising (text-to-image)
                model = new object[] { "1", 0 },     // MODEL from CheckpointLoader
                positive = new object[] { "2", 0 },  // CONDITIONING from positive CLIPTextEncode
                negative = new object[] { "3", 0 },  // CONDITIONING from negative CLIPTextEncode
                latent_image = new object[] { "4", 0 }, // LATENT from EmptyLatentImage
            },
        },
        ["6"] = new
        {
            class_type = "VAEDecode",
            inputs = new
            {
                samples = new object[] { "5", 0 }, // LATENT from KSampler
                vae = new object[] { "1", 2 },     // VAE from CheckpointLoader
            },
        },
        ["7"] = new
        {
            class_type = "SaveImage",
            inputs = new
            {
                filename_prefix = "minimal_example",
                images = new object[] { "6", 0 }, // IMAGE from VAEDecode
            },
        },
    };
}

// ---------------------------------------------------------------------------
// Step 3 — Queue the workflow
// ---------------------------------------------------------------------------

/// <summary>
/// Submits a workflow to the ComfyUI execution queue.
/// </summary>
/// <remarks>
/// The <paramref name="clientId"/> ties this submission to the WebSocket
/// connection so the server sends progress events and previews back to this
/// specific client.
/// </remarks>
/// <param name="client">Shared <see cref="HttpClient"/> instance.</param>
/// <param name="workflow">Workflow graph returned by <see cref="BuildWorkflow"/>.</param>
/// <param name="clientId">Client ID that matches the WebSocket connection.</param>
/// <returns>The <c>prompt_id</c> assigned by the server.</returns>
/// <exception cref="InvalidOperationException">
/// Thrown when the server rejects the prompt (e.g. invalid workflow or node errors).
/// </exception>
static async Task<string> QueueWorkflowAsync(HttpClient client, object workflow, string clientId)
{
    Console.WriteLine("\n→ Queuing workflow …");

    var body = new { prompt = workflow, client_id = clientId };

    var response = await client.PostAsJsonAsync($"{BaseUrl}/prompt", body);

    if (!response.IsSuccessStatusCode)
    {
        string error = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"POST /prompt failed: HTTP {(int)response.StatusCode} — {error}");
    }

    var result = await response.Content.ReadFromJsonAsync<JsonElement>();

    // The server may return node-level validation errors alongside a 200 OK.
    if (result.TryGetProperty("node_errors", out var nodeErrors) &&
        nodeErrors.ValueKind == JsonValueKind.Object &&
        nodeErrors.EnumerateObject().Any())
    {
        Console.WriteLine($"  ⚠ Node errors detected: {nodeErrors}");
    }

    string promptId = result.GetProperty("prompt_id").GetString()
        ?? throw new InvalidOperationException("Server did not return a prompt_id.");

    Console.WriteLine($"  ✓ Queued — prompt_id: {promptId}");
    return promptId;
}

// ---------------------------------------------------------------------------
// Step 4 — Monitor via WebSocket (previews + progress + completion)
// ---------------------------------------------------------------------------

/// <summary>
/// Decodes a binary preview image sent by the ComfyUI server over WebSocket.
/// </summary>
/// <remarks>
/// Two binary event formats are supported:
/// <list type="bullet">
///   <item>
///     <description>
///       Type 1 — PREVIEW_IMAGE:
///       [4B event type][4B image format][image bytes]
///       Image format: 1 = JPEG, 2 = PNG
///     </description>
///   </item>
///   <item>
///     <description>
///       Type 4 — PREVIEW_IMAGE_WITH_METADATA:
///       [4B event type][4B metadata length][UTF-8 JSON][image bytes]
///     </description>
///   </item>
/// </list>
/// </remarks>
/// <param name="buffer">Raw binary data received from the WebSocket.</param>
/// <param name="length">Number of valid bytes in the buffer.</param>
/// <returns>
/// A <see cref="PreviewImage"/> if the buffer contains a recognised preview
/// event, or <see langword="null"/> for unknown event types.
/// </returns>
static PreviewImage? DecodePreviewImage(byte[] buffer, int length)
{
    if (length < 8) return null;

    // Big-endian uint32 at offset 0 identifies the event type
    uint eventType = ReadUInt32BigEndian(buffer, 0);

    if (eventType == 1)
    {
        // PREVIEW_IMAGE: 4-byte event type + 4-byte format code + image data
        uint formatCode = ReadUInt32BigEndian(buffer, 4);
        string extension = formatCode == 1 ? "jpg" : "png";
        byte[] imageBytes = buffer[8..length];
        return new PreviewImage(extension, imageBytes, null);
    }

    if (eventType == 4)
    {
        // PREVIEW_IMAGE_WITH_METADATA: metadata JSON prepended to image data
        uint metadataLength = ReadUInt32BigEndian(buffer, 4);
        int metadataStart = 8;
        int metadataEnd = metadataStart + (int)metadataLength;
        string metadataJson = Encoding.UTF8.GetString(buffer, metadataStart, (int)metadataLength);
        var metadata = JsonDocument.Parse(metadataJson).RootElement;
        byte[] imageBytes = buffer[metadataEnd..length];

        // Use image_type from metadata if available, fall back to PNG
        string mimeType = "image/png";
        if (metadata.TryGetProperty("image_type", out var imgType))
        {
            mimeType = imgType.GetString() ?? mimeType;
        }

        string extension = mimeType == "image/jpeg" ? "jpg" : "png";
        return new PreviewImage(extension, imageBytes, metadata);
    }

    return null; // Unknown event type — skip silently
}

/// <summary>
/// Reads an unsigned 32-bit integer from a byte array in big-endian byte order.
/// </summary>
/// <param name="buffer">Source byte array.</param>
/// <param name="offset">Zero-based byte offset in <paramref name="buffer"/>.</param>
/// <returns>The decoded <see cref="uint"/> value.</returns>
static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
    ((uint)buffer[offset] << 24) |
    ((uint)buffer[offset + 1] << 16) |
    ((uint)buffer[offset + 2] << 8) |
    buffer[offset + 3];

/// <summary>
/// Opens a WebSocket connection to ComfyUI and waits until the specified
/// prompt finishes executing. Progress updates and preview images are handled
/// in real-time during the wait.
/// </summary>
/// <param name="promptId">The <c>prompt_id</c> returned by <see cref="QueueWorkflowAsync"/>.</param>
/// <param name="clientId">The client ID used when queuing the prompt.</param>
/// <param name="cancellationToken">Token to cancel the wait.</param>
/// <returns>
/// A dictionary mapping node IDs to their output objects, as returned by the
/// server in <c>executed</c> events.
/// </returns>
/// <exception cref="OperationCanceledException">
/// Thrown when <paramref name="cancellationToken"/> is cancelled.
/// </exception>
/// <exception cref="InvalidOperationException">
/// Thrown when the server reports an execution error for this prompt.
/// </exception>
static async Task<Dictionary<string, JsonElement>> WaitForCompletionAsync(
    string promptId,
    string clientId,
    CancellationToken cancellationToken = default)
{
    Console.WriteLine("\n→ Connecting to WebSocket …");

    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(
        new Uri($"{WsUrl}/ws?clientId={clientId}"),
        cancellationToken);

    Console.WriteLine("  ✓ WebSocket connected — waiting for results …\n");

    // Accumulate node outputs here; filled in as 'executed' events arrive.
    var nodeOutputs = new Dictionary<string, JsonElement>();

    // Receive buffer — 4 MB handles most preview images comfortably.
    // For very large preview images you may need to increase this.
    const int BufferSize = 4 * 1024 * 1024;
    var buffer = new byte[BufferSize];
    int previewCount = 0;

    // Ensure the local output directory exists before saving previews
    Directory.CreateDirectory("output");

    while (ws.State == WebSocketState.Open)
    {
        // Receive the next message — may require multiple reads for large payloads
        var segment = new ArraySegment<byte>(buffer);
        WebSocketReceiveResult result = await ws.ReceiveAsync(segment, cancellationToken);

        // Accumulate fragmented messages into a single contiguous buffer
        while (!result.EndOfMessage)
        {
            // Re-use remaining space in the buffer for continuation frames.
            // For very large messages you would grow a MemoryStream instead.
            int offset = result.Count;
            var continuation = new ArraySegment<byte>(buffer, offset, buffer.Length - offset);
            var next = await ws.ReceiveAsync(continuation, cancellationToken);
            result = new WebSocketReceiveResult(
                result.Count + next.Count,
                next.MessageType,
                next.EndOfMessage,
                next.CloseStatus,
                next.CloseStatusDescription);
        }

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
            // Binary message: preview image
            // ------------------------------------------------------------------
            var preview = DecodePreviewImage(buffer, bytesReceived);
            if (preview is not null)
            {
                previewCount++;
                string filename = $"output/preview_{previewCount}.{preview.Extension}";
                await File.WriteAllBytesAsync(filename, preview.ImageBytes, cancellationToken);

                string metaInfo = "";
                if (preview.Metadata.HasValue &&
                    preview.Metadata.Value.TryGetProperty("node_id", out var nodeId))
                {
                    metaInfo = $" (node: {nodeId.GetString()})";
                }

                Console.WriteLine($"  📷 Preview {previewCount} saved → {filename}{metaInfo}");
            }
        }
        else if (result.MessageType == WebSocketMessageType.Text)
        {
            // ------------------------------------------------------------------
            // JSON text message: status / progress / executed / error events
            // ------------------------------------------------------------------
            string json = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string eventType = root.GetProperty("type").GetString() ?? "";
            var data = root.TryGetProperty("data", out var d) ? d : default;

            switch (eventType)
            {
                case "execution_start":
                    // Fired once when the server begins processing our prompt
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

                    // node is null when the entire prompt is done
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
                    int max = data.GetProperty("max").GetInt32();
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
                        // Clone the element so it survives the using block above
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
                    // Nodes that hit the cache are reported here; no action needed
                    break;

                case "status":
                    // Overall queue status — useful for diagnostics but not required here
                    break;

                default:
                    Console.WriteLine($"  [ws] Unknown event: {eventType}");
                    break;
            }
        }
    }

    return nodeOutputs;
}

// ---------------------------------------------------------------------------
// Step 5 — Download the final generated image
// ---------------------------------------------------------------------------

/// <summary>
/// Downloads a generated image from the ComfyUI server and saves it locally.
/// </summary>
/// <remarks>
/// Images are served by the <c>/view</c> endpoint and are identified by:
/// <list type="bullet">
///   <item><description><c>filename</c>  — the filename returned in node outputs</description></item>
///   <item><description><c>subfolder</c> — subdirectory under ComfyUI's output/ folder (often empty)</description></item>
///   <item><description><c>type</c>      — "output" for finished images, "input" for uploaded images</description></item>
/// </list>
/// </remarks>
/// <param name="client">Shared <see cref="HttpClient"/> instance.</param>
/// <param name="filename">Image filename from node output.</param>
/// <param name="subfolder">Subfolder within the output directory.</param>
/// <param name="type">Storage type ("output" or "input").</param>
/// <returns>The local file path where the image was saved.</returns>
/// <exception cref="HttpRequestException">
/// Thrown when the download request fails.
/// </exception>
static async Task<string> DownloadImageAsync(
    HttpClient client,
    string filename,
    string subfolder = "",
    string type = "output")
{
    Console.WriteLine($"\n→ Downloading {filename} …");

    var query = new Dictionary<string, string?>
    {
        ["filename"] = filename,
        ["subfolder"] = subfolder,
        ["type"] = type,
    };

    string queryString = string.Join("&", query
        .Where(kv => kv.Value is not null)
        .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

    var response = await client.GetAsync($"{BaseUrl}/view?{queryString}");
    response.EnsureSuccessStatusCode();

    Directory.CreateDirectory("output");
    string localPath = Path.Combine("output", filename);

    // Stream the response body directly to disk to avoid buffering large files
    await using var fileStream = File.Create(localPath);
    await response.Content.CopyToAsync(fileStream);

    Console.WriteLine($"  ✓ Saved → {localPath}");
    return localPath;
}

// ---------------------------------------------------------------------------
// Step 6 — Extract image filenames from node outputs
// ---------------------------------------------------------------------------

/// <summary>
/// Searches the node output map returned after execution for image filenames.
/// </summary>
/// <remarks>
/// The server returns outputs per node; image filenames appear under the
/// <c>images</c> key of SaveImage (and similar) nodes.
/// </remarks>
/// <param name="nodeOutputs">Output map from <see cref="WaitForCompletionAsync"/>.</param>
/// <returns>
/// A list of <see cref="ImageDescriptor"/> objects ready to pass to
/// <see cref="DownloadImageAsync"/>.
/// </returns>
static List<ImageDescriptor> ExtractImages(Dictionary<string, JsonElement> nodeOutputs)
{
    var images = new List<ImageDescriptor>();

    foreach (var output in nodeOutputs.Values)
    {
        if (output.ValueKind != JsonValueKind.Object) continue;
        if (!output.TryGetProperty("images", out var imagesElem)) continue;
        if (imagesElem.ValueKind != JsonValueKind.Array) continue;

        foreach (var img in imagesElem.EnumerateArray())
        {
            string filename = img.GetProperty("filename").GetString() ?? "";
            string subfolder = img.TryGetProperty("subfolder", out var sf)
                ? sf.GetString() ?? "" : "";
            string imgType = img.TryGetProperty("type", out var t)
                ? t.GetString() ?? "output" : "output";

            if (!string.IsNullOrEmpty(filename))
            {
                images.Add(new ImageDescriptor(filename, subfolder, imgType));
            }
        }
    }

    return images;
}

// ---------------------------------------------------------------------------
// Type declarations — must appear after all top-level statements and functions
// ---------------------------------------------------------------------------

/// <summary>
/// Represents a decoded preview image received via WebSocket binary message.
/// </summary>
/// <param name="Extension">File extension ("jpg" or "png").</param>
/// <param name="ImageBytes">Raw image bytes ready to write to disk.</param>
/// <param name="Metadata">
/// Optional JSON metadata element (only present for Type 4 messages).
/// </param>
record PreviewImage(string Extension, byte[] ImageBytes, JsonElement? Metadata);

/// <summary>
/// Represents a generated image descriptor found in node execution outputs.
/// </summary>
/// <param name="Filename">Filename on the ComfyUI server.</param>
/// <param name="Subfolder">Subdirectory under the output root (may be empty).</param>
/// <param name="Type">Storage type: "output" for generated images, "input" for uploads.</param>
record ImageDescriptor(string Filename, string Subfolder, string Type);
