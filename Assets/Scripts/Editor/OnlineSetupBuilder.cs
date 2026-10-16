using System;
using System.Collections.Generic;
using SoulBind.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// SoulBind > Set Up Online, run with the match scene open: adds the NetworkMatch object
    /// (a NetworkObject plus NetworkMatch, wired to the MatchController) and puts the scene in the
    /// Build Settings right after the main menu. Running it again only rewires what is there.
    /// The NetworkManager needs no setup: OnlineSession creates it when someone hosts or joins.
    /// Netcode types are found by name (Unity's type cache), so this file needs no extra assembly references.
    /// </summary>
    public static class OnlineSetupBuilder
    {
        [MenuItem("SoulBind/Set Up Online")]
        private static void SetUpOnline()
        {
            GameObject controllerObject = GameObject.Find("MatchController");
            MatchController controller = controllerObject != null ? controllerObject.GetComponent<MatchController>() : null;
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Set Up Online",
                    "Open the match scene first (the one with the MatchController object), then run this again.", "OK");
                return;
            }

            Type networkObjectType = FindType("Unity.Netcode.NetworkObject");
            Type networkMatchType = FindType("SoulBind.Online.NetworkMatch");
            if (networkObjectType == null || networkMatchType == null)
            {
                EditorUtility.DisplayDialog("Set Up Online",
                    networkObjectType == null
                        ? "Netcode for GameObjects is not installed (Window > Package Manager)."
                        : "NetworkMatch.cs has not compiled yet. Fix any Console errors, then run this again.", "OK");
                return;
            }

            GameObject go = GameObject.Find("NetworkMatch");
            if (go == null)
            {
                go = new GameObject("NetworkMatch");
                SceneManager.MoveGameObjectToScene(go, controller.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(go, "Set Up Online");
            }
            if (go.GetComponent(networkObjectType) == null)
                Undo.AddComponent(go, networkObjectType);
            Component networkMatch = go.GetComponent(networkMatchType);
            if (networkMatch == null)
                networkMatch = Undo.AddComponent(go, networkMatchType);

            var so = new SerializedObject(networkMatch);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.ApplyModifiedProperties();

            Scene scene = controller.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            AddToBuildSettings(scene.path);

            Selection.activeGameObject = go;
            Debug.Log($"OnlineSetupBuilder: NetworkMatch added to '{scene.name}' and wired to the MatchController. " +
                      $"Save the scene (Ctrl+S). In the MainMenu scene, MainMenuView's Game Scene Name must be '{scene.name}'.");
        }

        // The match scene goes after the main menu (scene 0), which the menu's online code relies on.
        private static void AddToBuildSettings(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogWarning("OnlineSetupBuilder: save the scene once, then run Set Up Online again so it can be added to the Build Settings.");
                return;
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    if (!scenes[i].enabled)
                    {
                        scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                        EditorBuildSettings.scenes = scenes.ToArray();
                    }
                    if (i == 0)
                        Debug.LogWarning("OnlineSetupBuilder: the match scene is first in the Build Settings. Run " +
                                         "SoulBind > Create Main Menu Scene (or move MainMenu to the top) so the menu is scene 0.");
                    return;
                }
            }
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // Both types are components, so Unity's type cache can find them by name.
        private static Type FindType(string fullName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type.FullName == fullName)
                    return type;
            }
            return null;
        }
    }
}