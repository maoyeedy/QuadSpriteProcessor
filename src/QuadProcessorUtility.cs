using UnityEngine;
using UnityEditor;
using System.IO;

namespace QuadSpriteProcessor
{
    public static class QuadProcessorUtility
    {
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

        // New methods added for importer max size consideration
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

            float aspectRatio = (float)sourceWidth / sourceHeight;

            if (sourceWidth >= sourceHeight)
                return (maxSize, Mathf.RoundToInt(maxSize / aspectRatio));
            else
                return (Mathf.RoundToInt(maxSize * aspectRatio), maxSize);
        }

        public static (int width, int height) CalculateRequiredSourceDimensions(
            int sourceWidth, int sourceHeight, int importedWidth, int importedHeight,
            int targetImportedWidth, int targetImportedHeight)
        {
            // Calculate ratios between target and current imported dimensions
            float widthRatio = (float)targetImportedWidth / importedWidth;
            float heightRatio = (float)targetImportedHeight / importedHeight;

            // Apply the same ratios to the source dimensions
            return (
                Mathf.RoundToInt(sourceWidth * widthRatio),
                Mathf.RoundToInt(sourceHeight * heightRatio)
            );
        }
    }
}
