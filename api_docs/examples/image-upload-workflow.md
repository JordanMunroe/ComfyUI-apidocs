# Image Upload and Image-to-Image Workflow

This example shows how to upload an image to the ComfyUI server and use it as the starting point for an image-to-image (img2img) workflow.

---

## Upload an Image

Images are uploaded via `POST /upload/image` using a multipart form-data request.

### JavaScript

```javascript
import { readFile } from 'node:fs/promises';

/**
 * Uploads a local image file to the ComfyUI server.
 *
 * @param {string} localPath - Path to the local image file.
 * @param {string} [subfolder] - Optional subfolder under ComfyUI's input/ directory.
 * @returns {Promise<{ name: string, subfolder: string, type: string }>}
 *   The server's descriptor for the uploaded file.
 */
async function uploadImage(localPath, subfolder = '') {
  const fileBuffer = await readFile(localPath);
  const filename   = localPath.split('/').pop();

  const formData = new FormData();
  formData.append('image',     new Blob([fileBuffer], { type: 'image/png' }), filename);
  formData.append('type',      'input');
  formData.append('subfolder', subfolder);
  formData.append('overwrite', 'true');

  const response = await fetch('http://127.0.0.1:8188/upload/image', {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) throw new Error(`Upload failed: HTTP ${response.status}`);
  const result = await response.json();
  console.log(`Uploaded: ${result.name}`);
  return result;
}
```

### C\#

```csharp
/// <summary>
/// Uploads a local image file to the ComfyUI server.
/// </summary>
/// <param name="client">Shared HttpClient instance.</param>
/// <param name="localPath">Path to the local image file.</param>
/// <param name="subfolder">Optional subfolder under ComfyUI's input/ directory.</param>
/// <returns>JSON descriptor with <c>name</c>, <c>subfolder</c>, and <c>type</c>.</returns>
static async Task<JsonElement> UploadImageAsync(
    HttpClient client, string localPath, string subfolder = "")
{
    byte[] fileBytes = await File.ReadAllBytesAsync(localPath);
    string filename  = Path.GetFileName(localPath);

    using var content     = new MultipartFormDataContent();
    var       fileContent = new ByteArrayContent(fileBytes);
    fileContent.Headers.ContentType =
        new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

    content.Add(fileContent, "image", filename);
    content.Add(new StringContent("input"),     "type");
    content.Add(new StringContent(subfolder),   "subfolder");
    content.Add(new StringContent("true"),      "overwrite");

    var response = await client.PostAsync("http://127.0.0.1:8188/upload/image", content);
    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
    Console.WriteLine($"Uploaded: {result.GetProperty("name").GetString()}");
    return result;
}
```

---

## Build an img2img Workflow

Use the uploaded image name in a `LoadImage` node, then encode it through the VAE before passing it to KSampler with `denoise < 1.0`.

```javascript
/**
 * Builds an img2img workflow that modifies an uploaded image.
 *
 * @param {string} uploadedName - The filename returned by uploadImage().
 * @param {string} prompt - Text describing the desired output.
 * @param {number} [denoise=0.65] - Denoising strength (0.0 = no change, 1.0 = full).
 */
function buildImg2ImgWorkflow(uploadedName, prompt, denoise = 0.65) {
  return {
    '1': {
      class_type: 'CheckpointLoaderSimple',
      inputs: { ckpt_name: 'sd_xl_base_1.0.safetensors' },
    },
    '2': {
      // Load the previously uploaded image
      class_type: 'LoadImage',
      inputs: { image: uploadedName },
    },
    '3': {
      // Encode the loaded image into latent space
      class_type: 'VAEEncode',
      inputs: { pixels: ['2', 0], vae: ['1', 2] },
    },
    '4': {
      class_type: 'CLIPTextEncode',
      inputs: { text: prompt, clip: ['1', 1] },
    },
    '5': {
      class_type: 'CLIPTextEncode',
      inputs: { text: 'blurry, low quality', clip: ['1', 1] },
    },
    '6': {
      class_type: 'KSampler',
      inputs: {
        seed:         Math.floor(Math.random() * 2 ** 32),
        steps:        20,
        cfg:          7.0,
        sampler_name: 'euler',
        scheduler:    'normal',
        denoise,               // < 1.0 keeps some of the original image structure
        model:        ['1', 0],
        positive:     ['4', 0],
        negative:     ['5', 0],
        latent_image: ['3', 0], // encoded input image
      },
    },
    '7': {
      class_type: 'VAEDecode',
      inputs: { samples: ['6', 0], vae: ['1', 2] },
    },
    '8': {
      class_type: 'SaveImage',
      inputs: { filename_prefix: 'img2img', images: ['7', 0] },
    },
  };
}
```

---

## Full Flow

```javascript
import { randomUUID } from 'node:crypto';

const CLIENT_ID = randomUUID();

// 1. Upload the source image
const uploaded = await uploadImage('./input.png');

// 2. Build the img2img workflow
const workflow = buildImg2ImgWorkflow(
  uploaded.name,
  'a futuristic cityscape at night, neon lights, rain reflections',
  0.7, // 70% denoising — keeps structure, changes style
);

// 3. Queue the workflow
const response = await fetch('http://127.0.0.1:8188/prompt', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ prompt: workflow, client_id: CLIENT_ID }),
});

const { prompt_id } = await response.json();
console.log('Queued:', prompt_id);

// 4. Monitor via WebSocket (see websocket-monitoring.md)
```

---

## Denoising Strength Guide

| `denoise` | Effect |
|-----------|--------|
| `0.0` | No change — output = input |
| `0.3` | Subtle style change, strong structure preservation |
| `0.5–0.7` | Balanced transformation |
| `0.9–1.0` | Heavy transformation, loses input structure |

---

## See Also

- [Minimal API Example](./minimal-api-example.md) — Complete runnable example
- [Simple Workflow Execution](./simple-workflow-execution.md) — Workflow node anatomy
- [Download Generated Images](./download-outputs.md) — Retrieve the output
