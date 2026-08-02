using System;
using System.Linq;
using Domain.Board;
using Domain.Levels;
using Domain.Matching;
using Domain.State;
using Domain.Tray;
using Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Presentation
{
    /// <summary>
    /// Orchestrates the complete gameplay loop and owns the Gameplay scene's entire
    /// presentation hierarchy. Builds its own Canvas/BoardController/TrayController/
    /// GameplayHUD in Awake so the scene only needs an empty GameObject with this
    /// component attached — no prefab wiring or Inspector references required, which
    /// keeps the scene resilient to being rebuilt or regenerated.
    /// Connects Domain models to Presentation views. Handles: tile selection, match
    /// detection, board updates, tray updates, win/lose conditions.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        private BoardController _boardController;
        private TrayController _trayController;
        private GameplayHUD _hud;

        private BoardModel _board;
        private TrayModel _tray;
        private BoardStateMachine _stateMachine;
        private MatchSystem _matchSystem;
        private LevelModel _level;
        private int _currentLevelIndex;
        private bool _isGameOver;

        public event Action<bool> OnGameOver; // true = won, false = lost

        private void Awake()
        {
            BuildScenePresentation();
        }

        private void BuildScenePresentation()
        {
            var camGO = new GameObject("MainCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
            camGO.tag = "MainCamera";

            var canvas = UIFactory.CreateScreenCanvas("GameplayCanvas");

            var boardRoot = UIFactory.CreateContainer(canvas.transform, "BoardRoot");
            boardRoot.anchoredPosition = new Vector2(0, 150);
            _boardController = boardRoot.gameObject.AddComponent<BoardController>();

            var trayRoot = UIFactory.CreateContainer(canvas.transform, "TrayRoot");
            trayRoot.anchoredPosition = new Vector2(0, -750);
            _trayController = trayRoot.gameObject.AddComponent<TrayController>();

            var hudRoot = UIFactory.CreateFullScreenContainer(canvas.transform, "HUD");
            _hud = hudRoot.gameObject.AddComponent<GameplayHUD>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void Start()
        {
            InitializeGame();
        }

        private void InitializeGame()
        {
            _currentLevelIndex = PlayerPrefs.GetInt("LevelToPlay", 0);
            _level = LevelGenerator.GenerateLevel(_currentLevelIndex);

            var theme = Resources.Load<TileThemeSO>("TileTheme_Default");
            var typeCount = _level.TileLayout.Select(t => t.TileType.Value).Distinct().Count();
            var fruitSprites = LevelGenerator.SelectFruitSprites(_currentLevelIndex, typeCount, theme.FruitSprites);

            _board = BoardGenerator.CreateBoard(_level);
            _tray = new TrayModel(_level.TraySize);
            _stateMachine = new BoardStateMachine(); // starts in Loading
            _matchSystem = new MatchSystem(new ConsecutiveMatchRule(_level.MatchCount));

            _boardController.Initialize(_board, fruitSprites, OnTileSelected);
            _trayController.Initialize(_tray, theme, fruitSprites);
            _hud.Initialize(this, _level, _currentLevelIndex);

            _isGameOver = false;
            _stateMachine.TrySetState(BoardState.Ready);
        }

        private void OnTileSelected(TileId tileId)
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready)
                return;

            if (!_board.TryGetTile(tileId, out var tile) || !tile.IsSelectable)
                return;

            if (!_stateMachine.TrySetState(BoardState.Animating))
                return;

            var tileType = tile.TileType;

            if (!_tray.TryInsert(tileType, out _))
            {
                // Guard only — with the level generator's capacity invariant this should
                // never happen, but a full tray with no match is a loss, not a crash.
                EndGame(won: false);
                return;
            }

            _board.TrySelectTile(tile);

            if (_matchSystem.TryEvaluate(_tray, tileType, out var matchResult))
            {
                _tray.RemoveSlots(matchResult.SlotIndices);
            }

            _boardController.Refresh();
            _trayController.Refresh();

            ResolveTurnOutcome();
        }

        private void ResolveTurnOutcome()
        {
            if (_board.IsCleared && _tray.IsEmpty)
            {
                EndGame(won: true);
                return;
            }

            if (_tray.IsFull || (_board.SelectableTiles.Count == 0 && !_tray.IsEmpty))
            {
                EndGame(won: false);
                return;
            }

            _stateMachine.TrySetState(BoardState.Ready);
        }

        private void EndGame(bool won)
        {
            _isGameOver = true;
            _stateMachine.TrySetState(won ? BoardState.Won : BoardState.Lost);

            if (won)
            {
                var highestUnlocked = PlayerPrefs.GetInt("HighestUnlockedLevel", 0);
                if (_currentLevelIndex + 1 > highestUnlocked)
                {
                    PlayerPrefs.SetInt("HighestUnlockedLevel", _currentLevelIndex + 1);
                }
                PlayerPrefs.Save();
            }

            OnGameOver?.Invoke(won);
            _hud.ShowGameOverPopup(won);
        }

        public void Pause()
        {
            if (_stateMachine.TrySetState(BoardState.Paused))
            {
                Time.timeScale = 0;
            }
        }

        public void Resume()
        {
            if (_stateMachine.TrySetState(BoardState.Ready))
            {
                Time.timeScale = 1;
            }
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void NextLevel()
        {
            PlayerPrefs.SetInt("LevelToPlay", _currentLevelIndex + 1);
            Time.timeScale = 1;
            SceneManager.LoadScene("Gameplay");
        }

        public void ReturnToMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
