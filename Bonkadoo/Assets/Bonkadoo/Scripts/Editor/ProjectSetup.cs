using System;
using System.IO;
using System.Linq;
using Bonkadoo.Presentation;
using Bonkadoo.Presentation.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bonkadoo.Editor
{
    /// <summary>Idempotent project configuration; run from the menu or -executeMethod in CI.</summary>
    public static class ProjectSetup
    {
        public const string ScenesDir = "Assets/Bonkadoo/Scenes";

        public static readonly (string name, Type root)[] SceneRoots =
        {
            (Nav.Bootstrap, typeof(GameBootstrap)), (Nav.Menu, typeof(MenuScreen)),
            (Nav.ChapterHub, typeof(ChapterHubScreen)), (Nav.Gameplay, typeof(GameplayScreen)),
            (Nav.Credits, typeof(CreditsScreen)),
        };

        [MenuItem("Bonkadoo/Apply Project Setup")]
        public static void Apply()
        {
            ConfigurePlayer();
            EnsureScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Bonkadoo] project setup applied");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Omnicasa";
            PlayerSettings.productName = "BONKADOO!";
            PlayerSettings.bundleVersion = "0.9.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.omnicasa.bonkadoo");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.omnicasa.bonkadoo");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            // offline-first: no internet permission is required to play (D06); IAP/ads add their own when integrated
            PlayerSettings.Android.forceInternetPermission = false;
        }

        private static void EnsureScenes()
        {
            Directory.CreateDirectory(ScenesDir);
            foreach (var (name, root) in SceneRoots)
            {
                var path = $"{ScenesDir}/{name}.unity";
                var scene = File.Exists(path)
                    ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                if (UnityEngine.Object.FindAnyObjectByType(root) == null) new GameObject(root.Name).AddComponent(root);
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.13f, 0.13f, 0.23f);
                }
                EditorSceneManager.SaveScene(scene, path);
            }
            EditorBuildSettings.scenes = SceneRoots.Select(s => new EditorBuildSettingsScene($"{ScenesDir}/{s.name}.unity", true)).ToArray();
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes");
        }
    }
}
