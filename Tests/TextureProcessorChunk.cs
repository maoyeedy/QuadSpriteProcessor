using UnityEngine;
using UnityEditor;
using System.IO;

namespace QuadSpriteProcessor
{
    public static class TextureProcessorChunk
    {
        // Define the chunk size for processing (can be adjusted based on memory requirements)
        private const int ChunkSize = 128; // Process 128 rows at a time

        public static void ModifyTextureFile(string assetPath, int currentWidth, int currentHeight, int newWidth,
            int newHeight)
        {
            if (newWidth == currentWidth && newHeight == currentHeight) return;

            Texture2D sourceTexture = null;
            Texture2D destTexture = null;

            try
            {
                // Load source texture
                sourceTexture = LoadTextureFromFile(assetPath);
                if (sourceTexture == null)
                    return;

                // Use actual dimensions from loaded texture
                currentWidth = sourceTexture.width;
                currentHeight = sourceTexture.height;

                // Get original format and mipmap settings
                var originalFormat = sourceTexture.format;
                var hasMipMaps = sourceTexture.mipmapCount > 1;

                // Create destination texture with the new dimensions
                destTexture = new Texture2D(newWidth, newHeight, originalFormat, hasMipMaps);

                // Calculate scale factors
                var scaleX = (float)currentWidth / newWidth;
                var scaleY = (float)currentHeight / newHeight;

                // Process the texture in chunks to reduce memory usage
                for (var chunkStart = 0; chunkStart < newHeight; chunkStart += ChunkSize)
                {
                    // Calculate the current chunk height
                    var chunkHeight = Mathf.Min(ChunkSize, newHeight - chunkStart);
                    var chunkPixels = new Color[newWidth * chunkHeight];

                    // Calculate the region in the source texture that corresponds to this chunk
                    var sourceStartY = Mathf.FloorToInt(chunkStart * scaleY);
                    var sourceEndY = Mathf.CeilToInt((chunkStart + chunkHeight) * scaleY);
                    var sourceHeight = Mathf.Min(sourceEndY - sourceStartY, currentHeight - sourceStartY);

                    // Get the source pixels for this region in one go
                    var sourcePixels = sourceTexture.GetPixels(0, sourceStartY, currentWidth, sourceHeight);

                    for (var y = 0; y < chunkHeight; y++)
                    {
                        var targetY = chunkStart + y;

                        for (var x = 0; x < newWidth; x++)
                        {
                            // Calculate the corresponding position in the source texture
                            var sourceX = Mathf.FloorToInt(x * scaleX);
                            var sourceY = Mathf.FloorToInt(targetY * scaleY) - sourceStartY; // Adjust for the offset

                            // Ensure we stay within bounds
                            sourceX = Mathf.Min(sourceX, currentWidth - 1);
                            sourceY = Mathf.Clamp(sourceY, 0, sourceHeight - 1);

                            // Get the pixel from the source pixels array
                            var pixelColor = sourcePixels[sourceY * currentWidth + sourceX];

                            // Set the pixel in the chunk
                            chunkPixels[y * newWidth + x] = pixelColor;
                        }
                    }

                    // Apply this chunk to the destination texture
                    destTexture.SetPixels(0, chunkStart, newWidth, chunkHeight, chunkPixels);
                }

                // Apply all changes
                destTexture.Apply();

                // Encode to file format
                var fileExtension = Path.GetExtension(assetPath).ToLower();
                var newBytes = EncodeTexture(destTexture, fileExtension);

                if (newBytes == null) return;

                // Overwrite file
                File.WriteAllBytes(assetPath, newBytes);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                Debug.Log($"Resized: '{assetPath}' from {currentWidth}x{currentHeight} to {newWidth}x{newHeight}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error modifying texture {assetPath}: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                if (sourceTexture != null) Object.DestroyImmediate(sourceTexture);
                if (destTexture != null) Object.DestroyImmediate(destTexture);
            }
        }

        private static Texture2D LoadTextureFromFile(string assetPath)
        {
            var bytes = File.ReadAllBytes(assetPath);
            var texture = new Texture2D(2, 2);

            if (!texture.LoadImage(bytes))
            {
                Debug.LogError($"Failed to load image data for: {assetPath}");
                Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        // fileExtension already lowercase before input
        private static byte[] EncodeTexture(Texture2D texture, string fileExtension)
        {
            switch (fileExtension)
            {
                case ".png":
                    return texture.EncodeToPNG();
                case ".jpg":
                case ".jpeg":
                    return texture.EncodeToJPG();
                // case ".tga":
                    // return texture.EncodeToTGA();
                // case ".exr":
                    // return texture.EncodeToEXR();
                default:
                    Debug.LogWarning($"Unsupported file format for encoding: {fileExtension}");
                    return null;
            }
        }
    }
}