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

        [MenuItem("Tools/Quad Sprite Processor")]
        public static void ShowWindow()
        {
            var window = GetWindow<QuadProcessorEditorWindow>("Quad Sprite Processor");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawOptions();

            if (GUILayout.Button("Scan For Non-Quad-Divisible Sprites"))
            {
                ScanTextures();
            }

            if (_textures.Count == 0)
            {
                // EditorGUILayout.HelpBox("No textures found or scan not performed yet.", MessageType.Info);
                return;
            }

            DrawSelectionControls();
            DrawTextureList();
        }

        private void DrawSelectionControls()
        {
            DrawSelectionButtons();

            if (GUILayout.Button("Process Selected Sprites"))
            {
                ProcessSelectedTextures();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            _targetFolder = EditorGUILayout.TextField("Target Folder", _targetFolder);

            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                BrowseForFolder();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void BrowseForFolder()
        {
            var newPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (string.IsNullOrEmpty(newPath))
            {
                return;
            }

            _targetFolder = ConvertToProjectPath(newPath);
        }

        private string ConvertToProjectPath(string fullPath)
        {
            if (fullPath.StartsWith(Application.dataPath))
            {
                return "Assets" + fullPath.Substring(Application.dataPath.Length);
            }
            else
            {
                return fullPath;
            }
        }

        private void DrawOptions()
        {
            _processSubfolders = EditorGUILayout.Toggle("Include Subfolders", _processSubfolders);
            _considerImporterMaxSize = EditorGUILayout.Toggle("Consider Imported Size", _considerImporterMaxSize);
            EditorGUILayout.Space();
        }

        private void DrawSelectionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select All"))
            {
                ToggleAllTextures(true);
            }

            if (GUILayout.Button("Deselect All"))
            {
                ToggleAllTextures(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ToggleAllTextures(bool selected)
        {
            foreach (var texture in _textures)
            {
                texture.Selected = selected;
            }
        }

        private void DrawTextureList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawTextureListHeader();
            DrawTextureListItems();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTextureListHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Select", GUILayout.Width(50));
            EditorGUILayout.LabelField("Texture", GUILayout.Width(200));
            EditorGUILayout.LabelField("Current Size", GUILayout.Width(100));
            EditorGUILayout.LabelField("New Size", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextureListItems()
        {
            foreach (var texture in _textures)
            {
                EditorGUILayout.BeginHorizontal();

                texture.Selected = EditorGUILayout.Toggle(texture.Selected, GUILayout.Width(50));
                DrawTextureField(texture);
                DrawSizeLabels(texture);

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTextureField(TextureAsset texture)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texture.Path);

            if (tex is not null)
            {
                EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(200));
            }
            else
            {
                EditorGUILayout.LabelField(Path.GetFileName(texture.Path), GUILayout.Width(200));
            }
        }

        private void DrawSizeLabels(TextureAsset texture)
        {
            EditorGUILayout.LabelField($"{texture.CurrentWidth}x{texture.CurrentHeight}", GUILayout.Width(100));
            EditorGUILayout.LabelField($"{texture.NewWidth}x{texture.NewHeight}", GUILayout.Width(100));
        }

        private void ScanTextures()
        {
            _textures.Clear();

            if (!Directory.Exists(_targetFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Directory does not exist: {_targetFolder}", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Scanning", "Getting texture files...", 0);
            var scanOptions = new ScanOptions { FolderPath = _targetFolder, IncludeSubfolders = _processSubfolders, ConsiderImporterMaxSize = _considerImporterMaxSize };

            try
            {
                var textures = QuadProcessorUtility.ScanFolderForNonQuadTextures(
                    scanOptions,
                    UpdateScanProgressBar);

                _textures.AddRange(textures);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (_textures.Count == 0)
            {
                EditorUtility.DisplayDialog("Scan Complete",
                    "No textures with dimensions not divisible by 4 were found.", "OK");
            }
        }

        private void UpdateScanProgressBar(string fileName, int current, int total)
        {
            EditorUtility.DisplayProgressBar("Scanning Textures",
                $"Scanning {current}/{total}: {fileName}",
                (float)current / total);
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
            {
                return;
            }

            StartProcessingTextures(selectedTextures);
        }

        private bool ConfirmProcessing(int count)
        {
            return EditorUtility.DisplayDialog("Confirm Texture Modification",
                $"You are about to modify {count} texture files. " +
                "This operation cannot be undone. Proceed?", "Yes", "Cancel");
        }

        private void StartProcessingTextures(List<TextureAsset> texturesToProcess)
        {
            var startTime = DateTime.Now;
            var processOptions = new ProcessOptions { ConsiderImporterMaxSize = _considerImporterMaxSize };

            AssetDatabase.StartAssetEditing();

            try
            {
                var result = QuadProcessorUtility.ProcessTextures(
                    texturesToProcess,
                    processOptions,
                    UpdateProcessProgressBar);

                ShowProcessingResults(result, startTime);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                // Refresh the list
                _textures.Clear();
                ScanTextures();
            }
        }

        private void UpdateProcessProgressBar(string fileName, int current, int total)
        {
            EditorUtility.DisplayProgressBar("Processing Textures",
                $"Processing {current}/{total}: {fileName}",
                (float)current / total);
        }

        private void ShowProcessingResults(ProcessingResult result, DateTime startTime)
        {
            var duration = DateTime.Now - startTime;
            Debug.Log($"Processing ended within: {duration.TotalSeconds:F2} seconds)");

            var message = $"Successfully processed {result.Succeeded} textures.";

            if (result.Failed > 0)
            {
                message += $" {result.Failed} textures failed (see console for details).";
            }

            EditorUtility.DisplayDialog("Processing Complete", message, "OK");
        }
    }
}
