// WorkflowBuilder.cs
// Constructs ComfyUI workflow graphs for common generation tasks.

using System;
using System.Collections.Generic;

namespace ComfyMinimalExample;

/// <summary>
/// Constructs ComfyUI workflow graphs for common image generation tasks.
///
/// A workflow is a dictionary whose keys are node IDs and whose values
/// describe each node's type (<c>class_type</c>) and named inputs.
/// Outputs of one node are referenced in another node's inputs using the
/// tuple <c>["node_id", output_index]</c>.
///
/// Each builder method returns a plain <see cref="object"/> that can be
/// serialised directly by <see cref="System.Text.Json.JsonSerializer"/> and
/// submitted to <see cref="ComfyClient.QueueWorkflowAsync"/>.
/// </summary>
/// <example>
/// <code>
/// var builder  = new WorkflowBuilder();
/// var workflow = builder.BuildTxt2Img("a sunset over mountains");
/// string promptId = await client.QueueWorkflowAsync(workflow);
/// </code>
/// </example>
public class WorkflowBuilder
{
    /// <summary>
    /// Builds a minimal Stable Diffusion text-to-image workflow graph.
    /// </summary>
    /// <remarks>
    /// Pipeline:
    /// <list type="bullet">
    ///   <item><description>"1" CheckpointLoaderSimple — loads model, CLIP encoder, and VAE</description></item>
    ///   <item><description>"2" CLIPTextEncode         — encodes the positive text prompt</description></item>
    ///   <item><description>"3" CLIPTextEncode         — encodes the negative text prompt</description></item>
    ///   <item><description>"4" EmptyLatentImage       — creates a blank latent canvas</description></item>
    ///   <item><description>"5" KSampler               — runs the diffusion sampling loop</description></item>
    ///   <item><description>"6" VAEDecode              — converts the latent tensor to pixel space</description></item>
    ///   <item><description>"7" SaveImage              — writes the final PNG to disk on the server</description></item>
    /// </list>
    ///
    /// Node output reference format: <c>["node_id", output_index]</c>
    /// <list type="table">
    ///   <listheader><term>Node</term><description>Index → Type</description></listheader>
    ///   <item><term>"1" CheckpointLoaderSimple</term><description>0 → MODEL, 1 → CLIP, 2 → VAE</description></item>
    /// </list>
    /// </remarks>
    /// <param name="positivePrompt">Text describing what to generate.</param>
    /// <param name="negativePrompt">Text describing what to avoid.</param>
    /// <param name="checkpointName">Model filename inside ComfyUI's <c>models/</c> directory.</param>
    /// <returns>A workflow graph object ready to pass to <see cref="ComfyClient.QueueWorkflowAsync"/>.</returns>
    public object BuildTxt2Img(
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
                    clip = new object[] { "1", 1 }, // CLIP output from CheckpointLoaderSimple
                },
            },
            ["3"] = new
            {
                class_type = "CLIPTextEncode",
                inputs = new
                {
                    text = negativePrompt,
                    clip = new object[] { "1", 1 }, // CLIP output from CheckpointLoaderSimple
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
                    cfg = 7.0,                              // classifier-free guidance scale
                    sampler_name = "euler",
                    scheduler = "normal",
                    denoise = 1.0,                          // 1.0 = full denoising (text-to-image)
                    model        = new object[] { "1", 0 }, // MODEL from CheckpointLoaderSimple
                    positive     = new object[] { "2", 0 }, // CONDITIONING from positive CLIPTextEncode
                    negative     = new object[] { "3", 0 }, // CONDITIONING from negative CLIPTextEncode
                    latent_image = new object[] { "4", 0 }, // LATENT from EmptyLatentImage
                },
            },
            ["6"] = new
            {
                class_type = "VAEDecode",
                inputs = new
                {
                    samples = new object[] { "5", 0 }, // LATENT from KSampler
                    vae     = new object[] { "1", 2 }, // VAE from CheckpointLoaderSimple
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
}
