#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Editor
{
    /// <summary>
    /// Builds the complete scene-authored presentation hierarchy for every scene —
    /// Canvas, panels, buttons, sprites, popups, all baked in as real saved GameObjects,
    /// never constructed at runtime. Every Presentation UI script prefers
    /// [SerializeField] refs wired directly on its GameObject and only falls back to
    /// procedural construction (one or more private Build*Fallback methods) when those
    /// refs are missing. Rather than duplicating that construction logic here and
    /// risking the two copies drifting apart, this invokes those methods via reflection
    /// at edit time and saves the result — the exact same code path already hardened
    /// for runtime use is what populates the scene, assigning straight into each
    /// component's own serialized fields (no separate SerializedObject wiring needed).
    /// Recurses into children a fallback method creates (e.g. GameFlowController's
    /// fallback creates a GameplayHUD, which itself needs its own BuildFallback
    /// invoked to populate its buttons and popups) so the whole tree ends up fully
    /// built, not just the scene's single root component.
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("Tools/Build Game Scenes")]
        public static void BuildGameScenes()
        {
            // Sprite tile prefab must exist before any scene that spawns tiles
            // opens; also catches "stale UI-based prefab on disk" after pulling
            // the sprite conversion.
            TilePrefabBuilder.Rebuild();

            BuildScene("Assets/Scenes/Splash.unity", "SplashScreenUI", typeof(Presentation.UI.SplashScreenUI));
            BuildScene("Assets/Scenes/Bootstrap.unity", "GameRoot", typeof(Core.Bootstrap.GameRoot));
            BuildScene("Assets/Scenes/MainMenu.unity", "MainMenuUI", typeof(Presentation.UI.MainMenuUI));
            BuildScene("Assets/Scenes/LevelSelect.unity", "LevelSelectUI", typeof(Presentation.UI.LevelSelectUI));
            BuildScene("Assets/Scenes/Gameplay.unity", "GameFlowController", typeof(Presentation.GameFlowController));

            SetupBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("Build Game Scenes complete — every scene now has a real, scene-authored hierarchy saved to disk.");
        }

        private static void BuildScene(string path, string rootName, Type rootComponentType)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var rootGO = new GameObject(rootName);
            rootGO.AddComponent(rootComponentType);

            InvokeBuildFallbackRecursive(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Invokes Build*Fallback on every component in the scene, including components
        /// on GameObjects a fallback method itself just created — repeats in passes
        /// until a pass invokes nothing new, since each pass can surface fresh children
        /// (GameFlowController's fallback creates GameplayHUD; GameplayHUD's fallback
        /// then creates the popups; the popups' own fallbacks build their panels). Walks
        /// every root object, re-fetched each pass — not just descendants of the
        /// scene's original root — because a fallback can (and does, for the gameplay
        /// canvas) create a new top-level GameObject rather than a child. Scans inactive
        /// objects too, since popups start disabled.
        /// </summary>
        private static void InvokeBuildFallbackRecursive(UnityEngine.SceneManagement.Scene scene)
        {
            var visited = new HashSet<Component>();
            bool invokedAny;

            do
            {
                invokedAny = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (component == null || !visited.Add(component))
                        {
                            continue;
                        }

                        if (InvokeBuildFallback(component))
                        {
                            invokedAny = true;
                        }
                    }
                }
            } while (invokedAny);
        }

        /// <summary>
        /// Every Presentation UI script's fallback construction methods are private
        /// (only meant to be called from within the component itself), parameterless,
        /// and named starting with "Build" with "Fallback" somewhere in the name — e.g.
        /// BuildFallback, BuildFallbackScenePresentation, BuildAnnouncementFallback. A
        /// component can have more than one (GameplayHUD builds both its main HUD and
        /// its level-start announcement banner independently) — invoke all of them.
        /// Most components in the tree (Clickable, Image, TextMeshProUGUI, ...) have
        /// none at all, which is expected, not an error.
        /// </summary>
        private static bool InvokeBuildFallback(Component component)
        {
            var type = component.GetType();
            var found = false;

            foreach (var method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name.StartsWith("Build", StringComparison.Ordinal) &&
                    method.Name.Contains("Fallback") &&
                    method.GetParameters().Length == 0)
                {
                    method.Invoke(component, null);
                    found = true;
                }
            }

            return found;
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
