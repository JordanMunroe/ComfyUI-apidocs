// Program.cs
// Entry point for the ComfyUI minimal API example.
//
// Demonstrates the full workflow using four OOP classes:
//   1. ComfyConfig      — shared configuration
//   2. WorkflowBuilder  — constructs the workflow graph
//   3. ComfyClient      — HTTP operations (status, queue, download)
//   4. WebSocketMonitor — real-time progress and preview images
//
// Requirements:
//   - .NET 8 or newer
//   - dotnet add package System.Net.Http.Json
//
// Usage:
//   dotnet run
//
// Before running, start ComfyUI:
//   python main.py                # single-user mode (default)
//   python main.py --multi-user   # multi-user mode

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ComfyMinimalExample;

/// <summary>
/// Application entry point for the ComfyUI minimal API example.
/// </summary>
internal class Program
{
    /// <summary>
    /// Runs the minimal API example end-to-end.
    /// </summary>
    private static async Task Main()
    {
        // ------------------------------------------------------------------
        // Configuration
        //
        // Set MultiUser = true when ComfyUI is started with --multi-user.
        // All other values default to the standard local development setup.
        // ------------------------------------------------------------------
        var config = new ComfyConfig
        {
            MultiUser = false,   // flip to true for --multi-user server
            UserId    = "alice", // only used in multi-user mode
        };

        Console.WriteLine("=== ComfyUI Minimal API Example ===");
        Console.WriteLine($"Mode    : {(config.MultiUser ? $"multi-user (user: {config.UserId})" : "single-user")}");
        Console.WriteLine($"ClientID: {config.ClientId}\n");

        // ------------------------------------------------------------------
        // Create one instance of each service class.
        //
        // The shared HttpClient is created here and passed into ComfyClient so
        // connection pooling is used and sockets are not exhausted under load.
        // ------------------------------------------------------------------
        using var httpClient = new HttpClient();

        var client  = new ComfyClient(config, httpClient);
        var builder = new WorkflowBuilder();
        var monitor = new WebSocketMonitor(config);

        // Subscribe to the preview event before calling WaitForCompletionAsync.
        // The event fires on the WebSocket receive thread as soon as each binary
        // frame is decoded — before the image bytes have been written to disk.
        monitor.PreviewImageReceived += OnPreviewImageReceived;

        try
        {
            // 1. Verify the server is reachable.
            await client.GetServerStatusAsync();

            // 2. Build a text-to-image workflow graph.
            var workflow = builder.BuildTxt2Img(
                positivePrompt: "a beautiful sunset over mountains, golden hour, photorealistic",
                negativePrompt: "blurry, low quality, watermark");

            // 3. Submit the workflow to the execution queue.
            string promptId = await client.QueueWorkflowAsync(workflow);

            // 4. Wait for execution to finish, collecting previews along the way.
            var nodeOutputs = await monitor.WaitForCompletionAsync(promptId);

            // 5. Download every generated image to ./output/.
            var images = ComfyClient.ExtractImages(nodeOutputs);

            if (images.Count == 0)
            {
                Console.WriteLine("\n⚠ No images found in node outputs.");
            }
            else
            {
                foreach (var img in images)
                {
                    await client.DownloadImageAsync(img.Filename, img.Subfolder, img.Type);
                }
                Console.WriteLine($"\n✅ Done! {images.Count} image(s) downloaded to ./output/");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n✗ Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles the <see cref="WebSocketMonitor.PreviewImageReceived"/> event.
    ///
    /// Called synchronously on the WebSocket receive thread each time the server
    /// sends a binary preview frame.  The image bytes are available immediately
    /// via <paramref name="args"/>; the file at <c>args.SavePath</c> is written
    /// asynchronously in the background by <see cref="WebSocketMonitor"/>.
    /// </summary>
    private static void OnPreviewImageReceived(object? sender, PreviewReceivedEventArgs args)
    {
        string nodeInfo = args.Metadata.HasValue &&
                          args.Metadata.Value.TryGetProperty("node_id", out var nid)
            ? $" (node: {nid.GetString()})"
            : "";

        Console.WriteLine($"  📷 Preview #{args.Index} received{nodeInfo} — saving to {args.SavePath} …");
    }
}
