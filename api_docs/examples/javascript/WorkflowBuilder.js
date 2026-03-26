/**
 * @file WorkflowBuilder.js
 * @description Builds ComfyUI workflow graphs for common use cases.
 *
 * A workflow is a JSON object whose keys are node IDs and whose values
 * describe each node's type (`class_type`) and inputs.  Node outputs are
 * referenced with the tuple `["node_id", output_index]`.
 */

/**
 * Constructs ComfyUI workflow graphs.
 *
 * Each builder method returns a plain object that can be passed directly to
 * {@link ComfyClient#queueWorkflow} as the `workflow` argument.
 *
 * @example
 * const builder = new WorkflowBuilder();
 * const workflow = builder.buildTxt2Img('a sunset over mountains');
 */
export class WorkflowBuilder {
  /**
   * Builds a minimal Stable Diffusion text-to-image workflow graph.
   *
   * The pipeline:
   * ```
   * CheckpointLoader ─► CLIPTextEncode (positive prompt)
   *                  ─► CLIPTextEncode (negative prompt)
   * EmptyLatentImage ─►
   *                      KSampler ─► VAEDecode ─► SaveImage
   * ```
   *
   * Node output reference format: `["node_id", output_index]`
   * | Node | Index | Type |
   * |------|-------|------|
   * | "1" CheckpointLoaderSimple | 0 | MODEL |
   * | "1" CheckpointLoaderSimple | 1 | CLIP  |
   * | "1" CheckpointLoaderSimple | 2 | VAE   |
   *
   * @param {string} [positivePrompt] - Text describing what to generate.
   * @param {string} [negativePrompt] - Text describing what to avoid.
   * @param {string} [checkpointName] - Model filename in ComfyUI's models/ directory.
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
