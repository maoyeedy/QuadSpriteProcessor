using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QuadSpriteProcessor
{
    public class QuadProcessorEditorWindow : EditorWindow
    {
        private readonly List<TextureAsset> _textures = new();
        private bool _considerImporterMaxSize = true;
        private bool _processSubfolders = true;
        private Vector2 _scrollPosition;
        private string _targetFolder = "Assets";

        private void OnGUI()
        {
            DrawToolbar();
            DrawOptions();

            if (GUILayout.Button("Scan For Non-Quad-Divisible Sprites")) ScanTextures();

            if (_textures.Count > 0)
            {
                DrawSelectionButtons();

                if (GUILayout.Button("Process Selected Sprites")) ProcessSelectedTextures();

                DrawTextureList();
            }
            else
            {
                EditorGUILayout.HelpBox("No textures found or scan not performed yet.", MessageType.Info);
            }
        }

        [MenuItem("Tools/Quad Sprite Processor")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuadProcessorEditorWindow>("Quad Sprite Processor");
            window.minSize = new Vector2(500, 400);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            _targetFolder = EditorGUILayout.TextField("Target Folder", _targetFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(80))) BrowseForFolder();

            EditorGUILayout.EndHorizontal();
        }

        private void BrowseForFolder()
        {
            var newPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (string.IsNullOrEmpty(newPath))
                return;

            // Convert to relative project path if possible
            if (newPath.StartsWith(Application.dataPath))
                _targetFolder = "Assets" + newPath.Substring(Application.dataPath.Length);
            else
                _targetFolder = newPath;
        }

        private void DrawOptions()
        {
            _processSubfolders = EditorGUILayout.Toggle("Include Subfolders", _processSubfolders);
            _considerImporterMaxSize = EditorGUILayout.Toggle("Consider Imported Size", _considerImporterMaxSize); //TODO: a enum to switch between original and imported size
            EditorGUILayout.Space();
        }

        private void DrawSelectionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
                foreach (var texture in _textures)
                    texture.Selected = true;

            if (GUILayout.Button("Deselect All"))
                foreach (var texture in _textures)
                    texture.Selected = false;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextureList()
        {
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
                    EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(200));
                else
                    EditorGUILayout.LabelField(Path.GetFileName(texture.Path), GUILayout.Width(200));

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

            var searchOption = _processSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var fileExtensions = new[] { "*.png", "*.jpg", "*.jpeg" };
            var allFiles = new List<string>();

            foreach (var ext in fileExtensions) allFiles.AddRange(Directory.GetFiles(_targetFolder, ext, searchOption));

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
                if (ShouldSkipFile(loweredPath))
                    continue;

                try
                {
                    AddTextureIfNeeded(file);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error reading texture {file}: {e.Message}");
                }
            }

            EditorUtility.ClearProgressBar();

            if (_textures.Count == 0)
                EditorUtility.DisplayDialog("Scan Complete",
                    "No textures with dimensions not divisible by 4 were found.", "OK");
        }

        private bool ShouldSkipFile(string loweredPath)
        {
            return loweredPath.Contains("assets\\plugins") ||
                   loweredPath.Contains("assets\\samples") ||
                   loweredPath.Contains("assets\\editor") ||
                   loweredPath.Contains("~");
        }

        private void AddTextureIfNeeded(string filePath)
        {
            if (_considerImporterMaxSize)
                AddTextureBasedOnImporter(filePath);
            else
                AddTextureBasedOnActualSize(filePath);
        }

        private void AddTextureBasedOnActualSize(string filePath)
        {
            var textureInfo = QuadProcessorUtility.GetTextureInfo(filePath);

            if (!QuadProcessorUtility.AreDimensionsDivisibleByFour(textureInfo.Width, textureInfo.Height))
            {
                var newWidth = QuadProcessorUtility.CalculateDivisibleByFour(textureInfo.Width);
                var newHeight = QuadProcessorUtility.CalculateDivisibleByFour(textureInfo.Height);

                _textures.Add(new TextureAsset
                {
                    Path = filePath,
                    CurrentWidth = textureInfo.Width,
                    CurrentHeight = textureInfo.Height,
                    NewWidth = newWidth,
                    NewHeight = newHeight,
                    Selected = true
                });
            }
        }

        private void AddTextureBasedOnImporter(string filePath)
        {
            var importedInfo = QuadProcessorUtility.GetImportedTextureInfo(filePath);

            if (importedInfo.NeedsProcessing)
                _textures.Add(new TextureAsset
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
                });
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

            if (!ConfirmProcessing(selectedTextures.Count))
                return;

            ProcessTextures(selectedTextures);
        }

        private bool ConfirmProcessing(int count)
        {
            return EditorUtility.DisplayDialog("Confirm Texture Modification",
                $"You are about to modify {count} texture files. " +
                "This operation cannot be undone. Proceed?", "Yes", "Cancel");
        }

        private void ProcessTextures(List<TextureAsset> texturesToProcess)
        {
            var startTime = DateTime.Now;
            var processedCount = 0;
            var failedCount = 0;
            var totalCount = texturesToProcess.Count;

            AssetDatabase.StartAssetEditing();

            try
            {
                foreach (var texture in texturesToProcess)
                {
                    var fileName = Path.GetFileName(texture.Path);
                    processedCount++;

                    EditorUtility.DisplayProgressBar("Processing Textures",
                        $"Processing {processedCount}/{totalCount}: {fileName}",
                        (float)processedCount / totalCount);

                    try
                    {
                        if (_considerImporterMaxSize)
                            QuadProcessorUtility.ModifyTextureFile(
                                texture.Path,
                                texture.SourceWidth,
                                texture.SourceHeight,
                                texture.NewSourceWidth,
                                texture.NewSourceHeight);
                        else
                            QuadProcessorUtility.ModifyTextureFile(
                                texture.Path,
                                texture.CurrentWidth,
                                texture.CurrentHeight,
                                texture.NewWidth,
                                texture.NewHeight);
                    }
                    catch (Exception e)
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

                var endTime = DateTime.Now;
                var duration = endTime - startTime;
                Debug.Log($"Processing ended within: {duration.TotalSeconds:F2} seconds)");
            }

            ShowProcessingResults(processedCount, failedCount);

            // Refresh the list
            _textures.Clear();
            ScanTextures();
        }

        private void ShowProcessingResults(int processed, int failed)
        {
            var successCount = processed - failed;
            var message = $"Successfully processed {successCount} textures.";

            if (failed > 0) message += $" {failed} textures failed (see console for details).";

            EditorUtility.DisplayDialog("Processing Complete", message, "OK");
        }
    }
}
