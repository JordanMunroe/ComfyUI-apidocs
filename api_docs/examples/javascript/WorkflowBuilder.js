/**
 * @file WorkflowBuilder.js
 * @description Builds ComfyUI workflow graphs.
 */

/** Constructs ComfyUI workflow graphs. */
export class WorkflowBuilder {
  /**
   * Builds a minimal Stable Diffusion text-to-image workflow.
   *
   * Pipeline: CheckpointLoader → CLIPTextEncode (×2) + EmptyLatentImage
   *           → KSampler → VAEDecode → SaveImage
   *
   * @param {string} [positivePrompt]
   * @param {string} [negativePrompt]
   * @param {string} [checkpointName]
   * @returns {object} Workflow graph ready to submit to `POST /prompt`.
   */
  buildTxt2Img(
    positivePrompt = 'a beautiful sunset over mountains, golden hour, photorealistic',
    negativePrompt = 'blurry, low quality, watermark',
    checkpointName = 'sd_xl_base_1.0.safetensors',
  ) {
    return {
      '1': {
        class_type: 'CheckpointLoaderSimple',
        inputs: { ckpt_name: checkpointName },
      },
      '2': {
        class_type: 'CLIPTextEncode',
        inputs: {
          text: positivePrompt,
          clip: ['1', 1], // CLIP output from CheckpointLoaderSimple
        },
      },
      '3': {
        class_type: 'CLIPTextEncode',
        inputs: {
          text: negativePrompt,
          clip: ['1', 1], // CLIP output from CheckpointLoaderSimple
        },
      },
      '4': {
        class_type: 'EmptyLatentImage',
        inputs: { width: 512, height: 512, batch_size: 1 },
      },
      '5': {
        class_type: 'KSampler',
        inputs: {
          seed: Math.floor(Math.random() * 2 ** 32), // random seed each run
          steps: 20,
          cfg: 7.0,           // classifier-free guidance scale
          sampler_name: 'euler',
          scheduler: 'normal',
          denoise: 1.0,       // 1.0 = full denoising (text-to-image)
          model:        ['1', 0], // MODEL from CheckpointLoaderSimple
          positive:     ['2', 0], // CONDITIONING from positive CLIPTextEncode
          negative:     ['3', 0], // CONDITIONING from negative CLIPTextEncode
          latent_image: ['4', 0], // LATENT from EmptyLatentImage
        },
      },
      '6': {
        class_type: 'VAEDecode',
        inputs: {
          samples: ['5', 0], // LATENT from KSampler
          vae:     ['1', 2], // VAE from CheckpointLoaderSimple
        },
      },
      '7': {
        class_type: 'SaveImage',
        inputs: {
          filename_prefix: 'minimal_example',
          images: ['6', 0], // IMAGE from VAEDecode
        },
      },
    };
  }
}

