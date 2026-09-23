using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Typer.EditorTools
{
    public class TextureOptimizerTool : EditorWindow
    {
        [MenuItem("Tools/Typer/Texture Optimizer Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<TextureOptimizerTool>("Texture Optimizer");
            window.minSize = new Vector2(450, 320);
            window.Show();
        }

        [MenuItem("Tools/Typer/Optimize Textures (_Project Assets Only)")]
        public static void OptimizeProjectTexturesOnly()
        {
            OptimizeFolder(new[] { "Assets/_Project" });
        }

        [MenuItem("Tools/Typer/Optimize Textures (All Assets)")]
        public static void OptimizeAllTextures()
        {
            if (EditorUtility.DisplayDialog("Optimize All Textures",
                "This will clamp all textures >1024 to 1024, apply crunch compression to 3D albedo/diffuse textures, " +
                "disable mipmaps on UI sprites, and disable Read/Write on textures and models. Proceed?", "Yes", "Cancel"))
            {
                OptimizeFolder(new[] { "Assets" });
            }
        }

        private Vector2 scrollPos;
        private bool optimizeModels = true;
        private int targetMaxSize = 1024;
        private int crunchQuality = 75;

        private void OnGUI()
        {
            GUILayout.Label("Typer - Texture & Asset Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Optimization Rules:\n" +
                "1. Resolution Clamping: maxTextureSize > 1024 clamped to 1024 (smaller textures untouched).\n" +
                "2. Quality Retention: Crunch compression (Quality ~75) on standard 3D textures.\n" +
                "3. Normal Maps & UI Protection: Normal maps, UI sprites, and SDF font atlases are never crunched.\n" +
                "4. Memory Optimization: Disables Read/Write on textures & meshes; disables mipmaps on 2D UI sprites.",
                MessageType.Info);

            EditorGUILayout.Space();
            targetMaxSize = EditorGUILayout.IntPopup("Max Texture Size Clamp", targetMaxSize,
                new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
            crunchQuality = EditorGUILayout.IntSlider("Crunch Quality (3D)", crunchQuality, 50, 100);
            optimizeModels = EditorGUILayout.Toggle("Disable Read/Write on Models", optimizeModels);

            EditorGUILayout.Space();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (GUILayout.Button("Optimize _Project Assets Textures Only", GUILayout.Height(36)))
            {
                OptimizeFolder(new[] { "Assets/_Project" }, targetMaxSize, crunchQuality, optimizeModels);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Optimize All Project Textures (Assets/)", GUILayout.Height(36)))
            {
                OptimizeFolder(new[] { "Assets" }, targetMaxSize, crunchQuality, optimizeModels);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Disable Read/Write on 3D Models", GUILayout.Height(28)))
            {
                OptimizeModels(new[] { "Assets" });
            }

            EditorGUILayout.EndScrollView();
        }

        public static void OptimizeFolder(string[] searchFolders, int maxClamp = 1024, int crunchQual = 75, bool disableModelReadWrite = true)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchFolders);
            int total = guids.Length;
            int modifiedCount = 0;
            int clampedCount = 0;
            int crunchedCount = 0;
            int mipmapDisabledCount = 0;
            int readDisabledCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < total; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    if (EditorUtility.DisplayCancelableProgressBar("Optimizing Textures",
                        $"Processing ({i + 1}/{total}): {Path.GetFileName(path)}",
                        (float)i / total))
                    {
                        Debug.LogWarning("[TextureOptimizer] Operation cancelled by user.");
                        break;
                    }

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    bool modified = false;

                    // 1. Classification
                    bool isNormalMap = importer.textureType == TextureImporterType.NormalMap ||
                                       path.IndexOf("_normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("_norm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("_bump", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isFontOrSdf = path.IndexOf("TextMesh Pro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("Fonts", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("Font", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("SDF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       importer.textureType == TextureImporterType.SingleChannel;

                    bool isUIOrSprite = importer.textureType == TextureImporterType.Sprite ||
                                        path.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        path.IndexOf("/HUD/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        path.IndexOf("/GUI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        path.IndexOf("MainMenuExtras", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        path.IndexOf("Layer Lab", StringComparison.OrdinalIgnoreCase) >= 0;

                    // 2. Resolution Clamping
                    if (importer.maxTextureSize > maxClamp)
                    {
                        importer.maxTextureSize = maxClamp;
                        clampedCount++;
                        modified = true;
                    }

                    // 3. Memory Optimization (Read/Write)
                    if (importer.isReadable)
                    {
                        importer.isReadable = false;
                        readDisabledCount++;
                        modified = true;
                    }

                    // 4. Mipmaps: Disable for 2D UI & Sprites (saves ~33% VRAM and keeps UI sharp)
                    if (isUIOrSprite)
                    {
                        if (importer.mipmapEnabled)
                        {
                            importer.mipmapEnabled = false;
                            mipmapDisabledCount++;
                            modified = true;
                        }
                    }

                    // 5. Compression Settings
                    if (isNormalMap || isFontOrSdf)
                    {
                        // Never apply lossy crunch to normal maps or font atlases
                        if (importer.crunchedCompression)
                        {
                            importer.crunchedCompression = false;
                            modified = true;
                        }
                    }
                    else if (isUIOrSprite)
                    {
                        // UI Sprites: use standard compression without lossy crunch artifacts
                        if (importer.crunchedCompression)
                        {
                            importer.crunchedCompression = false;
                            modified = true;
                        }
                    }
                    else
                    {
                        // Standard 3D albedo/diffuse/specular textures: Apply Crunch Compression
                        if (!importer.crunchedCompression || importer.compressionQuality != crunchQual)
                        {
                            importer.textureCompression = TextureImporterCompression.Compressed;
                            importer.crunchedCompression = true;
                            importer.compressionQuality = crunchQual;
                            crunchedCount++;
                            modified = true;
                        }
                    }

                    // Apply to platform overrides if any exist
                    var platforms = new[] { "Standalone", "Android", "WebGL" };
                    foreach (var plat in platforms)
                    {
                        var settings = importer.GetPlatformTextureSettings(plat);
                        if (settings.overridden)
                        {
                            bool platMod = false;
                            if (settings.maxTextureSize > maxClamp)
                            {
                                settings.maxTextureSize = maxClamp;
                                platMod = true;
                            }
                            if (isNormalMap || isFontOrSdf || isUIOrSprite)
                            {
                                if (settings.crunchedCompression)
                                {
                                    settings.crunchedCompression = false;
                                    platMod = true;
                                }
                            }
                            else
                            {
                                if (!settings.crunchedCompression)
                                {
                                    settings.textureCompression = TextureImporterCompression.Compressed;
                                    settings.crunchedCompression = true;
                                    settings.compressionQuality = crunchQual;
                                    platMod = true;
                                }
                            }

                            if (platMod)
                            {
                                importer.SetPlatformTextureSettings(settings);
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        modifiedCount++;
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            if (disableModelReadWrite)
            {
                OptimizeModels(searchFolders);
            }

            Debug.Log($"[TextureOptimizer] Complete! Processed {total} textures.\n" +
                      $"Modified: {modifiedCount} | Clamped >{maxClamp}: {clampedCount} | " +
                      $"Crunched: {crunchedCount} | UI Mipmaps Disabled: {mipmapDisabledCount} | " +
                      $"Read/Write Disabled: {readDisabledCount}");
        }

        public static void OptimizeModels(string[] searchFolders)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", searchFolders);
            int modifiedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) continue;

                    if (importer.isReadable)
                    {
                        importer.isReadable = false;
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                        modifiedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[TextureOptimizer] Optimized {modifiedCount} 3D models (Read/Write disabled).");
        }
    }
}
