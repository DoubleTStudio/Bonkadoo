using System.IO;
using Bonkadoo.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bonkadoo.Editor
{
    /// <summary>Idempotent project configuration; run from the menu or -executeMethod in CI.</summary>
    public static class ProjectSetup
    {
        public const string ScenesDir = "Assets/Bonkadoo/Scenes";
        public static readonly string[] SceneNames = { "Bootstrap" };

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
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.omnicasa.bonkadoo");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.omnicasa.bonkadoo");
        }

        private static void EnsureScenes()
        {
            Directory.CreateDirectory(ScenesDir);
            var path = $"{ScenesDir}/Bootstrap.unity";
            if (!File.Exists(path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
                EditorSceneManager.SaveScene(scene, path);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }
    }
}
