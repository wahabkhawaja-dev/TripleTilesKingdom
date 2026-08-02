using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Presentation.UI
{
    /// <summary>
    /// Main Menu UI controller. Builds its own Canvas and elements procedurally so the
    /// scene only needs an empty GameObject with this component attached.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        private TextMeshProUGUI _coinText;

        private void Awake()
        {
            var canvas = UIFactory.CreateScreenCanvas("Canvas");
            canvas.transform.SetParent(transform, false);

            UIFactory.CreateFullScreenPanel(canvas.transform, new Color(0.1f, 0.1f, 0.15f));

            UIFactory.CreateText(canvas.transform, "Triple Tiles Kingdom", new Vector2(0, 400), new Vector2(800, 120), fontSize: 64);

            UIFactory.CreateButton(canvas.transform, "Play", new Vector2(0, 50), new Vector2(320, 90), new Color(0.2f, 0.8f, 0.3f), OnPlayClicked);
            UIFactory.CreateButton(canvas.transform, "Settings", new Vector2(0, -70), new Vector2(320, 90), Color.gray, OnSettingsClicked);
            UIFactory.CreateButton(canvas.transform, "Quit", new Vector2(0, -190), new Vector2(320, 90), new Color(0.8f, 0.3f, 0.2f), OnQuitClicked);

            _coinText = UIFactory.CreateText(canvas.transform, "0", new Vector2(-420, 760), new Vector2(200, 60), fontSize: 36);

            EnsureEventSystem();
            UpdateCoinDisplay();
        }

        private void OnPlayClicked()
        {
            SceneManager.LoadScene("LevelSelect");
        }

        private void OnSettingsClicked()
        {
            // Placeholder — settings panel not yet implemented.
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdateCoinDisplay()
        {
            var coins = PlayerPrefs.GetInt("Coins", 0);
            _coinText.text = coins.ToString();
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
