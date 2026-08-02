using UnityEngine;

namespace Presentation.UI
{
    /// <summary>Builds its own dim background and buttons onto its own GameObject.</summary>
    public sealed class LosePopup : MonoBehaviour
    {
        private GameFlowController _gameFlowController;

        public void Initialize(GameFlowController gameFlowController)
        {
            _gameFlowController = gameFlowController;

            UIFactory.CreateFullScreenPanel(transform, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.CreateText(transform, "Level Failed", new Vector2(0, 200), new Vector2(400, 100), fontSize: 48);

            UIFactory.CreateButton(transform, "Retry", new Vector2(0, 50), new Vector2(300, 80), new Color(0.2f, 0.8f, 0.3f), OnRetryClicked);
            UIFactory.CreateButton(transform, "Menu", new Vector2(0, -50), new Vector2(300, 80), new Color(0.8f, 0.3f, 0.2f), OnMenuClicked);
        }

        private void OnRetryClicked()
        {
            _gameFlowController.Restart();
        }

        private void OnMenuClicked()
        {
            _gameFlowController.ReturnToMenu();
        }
    }
}
