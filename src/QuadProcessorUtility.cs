using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuadSpriteProcessor
{
    public static class QuadProcessorUtility
    {
        #region Helpers

        public static bool AreDimensionsDivisibleByFour(int width, int height)
        {
            return width % 4 == 0 && height % 4 == 0;
        }

        public static TextureInfo GetTextureInfo(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);
            var info = new TextureInfo { Width = texture.width, Height = texture.height };
            Object.DestroyImmediate(texture);
            return info;
        }

        // Helper function to safely get a pixel from the array
        public static Color GetPixelSafe(Color[] pixels, int x, int y, int width, int height)
        {
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= width) x = width - 1;
            if (y >= height) y = height - 1;
            return pixels[y * width + x];
        }

        public static int CalculateDivisibleByFour(int dimension)
        {
            return dimension % 4 != 0 ? (int)Mathf.Ceil(dimension / 4f) * 4 : dimension;
        }

        #endregion

        #region Import size calculation

        public static ImportedTextureInfo GetImportedTextureInfo(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Failed to get TextureImporter for {assetPath}");
                return new ImportedTextureInfo(); // Return empty info
            }

            var sourceInfo = GetTextureInfo(assetPath);
            var maxSize = importer.maxTextureSize;

            // Calculate imported dimensions
            var (importedWidth, importedHeight) = CalculateImportedDimensions(
                sourceInfo.Width, sourceInfo.Height, maxSize);

            // Calculate quad-divisible dimensions
            var newImportedWidth = CalculateDivisibleByFour(importedWidth);
            var newImportedHeight = CalculateDivisibleByFour(importedHeight);

            // Calculate what the source dimensions should be to get quad-divisible imported dimensions
            var (newSourceWidth, newSourceHeight) = CalculateRequiredSourceDimensions(
                sourceInfo.Width, sourceInfo.Height, importedWidth, importedHeight,
                newImportedWidth, newImportedHeight);

            return new ImportedTextureInfo
            {
                SourceWidth = sourceInfo.Width,
                SourceHeight = sourceInfo.Height,
                ImportedWidth = importedWidth,
                ImportedHeight = importedHeight,
                NewImportedWidth = newImportedWidth,
                NewImportedHeight = newImportedHeight,
                NewSourceWidth = newSourceWidth,
                NewSourceHeight = newSourceHeight,
                NeedsProcessing = !AreDimensionsDivisibleByFour(importedWidth, importedHeight)
            };
        }

        public static (int width, int height) CalculateImportedDimensions(
            int sourceWidth, int sourceHeight, int maxSize)
        {
            if (sourceWidth <= maxSize && sourceHeight <= maxSize)
                return (sourceWidth, sourceHeight);

            var aspectRatio = (float)sourceWidth / sourceHeight;

            if (sourceWidth >= sourceHeight)
                return (maxSize, Mathf.RoundToInt(maxSize / aspectRatio));
            return (Mathf.RoundToInt(maxSize * aspectRatio), maxSize);
        }

        public static (int width, int height) CalculateRequiredSourceDimensions(
            int sourceWidth, int sourceHeight, int importedWidth, int importedHeight,
            int targetImportedWidth, int targetImportedHeight)
        {
            // Calculate ratios between target and current imported dimensions
            var widthRatio = (float)targetImportedWidth / importedWidth;
            var heightRatio = (float)targetImportedHeight / importedHeight;

            // Apply the same ratios to the source dimensions
            return (
                Mathf.RoundToInt(sourceWidth * widthRatio),
                Mathf.RoundToInt(sourceHeight * heightRatio)
            );
        }

        #endregion

        #region Modify original texture file

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
                for (var x = 0; x < newWidth; x++)
                {
                    var u = x / (float)(newWidth - 1);
                    var v = y / (float)(newHeight - 1);

                    var origX = Mathf.FloorToInt(u * (currentWidth - 1));
                    var origY = Mathf.FloorToInt(v * (currentHeight - 1));

                    newPixels[y * newWidth + x] =
                        GetPixelSafe(originalPixels, origX, origY,
                            currentWidth, currentHeight);
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
            catch (Exception e)
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

        #endregion
    }
}
