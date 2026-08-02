using UnityEngine;
using UnityEngine.SceneManagement;

namespace Presentation.UI
{
    /// <summary>
    /// Splash Screen. Builds its own background and label, then transitions to
    /// Bootstrap after a fixed delay.
    /// </summary>
    public sealed class SplashScreenUI : MonoBehaviour
    {
        [SerializeField] private float _displayDuration = 2f;

        private void Awake()
        {
            var canvas = UIFactory.CreateScreenCanvas("Canvas");
            canvas.transform.SetParent(transform, false);

            UIFactory.CreateFullScreenPanel(canvas.transform, new Color(0.2f, 0.2f, 0.3f));
            UIFactory.CreateText(canvas.transform, "Triple Tiles Kingdom", Vector2.zero, new Vector2(800, 200), fontSize: 56);
        }

        private void Start()
        {
            Invoke(nameof(TransitionToBootstrap), _displayDuration);
        }

        private void TransitionToBootstrap()
        {
            SceneManager.LoadScene("Bootstrap");
        }
    }
}
