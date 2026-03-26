// Program.cs
// Entry point for the ComfyUI minimal API example.
//
// Usage:   dotnet run
// Requires: .NET 8+, ComfyUI running at http://127.0.0.1:8188
//   single-user: python main.py
//   multi-user:  python main.py --multi-user

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ComfyMinimalExample;

internal class Program
{
    private static async Task Main()
    {
        var config = new ComfyConfig
        {
            MultiUser = false,   // flip to true for --multi-user server
            UserId    = "alice", // only used in multi-user mode
        };

        Console.WriteLine("=== ComfyUI Minimal API Example ===");
        Console.WriteLine($"Mode    : {(config.MultiUser ? $"multi-user (user: {config.UserId})" : "single-user")}");
        Console.WriteLine($"ClientID: {config.ClientId}\n");

        using var httpClient = new HttpClient();

        var client  = new ComfyClient(config, httpClient);
        var builder = new WorkflowBuilder();
        var monitor = new WebSocketMonitor(config);

        // Subscribe before calling WaitForCompletionAsync.
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

    private static void OnPreviewImageReceived(object? sender, PreviewReceivedEventArgs args)
    {
        string nodeInfo = args.Metadata.HasValue &&
                          args.Metadata.Value.TryGetProperty("node_id", out var nid)
            ? $" (node: {nid.GetString()})"
            : "";

        Console.WriteLine($"  📷 Preview #{args.Index} received{nodeInfo} → saving to {args.SavePath}");
    }
}

