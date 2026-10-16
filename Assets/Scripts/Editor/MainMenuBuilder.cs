using System.Collections.Generic;
using SoulBind.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// SoulBind > Create Main Menu Scene: run it with the match scene open. It creates
    /// Assets/Scenes/MainMenu.unity (camera, EventSystem, Canvas with MainMenuView) and puts the menu
    /// first and the match scene second in File > Build Settings.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("SoulBind/Create Main Menu Scene")]
        private static void CreateMainMenuScene()
        {
            Scene gameScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(gameScene.path))
            {
                EditorUtility.DisplayDialog("Create Main Menu Scene", "Open and save the match scene first, then run this again.", "OK");
                return;
            }
            if (gameScene.path == MenuScenePath)
            {
                EditorUtility.DisplayDialog("Create Main Menu Scene", "Open the match scene (not the menu), then run this again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string gameScenePath = gameScene.path;
            string gameSceneName = gameScene.name;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.06f, 0.12f);
            camera.orthographic = true;

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            // The project uses the new Input System, whose UI module is looked up by name so this
            // editor script does not need a reference to the Input System assembly.
            System.Type inputModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModule != null)
                eventSystem.AddComponent(inputModule);
            else
                eventSystem.AddComponent<StandaloneInputModule>();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            MainMenuView view = canvasObject.AddComponent<MainMenuView>();
            var so = new SerializedObject(view);
            so.FindProperty("gameSceneName").stringValue = gameSceneName;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(menu, MenuScenePath);

            // Build Settings: the menu first (it is what a build opens), then the match scene, then the rest.
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(gameScenePath, true),
            };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != MenuScenePath && existing.path != gameScenePath)
                    scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"MainMenuBuilder: created {MenuScenePath}. Build Settings: MainMenu first, then {gameSceneName}. " +
                      "Press Play here to start from the menu.");
        }
    }
}