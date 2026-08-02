#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Editor
{
    /// <summary>
    /// Utility to set up all game scenes. Every Presentation UI script builds its own
    /// hierarchy procedurally at runtime (see Presentation/UI/UIFactory.cs), so each
    /// scene here only needs a single GameObject carrying the relevant root component —
    /// there is no Inspector wiring to keep in sync.
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("Tools/Setup All Scenes")]
        public static void SetupAllScenes()
        {
            SetupScene("Assets/Scenes/Splash.unity", "SplashScreenUI", go => go.AddComponent<Presentation.UI.SplashScreenUI>());
            SetupScene("Assets/Scenes/Bootstrap.unity", "GameRoot", go => go.AddComponent<Core.Bootstrap.GameRoot>());
            SetupScene("Assets/Scenes/MainMenu.unity", "MainMenuUI", go => go.AddComponent<Presentation.UI.MainMenuUI>());
            SetupScene("Assets/Scenes/LevelSelect.unity", "LevelSelectUI", go => go.AddComponent<Presentation.UI.LevelSelectUI>());
            SetupScene("Assets/Scenes/Gameplay.unity", "GameFlowController", go => go.AddComponent<Presentation.GameFlowController>());

            SetupBuildSettings();

            Debug.Log("Setup All Scenes complete.");
        }

        private static void SetupScene(string path, string rootName, System.Action<GameObject> addRootComponent)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            var rootGO = new GameObject(rootName);
            addRootComponent(rootGO);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Splash.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/LevelSelect.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Gameplay.unity", true),
            };
        }
    }
}
#endif
