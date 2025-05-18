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

            return (int)Mathf.Ceil(dimension / 4f) * 4;
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

            // Calculate quad-divisible dimensions
            var newImportedWidth = CalculateDivisibleByFour(importedWidth);
            var newImportedHeight = CalculateDivisibleByFour(importedHeight);

            // Calculate what the source dimensions should be to get quad-divisible imported dimensions
            var (newSourceWidth, newSourceHeight) = CalculateRequiredSourceDimensions(
                sourceInfo.Width, sourceInfo.Height,
                importedWidth, importedHeight,
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

        public static (int width, int height) CalculateRequiredSourceDimensions(
            int sourceWidth, int sourceHeight,
            int importedWidth, int importedHeight,
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
                ModifyTextureFile(
                    texture.Path,
                    texture.SourceWidth,
                    texture.SourceHeight,
                    texture.NewSourceWidth,
                    texture.NewSourceHeight);
            }
            else
            {
                ModifyTextureFile(
                    texture.Path,
                    texture.CurrentWidth,
                    texture.CurrentHeight,
                    texture.NewWidth,
                    texture.NewHeight);
            }
        }

        #endregion

        #region Modify texture file

        public static void ModifyTextureFile(
            string assetPath,
            int currentWidth, int currentHeight,
            int newWidth, int newHeight)
        {
            if (newWidth == currentWidth && newHeight == currentHeight)
            {
                return;
            }

            Texture2D texture = null;
            try
            {
                texture = LoadTextureFromFile(assetPath);
                if (texture is null)
                {
                    return;
                }

                // Use actual dimensions from loaded texture
                currentWidth = texture.width;
                currentHeight = texture.height;

                if (!ResizeTexture(texture, currentWidth, currentHeight, newWidth, newHeight))
                {
                    return;
                }

                if (!SaveTextureToFile(texture, assetPath))
                {
                    return;
                }

                Debug.Log($"Resized: '{assetPath}' from {currentWidth}x{currentHeight} to {newWidth}x{newHeight}");
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
