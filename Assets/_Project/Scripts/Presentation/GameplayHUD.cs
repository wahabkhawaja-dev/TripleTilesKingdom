using Domain.Levels;
using Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>
    /// Manages the gameplay HUD. Builds its own info text, buttons, and popup
    /// container procedurally, then hosts popup instances. Must be attached to a
    /// RectTransform under a Canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameplayHUD : MonoBehaviour
    {
        private GameFlowController _gameFlowController;
        private RectTransform _popupContainer;
        private GameObject _currentPopup;

        public void Initialize(GameFlowController gameFlowController, LevelModel level, int levelIndex)
        {
            _gameFlowController = gameFlowController;

            UIFactory.CreateText(
                transform,
                $"Level {levelIndex + 1}  |  Match: {level.MatchCount}  |  Capacity: {level.TraySize}",
                new Vector2(0, 760),
                new Vector2(900, 60),
                fontSize: 32);

            UIFactory.CreateButton(transform, "Pause", new Vector2(-420, 760), new Vector2(160, 80), Color.gray, OnPauseClicked);
            UIFactory.CreateButton(transform, "Restart", new Vector2(420, 760), new Vector2(160, 80), Color.gray, OnRestartClicked);

            _popupContainer = UIFactory.CreateFullScreenContainer(transform, "PopupContainer");
        }

        private void OnPauseClicked()
        {
            _gameFlowController.Pause();
            ShowPauseMenu();
        }

        private void OnRestartClicked()
        {
            _gameFlowController.Restart();
        }

        private void ShowPauseMenu()
        {
            ClearCurrentPopup();
            _currentPopup = new GameObject("PauseMenu", typeof(RectTransform));
            _currentPopup.transform.SetParent(_popupContainer, false);
            var rect = (RectTransform)_currentPopup.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _currentPopup.AddComponent<PauseMenuPopup>().Initialize(_gameFlowController);
        }

        public void ShowGameOverPopup(bool won)
        {
            ClearCurrentPopup();
            _currentPopup = new GameObject(won ? "WinPopup" : "LosePopup", typeof(RectTransform));
            _currentPopup.transform.SetParent(_popupContainer, false);
            var rect = (RectTransform)_currentPopup.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (won)
            {
                _currentPopup.AddComponent<WinPopup>().Initialize(_gameFlowController);
            }
            else
            {
                _currentPopup.AddComponent<LosePopup>().Initialize(_gameFlowController);
            }
        }

        private void ClearCurrentPopup()
        {
            if (_currentPopup != null)
            {
                Destroy(_currentPopup);
                _currentPopup = null;
            }
        }
    }
}
