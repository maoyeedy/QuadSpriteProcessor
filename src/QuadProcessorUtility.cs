using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuadSpriteProcessor
{
    public class ScanOptions
    {
        public string FolderPath { get; set; }
        public bool IncludeSubfolders { get; set; }
        public bool ConsiderImporterMaxSize { get; set; }
    }

    public class ProcessOptions
    {
        public bool ConsiderImporterMaxSize { get; set; }
    }

    public class ProcessingResult
    {
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
    }

    public delegate void ProgressCallback(string fileName, int current, int total);

    public static class QuadProcessorUtility
    {
        #region Core helpers

        public static bool AreDimensionsDivisibleByFour(int width, int height)
        {
            return width % 4 == 0 && height % 4 == 0;
        }

        public static int CalculateDivisibleByFour(int dimension)
        {
            if (dimension % 4 == 0)
            {
                return dimension;
            }

            return (int)Math.Ceiling(dimension / 4.0) * 4;
        }

        #endregion

        #region Texture information

        public static TextureInfo GetTextureInfo(string path)
        {
            var texture = LoadRawTextureFromFile(path);
            if (texture is null)
            {
                return new TextureInfo();
            }

            var info = new TextureInfo { Width = texture.width, Height = texture.height };
            Object.DestroyImmediate(texture);
            return info;
        }

        private static Texture2D LoadRawTextureFromFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2);
                if (!texture.LoadImage(bytes))
                {
                    Object.DestroyImmediate(texture);
                    return null;
                }

                return texture;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading texture from {path}: {e.Message}");
                return null;
            }
        }

        // Helper function to safely get a pixel from the array
        public static Color GetPixelSafe(Color[] pixels, int x, int y, int width, int height)
        {
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);
            return pixels[y * width + x];
        }

        #endregion

        #region Import size calculation

        public static ImportedTextureInfo GetImportedTextureInfo(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer is null)
            {
                Debug.LogWarning($"Failed to get TextureImporter for {assetPath}");
                return new ImportedTextureInfo(); // Return empty info
            }

            var sourceInfo = GetTextureInfo(assetPath);
            var maxSize = importer.maxTextureSize;

            // Calculate imported dimensions
            var (importedWidth, importedHeight) = CalculateImportedDimensions(
                sourceInfo.Width, sourceInfo.Height, maxSize);

            // Check if imported dimensions need processing
            var needsProcessing = !AreDimensionsDivisibleByFour(importedWidth, importedHeight);
            if (!needsProcessing)
            {
                return new ImportedTextureInfo
                {
                    SourceWidth = sourceInfo.Width,
                    SourceHeight = sourceInfo.Height,
                    ImportedWidth = importedWidth,
                    ImportedHeight = importedHeight,
                    NewImportedWidth = importedWidth,
                    NewImportedHeight = importedHeight,
                    NewSourceWidth = sourceInfo.Width,
                    NewSourceHeight = sourceInfo.Height,
                    NeedsProcessing = false
                };
            }

            // Calculate quad-divisible dimensions
            var newImportedWidth = CalculateDivisibleByFour(importedWidth);
            var newImportedHeight = CalculateDivisibleByFour(importedHeight);

            // Calculate the exact source dimensions needed to get quad-divisible imported dimensions
            var (newSourceWidth, newSourceHeight) = CalculateExactSourceDimensions(
                sourceInfo.Width, sourceInfo.Height,
                maxSize,
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
                NeedsProcessing = true
            };
        }

        public static (int width, int height) CalculateImportedDimensions(
            int sourceWidth, int sourceHeight, int maxSize)
        {
            if (sourceWidth <= maxSize && sourceHeight <= maxSize)
            {
                return (sourceWidth, sourceHeight);
            }

            var aspectRatio = (float)sourceWidth / sourceHeight;
            if (sourceWidth >= sourceHeight)
            {
                return (maxSize, Mathf.RoundToInt(maxSize / aspectRatio));
            }

            return (Mathf.RoundToInt(maxSize * aspectRatio), maxSize);
        }

        public static (int width, int height) CalculateExactSourceDimensions(
            int sourceWidth, int sourceHeight,
            int maxSize,
            int targetImportedWidth, int targetImportedHeight)
        {
            if (!IsScaledByImporter(sourceWidth, sourceHeight, maxSize))
            {
                return HandleUnscaledDimensions(targetImportedWidth, targetImportedHeight);
            }

            if (IsWidthConstrained(sourceWidth, sourceHeight))
            {
                return CalculateWidthConstrainedDimensions(sourceWidth, sourceHeight, maxSize, targetImportedWidth, targetImportedHeight);
            }
            else
            {
                return CalculateHeightConstrainedDimensions(sourceWidth, sourceHeight, maxSize, targetImportedWidth, targetImportedHeight);
            }
        }

        private static bool IsScaledByImporter(int width, int height, int maxSize)
        {
            return width > maxSize || height > maxSize;
        }

        private static (int width, int height) HandleUnscaledDimensions(int targetWidth, int targetHeight)
        {
            // Debug.LogWarning($"Texture is not scaled by importer. Using original dimensions: {targetWidth}x{targetHeight}");
            return (targetWidth, targetHeight);
        }

        private static bool IsWidthConstrained(int width, int height)
        {
            return width >= height;
        }

        private static (int width, int height) CalculateWidthConstrainedDimensions(
            int sourceWidth, int sourceHeight, int maxSize,
            int targetImportedWidth, int targetImportedHeight)
        {
            var scale = (float)maxSize / sourceWidth;
            var scaledHeight = sourceHeight * scale;

            // Calculate exact source dimensions
            var exactSourceWidth = Mathf.RoundToInt(sourceWidth * ((float)targetImportedWidth / maxSize));
            var exactSourceHeight = Mathf.RoundToInt(sourceHeight * ((float)targetImportedHeight / scaledHeight));

            return (exactSourceWidth, exactSourceHeight);
        }

        private static (int width, int height) CalculateHeightConstrainedDimensions(
            int sourceWidth, int sourceHeight, int maxSize,
            int targetImportedWidth, int targetImportedHeight)
        {
            var scale = (float)maxSize / sourceHeight;
            var scaledWidth = sourceWidth * scale;

            // Calculate exact source dimensions
            var exactSourceWidth = Mathf.RoundToInt(sourceWidth * ((float)targetImportedWidth / scaledWidth));
            var exactSourceHeight = Mathf.RoundToInt(sourceHeight * ((float)targetImportedHeight / maxSize));

            return (exactSourceWidth, exactSourceHeight);
        }

        #endregion

        #region Scanning

        public static List<TextureAsset> ScanFolderForNonQuadTextures(
            ScanOptions options,
            ProgressCallback progressCallback = null)
        {
            var textureFiles = FindTextureFiles(options.FolderPath, options.IncludeSubfolders);
            return ScanTextureFiles(textureFiles, options.ConsiderImporterMaxSize, progressCallback);
        }

        private static List<string> FindTextureFiles(string folderPath, bool includeSubfolders)
        {
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var fileExtensions = new[] { "*.png", "*.jpg", "*.jpeg" };
            var files = new List<string>();
            foreach (var ext in fileExtensions)
            {
                files.AddRange(Directory.GetFiles(folderPath, ext, searchOption));
            }

            return files;
        }

        private static List<TextureAsset> ScanTextureFiles(
            List<string> fileList,
            bool considerImporterMaxSize,
            ProgressCallback progressCallback)
        {
            var result = new List<TextureAsset>();
            var fileCount = fileList.Count;
            var processedCount = 0;
            foreach (var file in fileList)
            {
                processedCount++;
                var fileName = Path.GetFileName(file);
                progressCallback?.Invoke(fileName, processedCount, fileCount);
                if (ShouldSkipFile(file))
                {
                    continue;
                }

                try
                {
                    var textureAsset = AnalyzeTextureFile(file, considerImporterMaxSize);
                    if (textureAsset is not null)
                    {
                        result.Add(textureAsset);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error reading texture {file}: {e.Message}");
                }
            }

            return result;
        }

        private static bool ShouldSkipFile(string filePath)
        {
            var loweredPath = filePath.ToLower();
            return loweredPath.Contains("assets\\plugins") ||
                   loweredPath.Contains("assets\\samples") ||
                   loweredPath.Contains("assets\\editor") ||
                   loweredPath.Contains("~");
        }

        private static TextureAsset AnalyzeTextureFile(string filePath, bool considerImporterMaxSize)
        {
            if (considerImporterMaxSize)
            {
                return AnalyzeImportedTexture(filePath);
            }
            else
            {
                return AnalyzeActualTexture(filePath);
            }
        }

        private static TextureAsset AnalyzeActualTexture(string filePath)
        {
            var textureInfo = GetTextureInfo(filePath);
            if (AreDimensionsDivisibleByFour(textureInfo.Width, textureInfo.Height))
            {
                return null;
            }

            var newWidth = CalculateDivisibleByFour(textureInfo.Width);
            var newHeight = CalculateDivisibleByFour(textureInfo.Height);
            return new TextureAsset
            {
                Path = filePath,
                CurrentWidth = textureInfo.Width,
                CurrentHeight = textureInfo.Height,
                NewWidth = newWidth,
                NewHeight = newHeight,
                Selected = true
            };
        }

        private static TextureAsset AnalyzeImportedTexture(string filePath)
        {
            var importedInfo = GetImportedTextureInfo(filePath);
            if (!importedInfo.NeedsProcessing)
            {
                return null;
            }

            return new TextureAsset
            {
                Path = filePath,
                CurrentWidth = importedInfo.ImportedWidth,
                CurrentHeight = importedInfo.ImportedHeight,
                NewWidth = importedInfo.NewImportedWidth,
                NewHeight = importedInfo.NewImportedHeight,
                SourceWidth = importedInfo.SourceWidth,
                SourceHeight = importedInfo.SourceHeight,
                NewSourceWidth = importedInfo.NewSourceWidth,
                NewSourceHeight = importedInfo.NewSourceHeight,
                Selected = true
            };
        }

        #endregion

        #region Processing

        public static ProcessingResult ProcessTextures(
            List<TextureAsset> textures,
            ProcessOptions options,
            ProgressCallback progressCallback = null)
        {
            var result = new ProcessingResult();
            var totalCount = textures.Count;
            for (var i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                result.Total++;
                var fileName = Path.GetFileName(texture.Path);
                progressCallback?.Invoke(fileName, i + 1, totalCount);
                try
                {
                    ProcessSingleTexture(texture, options.ConsiderImporterMaxSize);
                    result.Succeeded++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to process {texture.Path}: {e.Message}");
                    result.Failed++;
                }
            }

            return result;
        }

        private static void ProcessSingleTexture(TextureAsset texture, bool considerImporterMaxSize)
        {
            if (considerImporterMaxSize)
            {
                // Make a direct change to the source texture to the correct dimensions
                ModifyTextureFileDirectly(
                    texture.Path,
                    texture.NewSourceWidth,
                    texture.NewSourceHeight);
            }
            else
            {
                ModifyTextureFileDirectly(
                    texture.Path,
                    texture.NewWidth,
                    texture.NewHeight);
            }
        }

        #endregion

        #region Modify texture file

        public static void ModifyTextureFileDirectly(
            string assetPath,
            int targetWidth,
            int targetHeight)
        {
            Texture2D texture = null;
            try
            {
                texture = LoadTextureFromFile(assetPath);
                if (texture is null)
                {
                    return;
                }

                var currentWidth = texture.width;
                var currentHeight = texture.height;

                if (currentWidth == targetWidth && currentHeight == targetHeight)
                {
                    Debug.Log($"No resize needed for: '{assetPath}' - already at target size {targetWidth}x{targetHeight}");
                    return;
                }

                // Perform direct resize to target dimensions
                if (!ResizeTexture(texture, currentWidth, currentHeight, targetWidth, targetHeight))
                {
                    return;
                }

                if (!SaveTextureToFile(texture, assetPath))
                {
                    return;
                }

                Debug.Log($"Resized: '{assetPath}' from {currentWidth}x{currentHeight} to {targetWidth}x{targetHeight}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error modifying texture {assetPath}: {e.Message}\n{e.StackTrace}");
                throw;
            }
            finally
            {
                if (texture is not null)
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        // Legacy method kept for compatibility
        public static void ModifyTextureFile(
            string assetPath,
            int currentWidth, int currentHeight,
            int newWidth, int newHeight)
        {
            ModifyTextureFileDirectly(assetPath, newWidth, newHeight);
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

        private static bool ResizeTexture(
            Texture2D texture,
            int currentWidth, int currentHeight,
            int newWidth, int newHeight)
        {
            // Store original pixels and format
            var originalPixels = texture.GetPixels();
            var originalFormat = texture.format;
            var hasMipMaps = texture.mipmapCount > 1;

            // Reinitialize clears all pixel data but preserves the texture object
            if (!texture.Reinitialize(newWidth, newHeight, originalFormat, hasMipMaps))
            {
                Debug.LogError("Failed to reinitialize texture");
                return false;
            }

            ResamplePixels(texture, originalPixels, currentWidth, currentHeight, newWidth, newHeight);
            return true;
        }

        private static void ResamplePixels(
            Texture2D texture,
            Color[] originalPixels,
            int currentWidth, int currentHeight,
            int newWidth, int newHeight)
        {
            var newPixels = new Color[newWidth * newHeight];
            for (var y = 0; y < newHeight; y++)
            {
                for (var x = 0; x < newWidth; x++)
                {
                    var u = x / (float)(newWidth - 1);
                    var v = y / (float)(newHeight - 1);
                    var origX = Mathf.FloorToInt(u * (currentWidth - 1));
                    var origY = Mathf.FloorToInt(v * (currentHeight - 1));
                    newPixels[y * newWidth + x] = GetPixelSafe(
                        originalPixels, origX, origY, currentWidth, currentHeight);
                }
            }

            texture.SetPixels(newPixels);
            texture.Apply();
        }

        private static bool SaveTextureToFile(Texture2D texture, string assetPath)
        {
            var fileExtension = Path.GetExtension(assetPath).ToLower();
            var newBytes = EncodeTexture(texture, fileExtension);
            if (newBytes is null)
            {
                Debug.LogError($"Failed to encode texture as {fileExtension}");
                return false;
            }

            File.WriteAllBytes(assetPath, newBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return true;
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
                default:
                    Debug.LogWarning($"Unsupported file format for encoding: {fileExtension}");
                    return null;
            }
        }

        #endregion
    }
}
