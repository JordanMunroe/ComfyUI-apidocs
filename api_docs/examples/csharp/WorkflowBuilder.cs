// WorkflowBuilder.cs
// Constructs ComfyUI workflow graphs for common generation tasks.

using System;
using System.Collections.Generic;

namespace ComfyMinimalExample;

/// <summary>Builds ComfyUI workflow graphs.</summary>
public class WorkflowBuilder
{
    /// <summary>
    /// Builds a minimal Stable Diffusion text-to-image workflow.
    ///
    /// Pipeline: CheckpointLoader → CLIPTextEncode (×2) + EmptyLatentImage
    ///           → KSampler → VAEDecode → SaveImage
    /// </summary>
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

