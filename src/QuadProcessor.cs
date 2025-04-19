using UnityEngine;
using UnityEditor;
using System.IO;

namespace QuadSpriteProcessor
{
    public static class QuadProcessor
    {
        public static void ModifyTextureFile(string assetPath, int currentWidth, int currentHeight, int newWidth,
            int newHeight)
        {
            if (newWidth == currentWidth && newHeight == currentHeight) return;

            Texture2D texture = null;

            try
            {
                // Load texture from file
                texture = LoadTextureFromFile(assetPath);
                if (texture == null)
                    return;

                // Use actual dimensions from loaded texture
                currentWidth = texture.width;
                currentHeight = texture.height;

                // Store original pixels and format
                var originalPixels = texture.GetPixels();
                var originalFormat = texture.format;
                var hasMipMaps = texture.mipmapCount > 1;

                // Reinitialize clears all pixel data but preserves the texture object
                if (!texture.Reinitialize(newWidth, newHeight, originalFormat, hasMipMaps))
                {
                    Debug.LogError($"Failed to reinitialize texture: {assetPath}");
                    return;
                }

                // We need to manually scale the pixels since Reinitialize clears all pixel data
                var newPixels = new Color[newWidth * newHeight];

                for (var y = 0; y < newHeight; y++)
                {
                    for (var x = 0; x < newWidth; x++)
                    {
                        var u = x / (float)(newWidth - 1);
                        var v = y / (float)(newHeight - 1);

                        var origX = Mathf.FloorToInt(u * (currentWidth - 1));
                        var origY = Mathf.FloorToInt(v * (currentHeight - 1));

                        newPixels[y * newWidth + x] =
                            QuadProcessorUtility.GetPixelSafe(originalPixels, origX, origY,
                                currentWidth, currentHeight);
                    }
                }

                // Set the scaled pixels to the reinitialized texture
                texture.SetPixels(newPixels);
                texture.Apply();

                // Encode to file format
                var fileExtension = Path.GetExtension(assetPath).ToLower();
                var newBytes = EncodeTexture(texture, fileExtension);

                if (newBytes == null) return;

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
                if (texture != null) Object.DestroyImmediate(texture);
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
// #if UNITY_2021_3_OR_NEWER
                case ".tga":
                    return texture.EncodeToTGA();
                case ".exr":
                    return texture.EncodeToEXR();
// #endif
                default:
                    Debug.LogWarning($"Unsupported file format for encoding: {fileExtension}");
                    return null;
            }
        }
    }
}