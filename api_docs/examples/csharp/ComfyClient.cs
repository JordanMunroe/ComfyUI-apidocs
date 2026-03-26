// ComfyClient.cs
// HTTP API client for ComfyUI.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ComfyMinimalExample;

/// <summary>
/// HTTP client for the ComfyUI REST API.
///
/// Wraps a shared <see cref="HttpClient"/> and centralises header construction,
/// including the optional <c>comfy-user</c> header required in multi-user mode.
///
/// One instance should be shared for the lifetime of the application so that
/// the underlying <see cref="HttpClient"/> connection pool is reused.
/// </summary>
/// <example>
/// <code>
/// var config = new ComfyConfig { MultiUser = false };
/// using var httpClient = new HttpClient();
/// var client = new ComfyClient(config, httpClient);
/// await client.GetServerStatusAsync();
/// </code>
/// </example>
public class ComfyClient
{
    private readonly ComfyConfig _config;
    private readonly HttpClient  _httpClient;

    /// <summary>
    /// Initialises a new <see cref="ComfyClient"/>.
    /// </summary>
    /// <param name="config">Shared <see cref="ComfyConfig"/> instance.</param>
    /// <param name="httpClient">
    /// A shared <see cref="HttpClient"/> instance. The caller is responsible
    /// for its lifecycle (disposal).
    /// </param>
    public ComfyClient(ComfyConfig config, HttpClient httpClient)
    {
        _config     = config;
        _httpClient = httpClient;

        // Apply the comfy-user header globally in multi-user mode so it is
        // never accidentally omitted from individual calls.
        if (_config.MultiUser)
        {
            _httpClient.DefaultRequestHeaders.Add("comfy-user", _config.UserId);
        }
    }

    // -------------------------------------------------------------------------
    // Public API methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetches system statistics from the ComfyUI server.
    ///
    /// Use this to confirm the server is reachable and log the running version
    /// before submitting any work.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>A task that completes when the status check is done.</returns>
    /// <exception cref="HttpRequestException">
    /// Thrown when the server is unreachable or returns a non-success status code.
    /// </exception>
    public async Task GetServerStatusAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("→ Checking server status …");

        var stats = await _httpClient.GetFromJsonAsync<JsonElement>(
            $"{_config.BaseUrl}/system_stats", cancellationToken);

        string version = "unknown";
        if (stats.TryGetProperty("system", out var sys) &&
            sys.TryGetProperty("comfyui_version", out var ver))
        {
            version = ver.GetString() ?? "unknown";
        }

        Console.WriteLine($"  ✓ Server online — ComfyUI {version}");
    }

    /// <summary>
    /// Submits a workflow to the ComfyUI execution queue.
    ///
    /// The <see cref="ComfyConfig.ClientId"/> ties this submission to the
    /// WebSocket connection so the server routes progress events and preview
    /// images back to this specific client.
    /// </summary>
    /// <param name="workflow">Workflow graph returned by <see cref="WorkflowBuilder"/>.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The <c>prompt_id</c> assigned by the server.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the server rejects the prompt (e.g. invalid workflow or node errors).
    /// </exception>
    public async Task<string> QueueWorkflowAsync(
        object workflow, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n→ Queuing workflow …");

        var body = new { prompt = workflow, client_id = _config.ClientId };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.BaseUrl}/prompt", body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"POST /prompt failed: HTTP {(int)response.StatusCode} — {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: cancellationToken);

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

    /// <summary>
    /// Downloads a generated image from the ComfyUI server and saves it locally.
    ///
    /// Images are served by <c>GET /view</c> and identified by:
    /// <list type="bullet">
    ///   <item><description><c>filename</c>  — filename returned in node outputs</description></item>
    ///   <item><description><c>subfolder</c> — subdirectory under ComfyUI's <c>output/</c> folder (often empty)</description></item>
    ///   <item><description><c>type</c>      — <c>"output"</c> for generated images, <c>"input"</c> for uploads</description></item>
    /// </list>
    /// </summary>
    /// <param name="filename">Image filename from node output.</param>
    /// <param name="subfolder">Subfolder within the output directory.</param>
    /// <param name="type">Storage type (<c>"output"</c> or <c>"input"</c>).</param>
    /// <param name="destDir">Local directory where the image is saved.</param>
    /// <param name="cancellationToken">Token to cancel the download.</param>
    /// <returns>The local file path where the image was saved.</returns>
    /// <exception cref="HttpRequestException">Thrown when the download fails.</exception>
    public async Task<string> DownloadImageAsync(
        string filename,
        string subfolder = "",
        string type = "output",
        string destDir = "output",
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n→ Downloading {filename} …");

        string queryString =
            $"filename={Uri.EscapeDataString(filename)}" +
            $"&subfolder={Uri.EscapeDataString(subfolder)}" +
            $"&type={Uri.EscapeDataString(type)}";

        var response = await _httpClient.GetAsync(
            $"{_config.BaseUrl}/view?{queryString}", cancellationToken);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(destDir);
        string localPath = Path.Combine(destDir, filename);

        // Stream directly to disk to avoid buffering large image files in memory
        await using var fileStream = File.Create(localPath);
        await response.Content.CopyToAsync(fileStream, cancellationToken);

        Console.WriteLine($"  ✓ Saved → {localPath}");
        return localPath;
    }

    /// <summary>
    /// Extracts image descriptors from the node output map returned after execution.
    ///
    /// The server returns outputs keyed by node ID; image filenames appear
    /// under the <c>images</c> array of nodes such as <c>SaveImage</c>.
    /// </summary>
    /// <param name="nodeOutputs">
    /// Output map from <see cref="WebSocketMonitor.WaitForCompletionAsync"/>.
    /// </param>
    /// <returns>
    /// A list of <see cref="ImageDescriptor"/> objects ready to pass to
    /// <see cref="DownloadImageAsync"/>.
    /// </returns>
    public static List<ImageDescriptor> ExtractImages(
        Dictionary<string, JsonElement> nodeOutputs)
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
}
