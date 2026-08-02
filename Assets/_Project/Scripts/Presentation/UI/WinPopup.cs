using UnityEngine;

namespace Presentation.UI
{
    /// <summary>Builds its own dim background and buttons onto its own GameObject.</summary>
    public sealed class WinPopup : MonoBehaviour
    {
        private GameFlowController _gameFlowController;

        public void Initialize(GameFlowController gameFlowController)
        {
            _gameFlowController = gameFlowController;

            UIFactory.CreateFullScreenPanel(transform, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.CreateText(transform, "You Win!", new Vector2(0, 200), new Vector2(400, 100), fontSize: 48);

            UIFactory.CreateButton(transform, "Next", new Vector2(0, 50), new Vector2(300, 80), new Color(0.2f, 0.8f, 0.3f), OnNextClicked);
            UIFactory.CreateButton(transform, "Restart", new Vector2(0, -50), new Vector2(300, 80), Color.gray, OnRestartClicked);
            UIFactory.CreateButton(transform, "Menu", new Vector2(0, -150), new Vector2(300, 80), new Color(0.8f, 0.3f, 0.2f), OnMenuClicked);
        }

        private void OnNextClicked()
        {
            _gameFlowController.NextLevel();
        }

        private void OnMenuClicked()
        {
            _gameFlowController.ReturnToMenu();
        }

        private void OnRestartClicked()
        {
            _gameFlowController.Restart();
        }
    }
}
