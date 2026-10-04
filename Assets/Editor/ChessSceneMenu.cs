using System.IO;
using System.Linq;
using BciChess.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BciChess.EditorTools
{
    /// <summary>Creates the playable chess scene: a camera plus a ChessGameBootstrap that builds everything else.</summary>
    public static class ChessSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/Chess.unity";

        [MenuItem("BCI Chess/Create Chess Scene")]
        public static void CreateChessScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("BCI Chess", $"{ScenePath} already exists. Replace it?", "Replace", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x1B, 0x1F, 0x27, 0xFF);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            new GameObject("ChessGame").AddComponent<ChessGameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"Created {ScenePath}. Press Play to start.");
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
