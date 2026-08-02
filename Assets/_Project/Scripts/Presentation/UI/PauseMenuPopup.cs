using UnityEngine;

namespace Presentation.UI
{
    /// <summary>Builds its own dim background and buttons onto its own GameObject.</summary>
    public sealed class PauseMenuPopup : MonoBehaviour
    {
        private GameFlowController _gameFlowController;

        public void Initialize(GameFlowController gameFlowController)
        {
            _gameFlowController = gameFlowController;

            UIFactory.CreateFullScreenPanel(transform, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.CreateText(transform, "Paused", new Vector2(0, 200), new Vector2(400, 100), fontSize: 48);

            UIFactory.CreateButton(transform, "Resume", new Vector2(0, 50), new Vector2(300, 80), new Color(0.2f, 0.8f, 0.3f), OnResumeClicked);
            UIFactory.CreateButton(transform, "Restart", new Vector2(0, -50), new Vector2(300, 80), Color.gray, OnRestartClicked);
            UIFactory.CreateButton(transform, "Menu", new Vector2(0, -150), new Vector2(300, 80), new Color(0.8f, 0.3f, 0.2f), OnMenuClicked);
        }

        private void OnResumeClicked()
        {
            _gameFlowController.Resume();
            Destroy(gameObject);
        }

        private void OnRestartClicked()
        {
            _gameFlowController.Restart();
        }

        private void OnMenuClicked()
        {
            _gameFlowController.ReturnToMenu();
        }
    }
}
