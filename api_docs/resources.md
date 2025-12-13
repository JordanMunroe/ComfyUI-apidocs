# Resource Management

Resource management endpoints provide access to ComfyUI's file-based resources including models, embeddings, and images. These endpoints allow you to discover available resources, upload new files, retrieve metadata, and access generated outputs. Understanding resource management is essential for building UIs that help users select models, manage their asset library, or display generated content.

## Models

Models are the AI components that power ComfyUI's generation capabilities. ComfyUI supports various model types including checkpoints, VAEs, LoRAs, controlnets, upscale models, and more. Models are organized into folders by type, and can be located in multiple directories. The model endpoints help you discover what models are available, view their metadata, and even preview models that support it (like safetensors files with embedded previews).

### List Model Types

**Endpoint:** `GET /models`

**Response:**
```json
[
  "checkpoints",
  "vae",
  "loras",
  "controlnet",
  "clip",
  "upscale_models",
  "embeddings",
  ...
]
```

### List Models in Folder

**Endpoint:** `GET /models/{folder}`

**Parameters:**
- `folder`: Model folder type (e.g., "checkpoints", "loras")

**Response:**
```json
[
  "model1.safetensors",
  "model2.ckpt",
  "subfolder/model3.safetensors"
]
```

### Get Model Metadata (Safetensors)

**Endpoint:** `GET /view_metadata/{folder_name}`

**Query Parameters:**
- `filename`: Name of the safetensors file

**Response:**
```json
{
  "modelspec.architecture": "stable-diffusion-xl-v1-base",
  "modelspec.title": "Model Name",
  "modelspec.description": "Model description",
  ...
}
```

**Note:** Only works with `.safetensors` files.

### Experimental: Get Model Folders with Paths

**Endpoint:** `GET /experiment/models`

**Response:**
```json
[
  {
    "name": "checkpoints",
    "folders": ["/path/to/models/checkpoints", "/another/path"]
  },
  ...
]
```

### Experimental: Get Model Files with Details

**Endpoint:** `GET /experiment/models/{folder}`

**Response:**
```json
[
  {
    "name": "model.safetensors",
    "path": "subfolder/model.safetensors",
    "folder_index": 0,
    "size": 2147483648,
    "modified": 1701234567.89
  }
]
```

### Experimental: Get Model Preview

**Endpoint:** `GET /experiment/models/preview/{folder}/{path_index}/{filename}`

**Parameters:**
- `folder`: Model folder type
- `path_index`: Index of the folder path
- `filename`: Model filename (can include subfolders)

**Response:** Image file (WEBP format)

---

## Embeddings

Embeddings (also known as textual inversions) are learned representations that can be used in prompts to achieve specific styles or subjects. They're typically small files that modify how the model interprets certain tokens. The embeddings endpoint lists all available embeddings in your ComfyUI installation, allowing users to discover and use them in their text prompts.

### List Embeddings

**Endpoint:** `GET /embeddings`

**Response:**
```json
[
  "embedding1",
  "embedding2",
  "subfolder/embedding3"
]
```

**Note:** Returns filenames without extensions.

---

## Images

Image management is crucial for workflows that require input images (like img2img, inpainting, or controlnet workflows) or for retrieving generated outputs. ComfyUI maintains separate directories for different image types: input (user uploads), output (generated results), and temp (intermediate/preview images). The image endpoints support uploading, viewing with on-the-fly format conversion, and managing masks for inpainting workflows.

### Upload Image

**Endpoint:** `POST /upload/image`

**Content-Type:** `multipart/form-data`

**Form Parameters:**
- `image`: Image file (required)
- `subfolder` (optional): Subfolder within the upload directory
- `type` (optional): Directory type ("input", "temp", "output") - default is "input"
- `overwrite` (optional): "true" or "1" to overwrite existing files

**Response:**
```json
{
  "name": "image.png",
  "subfolder": "subfolder",
  "type": "input"
}
```

**Notes:** 
- If file exists and overwrite is false, filename will be incremented (e.g., "image (1).png")
- Duplicate images (same content hash) are not saved again - the existing filename is returned
- Supported formats include PNG, JPEG, WEBP, and other common image formats

### Upload Mask

**Endpoint:** `POST /upload/mask`

**Content-Type:** `multipart/form-data`

**Form Parameters:**
- `image`: Mask image file (required)
- `original_ref`: JSON string with reference to original image
  ```json
  {
    "filename": "original.png",
    "type": "output",
    "subfolder": ""
  }
  ```
- `subfolder` (optional): Subfolder within the upload directory
- `type` (optional): Directory type ("input", "temp", "output")

**Response:** Same as upload image

**Note:** This endpoint applies the uploaded mask as an alpha channel to the referenced original image, creating a composite image.

### View Image

**Endpoint:** `GET /view`

**Query Parameters:**
- `filename`: Image filename (required, can include annotation like `image.png [output]`)
- `type` (optional): Directory type ("output", "input", "temp") - default is "output"
- `subfolder` (optional): Subfolder path within the directory
- `preview` (optional): Format for preview with optional quality (e.g., "webp", "jpeg", "webp;90", "jpeg;80")
  - Supported formats: "webp", "jpeg"
  - Quality range: 1-100 (default: 90)
- `channel` (optional): Channel to extract from image
  - "rgb" - RGB channels only
  - "a" - Alpha channel only (as grayscale)
  - "rgba" - All channels including alpha

**Response:** Image file (binary data)

**Examples:**
- `/view?filename=image.png&type=output`
- `/view?filename=image.png&preview=webp;80`
- `/view?filename=image.png&channel=a` (alpha channel only)
- `/view?filename=subfolder/image.png&subfolder=myfolder&type=input`

**Note:** The preview parameter allows for on-the-fly image conversion and compression without modifying the original file.

---

## Extensions

Extensions are JavaScript modules that enhance ComfyUI's frontend functionality. They can add new UI components, modify node behavior, integrate with external services, or provide custom visualizations. Custom nodes often include their own web extensions to provide specialized interfaces. The extensions endpoint lists all available JavaScript files that will be loaded by the frontend.

**Endpoint:** `GET /extensions`

**Response:**
```json
[
  "/extensions/extension1/script.js",
  "/extensions/extension2/module.js",
  "/extensions/custom_nodes.node_name/script.js"
]
```

**Note:** Returns JavaScript files from both the core extensions directory and custom node web extensions. Paths are relative to the web root and URL-encoded for custom node paths.
