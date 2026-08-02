using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Presentation.UI
{
    /// <summary>
    /// Level Select UI. Builds its own Canvas, grid, and level buttons procedurally.
    /// Displays available levels and handles selection.
    /// </summary>
    public sealed class LevelSelectUI : MonoBehaviour
    {
        private const int MaxLevels = 50;
        private const int Columns = 6;

        private void Awake()
        {
            var canvas = UIFactory.CreateScreenCanvas("Canvas");
            canvas.transform.SetParent(transform, false);

            UIFactory.CreateFullScreenPanel(canvas.transform, new Color(0.1f, 0.1f, 0.15f));
            UIFactory.CreateText(canvas.transform, "Select Level", new Vector2(0, 760), new Vector2(800, 100), fontSize: 50);
            UIFactory.CreateButton(canvas.transform, "Back", new Vector2(-420, -760), new Vector2(220, 90), Color.gray, OnBackClicked);

            var gridContainer = UIFactory.CreateContainer(canvas.transform, "LevelGrid");
            var grid = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(140, 140);
            grid.spacing = new Vector2(15, 15);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.MiddleCenter;
            gridContainer.sizeDelta = new Vector2(Columns * 155, ((MaxLevels + Columns - 1) / Columns) * 155);

            CreateLevelButtons(gridContainer);
            EnsureEventSystem();
        }

        private void CreateLevelButtons(Transform container)
        {
            var highestUnlocked = PlayerPrefs.GetInt("HighestUnlockedLevel", 0);

            for (var i = 0; i < MaxLevels; i++)
            {
                var levelIndex = i;
                var isUnlocked = i <= highestUnlocked;

                var button = UIFactory.CreateButton(
                    container,
                    (i + 1).ToString(),
                    Vector2.zero,
                    new Vector2(120, 120),
                    isUnlocked ? new Color(0.2f, 0.6f, 0.9f) : new Color(0.3f, 0.3f, 0.3f),
                    () => OnLevelSelected(levelIndex));

                button.interactable = isUnlocked;
            }
        }

        private void OnLevelSelected(int levelIndex)
        {
            PlayerPrefs.SetInt("LevelToPlay", levelIndex);
            SceneManager.LoadScene("Gameplay");
        }

        private void OnBackClicked()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }
}
