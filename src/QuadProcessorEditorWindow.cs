using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace QuadSpriteProcessor
{
    public class QuadProcessorEditorWindow : EditorWindow
    {
        private readonly List<TextureAsset> _textures = new();
        private Vector2 _scrollPosition;
        private bool _processSubfolders = true;
        private string _targetFolder = "Assets";

        [MenuItem("Tools/Quad Sprite Processor")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuadProcessorEditorWindow>("Quad Sprite Processor");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            _targetFolder = EditorGUILayout.TextField("Target Folder", _targetFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var newPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    // Convert to relative project path if possible
                    if (newPath.StartsWith(Application.dataPath))
                    {
                        _targetFolder = "Assets" + newPath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        _targetFolder = newPath;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            _processSubfolders = EditorGUILayout.Toggle("Include Subfolders", _processSubfolders);

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan For Non-Quad-Divisible Sprites"))
            {
                ScanTextures();
            }

            // EditorGUILayout.Space();

            if (_textures.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Select All"))
                {
                    foreach (var texture in _textures)
                    {
                        texture.Selected = true;
                    }
                }

                if (GUILayout.Button("Deselect All"))
                {
                    foreach (var texture in _textures)
                    {
                        texture.Selected = false;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Process Selected Sprites"))
                {
                    ProcessSelectedTextures();
                }

                EditorGUI.EndDisabledGroup();

                DrawTextureList();
            }
            else
            {
                EditorGUILayout.HelpBox("No textures found or scan not performed yet.", MessageType.Info);
            }
        }

        private void DrawTextureList()
        {
            // EditorGUILayout.LabelField($"Found {_textures.Count} textures with dimensions not divisible by 4");

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Select", GUILayout.Width(50));
            EditorGUILayout.LabelField("Texture", GUILayout.Width(200));
            EditorGUILayout.LabelField("Current Size", GUILayout.Width(100));
            EditorGUILayout.LabelField("New Size", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Textures list
            foreach (var texture in _textures)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texture.Path);

                EditorGUILayout.BeginHorizontal();
                texture.Selected = EditorGUILayout.Toggle(texture.Selected, GUILayout.Width(50));

                if (tex != null)
                {
                    EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(200));
                }
                else
                {
                    EditorGUILayout.LabelField(Path.GetFileName(texture.Path), GUILayout.Width(200));
                }

                EditorGUILayout.LabelField($"{texture.CurrentWidth}x{texture.CurrentHeight}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{texture.NewWidth}x{texture.NewHeight}", GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void ScanTextures()
        {
            _textures.Clear();

            if (!Directory.Exists(_targetFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Directory does not exist: {_targetFolder}", "OK");
                return;
            }

            var searchOption = _processSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var fileExtensions = new[] { "*.png", "*.jpg", "*.jpeg" };
            var allFiles = new List<string>();

            foreach (var ext in fileExtensions)
            {
                allFiles.AddRange(Directory.GetFiles(_targetFolder, ext, searchOption));
            }

            var fileCount = allFiles.Count;
            var processedCount = 0;

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                processedCount++;
                EditorUtility.DisplayProgressBar("Scanning Textures",
                    $"Scanning {processedCount}/{fileCount}: {fileName}",
                    (float)processedCount / fileCount);

                var loweredPath = file.ToLower();
                if (loweredPath.Contains("assets\\plugins") ||
                    loweredPath.Contains("assets\\samples") ||
                    loweredPath.Contains("~"))
                {
                    // Debug.LogWarning($"Skipping texture in. {loweredPath}");
                    continue;
                }

                try
                {
                    var textureInfo = QuadProcessorUtility.GetTextureInfo(file);
                    if (!QuadProcessorUtility.AreDimensionsDivisibleByFour(textureInfo.Width, textureInfo.Height))
                    {
                        var newWidth = textureInfo.Width % 4 != 0
                            ? (int)Mathf.Ceil(textureInfo.Width / 4f) * 4
                            : textureInfo.Width;

                        var newHeight = textureInfo.Height % 4 != 0
                            ? (int)Mathf.Ceil(textureInfo.Height / 4f) * 4
                            : textureInfo.Height;

                        _textures.Add(new TextureAsset
                        {
                            Path = file,
                            CurrentWidth = textureInfo.Width,
                            CurrentHeight = textureInfo.Height,
                            NewWidth = newWidth,
                            NewHeight = newHeight,
                            Selected = true
                        });
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error reading texture {file}: {e.Message}");
                }
            }

            EditorUtility.ClearProgressBar();

            if (_textures.Count == 0)
            {
                EditorUtility.DisplayDialog("Scan Complete",
                    "No textures with dimensions not divisible by 4 were found.", "OK");
            }
        }

        private void ProcessSelectedTextures()
        {
            var selectedTextures = _textures.FindAll(t => t.Selected);
            if (selectedTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("No Textures Selected",
                    "Please select at least one texture to process.", "OK");
                return;
            }

            var proceed = EditorUtility.DisplayDialog("Confirm Texture Modification",
                $"You are about to modify {selectedTextures.Count} texture files. " +
                "This operation cannot be undone. Proceed?", "Yes", "Cancel");

            if (!proceed)
                return;

            var startTime = System.DateTime.Now;
            var processedCount = 0;
            var failedCount = 0;
            var totalCount = selectedTextures.Count;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var texture in selectedTextures)
                {
                    var fileName = Path.GetFileName(texture.Path);
                    processedCount++;

                    EditorUtility.DisplayProgressBar("Processing Textures",
                        $"Processing {processedCount}/{totalCount}: {fileName}",
                        (float)processedCount / totalCount);

                    try
                    {
                        QuadProcessor.ModifyTextureFile(texture.Path, texture.CurrentWidth, texture.CurrentHeight, texture.NewWidth, texture.NewHeight);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to process {texture.Path}: {e.Message}");
                        failedCount++;
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
                Debug.Log($"Processing ended within: {duration.TotalSeconds:F2} seconds)");
            }

            var successCount = processedCount - failedCount;
            var message = $"Successfully processed {successCount} textures.";

            if (failedCount > 0)
            {
                message += $"\n{failedCount} textures failed (see console for details).";
            }

            EditorUtility.DisplayDialog("Processing Complete", message, "OK");

            // Refresh the list to update any textures that may have changed
            _textures.Clear();
            ScanTextures();
        }
    }
}
