using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace QuadSpriteProcessor
{
    public static class TextureProcessorRT
    {
        public static void ModifyTextureFile(string assetPath, int currentWidth, int currentHeight, int newWidth,
            int newHeight)
        {
            if (newWidth == currentWidth && newHeight == currentHeight) return;

            Texture2D originalTexture = null;
            Texture2D newTexture = null;
            RenderTexture sourceRT = null;
            RenderTexture destRT = null;

            try
            {
                // Store Original
                var originalSettings = new TextureImporterSettings();
                var originalPlatformSettings = new Dictionary<string, TextureImporterPlatformSettings>();
                var originalImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (originalImporter == null)
                {
                    Debug.LogError($"Could not get TextureImporter for: {assetPath}");
                    return;
                }

                originalImporter.ReadTextureSettings(originalSettings);
                StorePlatformSettings(originalImporter, originalPlatformSettings);

                // Load and resize texture
                originalTexture = LoadTextureFromFile(assetPath);
                if (originalTexture == null)
                    return;

                // Use actual dimensions from loaded texture
                currentWidth = originalTexture.width;
                currentHeight = originalTexture.height;

                // Create resized texture
                newTexture = new Texture2D(newWidth, newHeight, originalTexture.format,
                    originalTexture.mipmapCount > 1);

                sourceRT = RenderTexture.GetTemporary(currentWidth, currentHeight, 0, RenderTextureFormat.Default);
                destRT = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.Default);
                sourceRT.filterMode = FilterMode.Point;
                destRT.filterMode = FilterMode.Point;

                // Perform resize operation
                Graphics.Blit(originalTexture, sourceRT);
                Graphics.Blit(sourceRT, destRT);

                var previousActive = RenderTexture.active;
                RenderTexture.active = destRT;
                newTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
                newTexture.Apply();
                RenderTexture.active = previousActive;

                // Encode to file format
                var fileExtension = Path.GetExtension(assetPath).ToLower();
                var newBytes = EncodeTexture(newTexture, fileExtension);

                if (newBytes == null) return;

                // Overwrite file and restore settings
                File.WriteAllBytes(assetPath, newBytes);

                // Re-apply original settings
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.SetTextureSettings(originalSettings);

                    foreach (var kvp in originalPlatformSettings)
                        importer.SetPlatformTextureSettings(kvp.Value);

                    importer.SaveAndReimport();

                    Debug.Log(
                        $"Resized: '{assetPath}' from {currentWidth}x{currentHeight} to {newWidth}x{newHeight}");
                }
                else
                {
                    Debug.LogWarning(
                        $"Could not get importer after overwriting {assetPath}. Forcing generic reimport.");
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error modifying texture {assetPath}: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                if (originalTexture != null) Object.DestroyImmediate(originalTexture);
                if (newTexture != null) Object.DestroyImmediate(newTexture);
                if (sourceRT != null) RenderTexture.ReleaseTemporary(sourceRT);
                if (destRT != null) RenderTexture.ReleaseTemporary(destRT);

                if (RenderTexture.active != null &&
                    (RenderTexture.active == sourceRT || RenderTexture.active == destRT))
                    RenderTexture.active = null;
            }
        }

        private static void StorePlatformSettings(TextureImporter importer,
            Dictionary<string, TextureImporterPlatformSettings> platformSettings)
        {
            var platforms = new[]
            {
                "DefaultTexturePlatform", "Standalone", "iPhone", "Android", "WebGL",
                "Windows Store Apps", "PS4", "XboxOne", "Switch"
            };

            foreach (var platform in platforms)
                platformSettings[platform] = importer.GetPlatformTextureSettings(platform);
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
