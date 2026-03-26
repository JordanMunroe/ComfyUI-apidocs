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

/// <summary>HTTP client for the ComfyUI REST API.</summary>
public class ComfyClient
{
    private readonly ComfyConfig _config;
    private readonly HttpClient  _httpClient;

    /// <param name="config">Shared configuration instance.</param>
    /// <param name="httpClient">Shared HttpClient; caller owns its lifetime.</param>
    public ComfyClient(ComfyConfig config, HttpClient httpClient)
    {
        _config     = config;
        _httpClient = httpClient;

        // In multi-user mode, add the comfy-user header globally.
        if (_config.MultiUser)
        {
            _httpClient.DefaultRequestHeaders.Add("comfy-user", _config.UserId);
        }
    }

    /// <summary>Checks server reachability and logs the ComfyUI version.</summary>
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

    /// <summary>Submits a workflow to the queue and returns the assigned <c>prompt_id</c>.</summary>
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

    /// <summary>Downloads a generated image from <c>GET /view</c> and saves it locally.</summary>
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

        await using var fileStream = File.Create(localPath);
        await response.Content.CopyToAsync(fileStream, cancellationToken);

        Console.WriteLine($"  ✓ Saved → {localPath}");
        return localPath;
    }

    /// <summary>Extracts image descriptors from node outputs returned after execution.</summary>
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

