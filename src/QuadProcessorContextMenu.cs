using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace QuadSpriteProcessor
{
    public static class QuadProcessorContextMenu
    {
        [MenuItem("Assets/Resize to be Quad-Divisible", true)]
        private static bool ValidateProcessTexture()
        {
            // Check if any selected object is a texture
            return Selection.objects.Any(IsValidTexture);
        }

        [MenuItem("Assets/Resize to be Quad-Divisible")]
        private static void ProcessTextures()
        {
            var validTextures = new List<(Texture2D texture, string path, TextureInfo info, int newWidth, int newHeight)>();
            var alreadyDivisible = new List<string>();

            // Find all valid textures in selection
            foreach (var obj in Selection.objects)
            {
                if (!IsValidTexture(obj)) continue;

                var texture = obj as Texture2D;
                var path = AssetDatabase.GetAssetPath(texture);
                var textureInfo = QuadProcessorUtility.GetTextureInfo(path);

                // Check if already divisible by 4
                if (QuadProcessorUtility.AreDimensionsDivisibleByFour(textureInfo.Width, textureInfo.Height))
                {
                    alreadyDivisible.Add(texture.name);
                    continue;
                }

                // Calculate new dimensions
                var newWidth = QuadProcessorUtility.CalculateDivisibleByFour(textureInfo.Width);
                var newHeight = QuadProcessorUtility.CalculateDivisibleByFour(textureInfo.Height);

                validTextures.Add((texture, path, textureInfo, newWidth, newHeight));
            }

            // If no valid textures found
            if (validTextures.Count == 0)
            {
                if (alreadyDivisible.Count > 0)
                {
                    var message = alreadyDivisible.Count == 1
                        ? $"Texture \"{alreadyDivisible[0]}\" is already divisible by 4."
                        : $"All {alreadyDivisible.Count} selected textures are already divisible by 4.";

                    EditorUtility.DisplayDialog("No Processing Needed", message, "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("No Valid Textures",
                        "No valid textures found in selection. Please select PNG, JPG, JPEG files.",
                        "OK");
                }
                return;
            }

            // Show summary and confirm
            var proceed = EditorUtility.DisplayDialog("Confirm Texture Modification",
                $"This will process {validTextures.Count} texture(s) to have dimensions divisible by 4.\n\n" +
                "This operation cannot be undone. Proceed?",
                "Yes", "Cancel");

            if (!proceed) return;

            // Process textures
            var startTime = System.DateTime.Now;
            var processed = 0;
            var failed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < validTextures.Count; i++)
                {
                    var (texture, path, info, newWidth, newHeight) = validTextures[i];

                    EditorUtility.DisplayProgressBar("Processing Textures",
                        $"Processing {i+1}/{validTextures.Count}: {texture.name}",
                        (float)i / validTextures.Count);

                    try
                    {
                        QuadProcessorUtility.ModifyTextureFile(
                            path,
                            info.Width,
                            info.Height,
                            newWidth,
                            newHeight);

                        processed++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error processing texture {path}: {e.Message}");
                        failed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                var endTime = System.DateTime.Now;
                var duration = endTime - startTime;
                Debug.Log($"Texture processing completed in {duration.TotalSeconds:F2} seconds");
            }

            // Show results
            if (failed > 0)
            {
                EditorUtility.DisplayDialog("Processing Complete",
                    $"Processed {processed} textures successfully.\n{failed} textures failed (see console for details).",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Processing Complete",
                    $"Successfully processed {processed} textures.",
                    "OK");
            }
        }

        private static bool IsValidTexture(Object obj)
        {
            if (obj is not Texture2D) return false;

            var path = AssetDatabase.GetAssetPath(obj);
            var ext = Path.GetExtension(path).ToLower();

            // Check if it's a supported texture format
            return ext is ".png" or ".jpg" or ".jpeg";
        }
    }
}