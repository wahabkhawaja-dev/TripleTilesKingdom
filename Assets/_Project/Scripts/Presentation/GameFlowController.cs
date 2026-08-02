using System;
using System.Collections.Generic;
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
    /// Orchestrates the complete gameplay loop. Prefers a scene-authored presentation
    /// hierarchy (Canvas/BoardRoot/TrayRoot/HUD built once by Tools > Build Game Scenes
    /// and saved into Gameplay.unity, so it's hand-editable in the Inspector like any
    /// other scene content) — falls back to runtime construction via UIFactory only if
    /// those references are missing, so the scene never breaks if it hasn't been
    /// (re)built yet.
    /// Connects Domain models to Presentation views. Handles: tile selection, match
    /// detection, board updates, tray updates, win/lose conditions, and the
    /// Shuffle/Hint/Hammer/Undo power-ups.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("Scene-authored refs (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private BoardController _boardController;
        [SerializeField] private TrayController _trayController;
        [SerializeField] private GameplayHUD _hud;
        [SerializeField] private RectTransform _boardRoot;
        [SerializeField] private RectTransform _trayRoot;

        private BoardModel _board;
        private TrayModel _tray;
        private BoardStateMachine _stateMachine;
        private MatchSystem _matchSystem;
        private LevelModel _level;
        private TileThemeSO _tileTheme;
        private Sprite[] _fruitSprites;
        private int _currentLevelIndex;
        private bool _isGameOver;

        /// <summary>Every tile successfully selected through the tray, in order — replayed minus the last entry to implement Undo without needing reversible Domain mutations.</summary>
        private readonly List<TileId> _moveHistory = new();

        public event Action<bool> OnGameOver; // true = won, false = lost

        private void Awake()
        {
            if (_boardController == null || _trayController == null || _hud == null)
            {
                BuildFallbackScenePresentation();
            }

            if (FindFirstObjectByType<Camera>() == null)
            {
                var camGO = new GameObject("MainCamera");
                var cam = camGO.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
                camGO.tag = "MainCamera";
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>Original runtime-construction path, used only when scene-authored refs are missing.</summary>
        private void BuildFallbackScenePresentation()
        {
            var canvas = UIFactory.CreateScreenCanvas("GameplayCanvas");
            canvas.transform.SetParent(transform, false);

            // Vertical layout budget (canvas is ~1920 units tall — see UIFactory's
            // portrait CanvasScaler reference resolution — so safe content range is
            // roughly -900..900): status bar/pause/replay at 860, board centered around
            // 150 (its own pyramid shift adds height above that), tray at -500,
            // power-up row at -780 — chosen with enough clearance that the (now larger)
            // board, tray, and power-ups never overlap.
            _boardRoot = UIFactory.CreateContainer(canvas.transform, "BoardRoot");
            _boardRoot.anchoredPosition = new Vector2(0, 150);
            _boardController = _boardRoot.gameObject.AddComponent<BoardController>();

            _trayRoot = UIFactory.CreateContainer(canvas.transform, "TrayRoot");
            _trayRoot.anchoredPosition = new Vector2(0, -500);
            _trayController = _trayRoot.gameObject.AddComponent<TrayController>();

            var hudRoot = UIFactory.CreateFullScreenContainer(canvas.transform, "HUD");
            _hud = hudRoot.gameObject.AddComponent<GameplayHUD>();
        }

        private void Start()
        {
            InitializeGame();
        }

        private void InitializeGame()
        {
            _currentLevelIndex = PlayerPrefs.GetInt("LevelToPlay", 0);
            _level = LevelGenerator.GenerateLevel(_currentLevelIndex);

            _tileTheme = Resources.Load<TileThemeSO>("TileTheme_Default");
            var typeCount = _level.TileLayout.Select(t => t.TileType.Value).Distinct().Count();
            _fruitSprites = LevelGenerator.SelectFruitSprites(_currentLevelIndex, typeCount, _tileTheme.FruitSprites);

            _board = BoardGenerator.CreateBoard(_level);
            _tray = new TrayModel(_level.TraySize);
            _stateMachine = new BoardStateMachine(); // starts in Loading
            _matchSystem = new MatchSystem(new ConsecutiveMatchRule(_level.MatchCount));
            _moveHistory.Clear();

            var flyTarget = _trayRoot.anchoredPosition - _boardRoot.anchoredPosition;
            _boardController.Initialize(_board, _fruitSprites, flyTarget, OnTileSelected);
            _trayController.Initialize(_tray, _tileTheme, _fruitSprites);
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
            _moveHistory.Add(tileId);

            MatchResult? completedMatch = null;
            if (_matchSystem.TryEvaluate(_tray, tileType, out var matchResult))
            {
                completedMatch = matchResult;
                _tray.RemoveSlots(matchResult.SlotIndices);
            }

            _boardController.Refresh();
            _trayController.Refresh(completedMatch);

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
                _boardController.ShowWinAnimation();
            }
            else
            {
                _boardController.ShowLoseAnimation();
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

        // ------------------------------------------------------------------
        // Power-ups
        // ------------------------------------------------------------------

        /// <summary>
        /// Rebuilds the board and tray from scratch from the original level layout and
        /// replays every move up to (but not including) the last one. Domain has no
        /// reversible "un-select" operation (TrySelectTile's cascade is one-way by
        /// design — see DECISIONS.md), so rather than add one, undo is implemented at
        /// this layer as deterministic replay: BoardGenerator.CreateBoard on the same
        /// LevelModel always assigns the same TileIds in the same order, so historical
        /// TileIds remain valid references into the freshly rebuilt board.
        /// </summary>
        public void Undo()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready || _moveHistory.Count == 0)
                return;

            _moveHistory.RemoveAt(_moveHistory.Count - 1);
            RebuildFromHistory();
        }

        private void RebuildFromHistory()
        {
            _board = BoardGenerator.CreateBoard(_level);
            _tray = new TrayModel(_level.TraySize);

            var replay = new List<TileId>(_moveHistory);
            _moveHistory.Clear();

            foreach (var tileId in replay)
            {
                if (!_board.TryGetTile(tileId, out var tile) || !tile.IsSelectable)
                {
                    continue; // Shouldn't happen for a valid history, but never corrupt state over it.
                }

                var type = tile.TileType;
                if (!_tray.TryInsert(type, out _))
                {
                    continue;
                }

                _board.TrySelectTile(tile);
                _moveHistory.Add(tileId);

                if (_matchSystem.TryEvaluate(_tray, type, out var matchResult))
                {
                    _tray.RemoveSlots(matchResult.SlotIndices);
                }
            }

            var flyTarget = _trayRoot.anchoredPosition - _boardRoot.anchoredPosition;
            _boardController.Initialize(_board, _fruitSprites, flyTarget, OnTileSelected);
            _trayController.Initialize(_tray, _tileTheme, _fruitSprites);

            _isGameOver = false;
            _stateMachine.TrySetState(BoardState.Ready);
        }

        /// <summary>
        /// Reshuffles which type each still-on-board tile shows, keeping positions,
        /// layers and per-type counts identical — only the type-to-position mapping
        /// changes. TileModel.TileType has no setter (immutable by design), so this
        /// rebuilds a fresh BoardModel from a synthetic layout of the remaining tiles
        /// rather than mutating existing tiles in place.
        /// </summary>
        public void Shuffle()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready)
                return;

            var remainingCoords = new List<BoardCoordinate>();
            var remainingTypes = new List<TileTypeId>();
            foreach (var tile in _board.AllTiles)
            {
                if (tile.State == TileState.Removed)
                    continue;
                remainingCoords.Add(tile.Coordinate);
                remainingTypes.Add(tile.TileType);
            }

            if (remainingCoords.Count == 0)
                return;

            var random = new System.Random();
            for (var i = remainingTypes.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (remainingTypes[i], remainingTypes[j]) = (remainingTypes[j], remainingTypes[i]);
            }

            var spawns = new List<TileSpawnData>(remainingCoords.Count);
            for (var i = 0; i < remainingCoords.Count; i++)
            {
                spawns.Add(new TileSpawnData(remainingCoords[i], remainingTypes[i]));
            }

            var shuffledLevel = new LevelModel(_level.LevelId, spawns, _level.TraySize, _level.MatchCount, _level.ThemeId, _level.Rules);
            _board = BoardGenerator.CreateBoard(shuffledLevel);

            // Shuffling changes tile identity (fresh TileIds), so past history no longer
            // maps onto valid tiles — clear it rather than leave Undo pointing at nothing.
            _moveHistory.Clear();

            var flyTarget = _trayRoot.anchoredPosition - _boardRoot.anchoredPosition;
            _boardController.Initialize(_board, _fruitSprites, flyTarget, OnTileSelected);
        }

        /// <summary>Highlights a currently-selectable tile — purely visual, no state change.</summary>
        public void Hint()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready)
                return;

            foreach (var tile in _board.SelectableTiles)
            {
                _boardController.PulseTile(tile.Id);
                break;
            }
        }

        /// <summary>
        /// Removes one selectable tile directly from the board without routing it
        /// through the tray — helps clear the board but doesn't progress toward
        /// completing a tray match. A blunt, always-available "unstick" tool.
        /// </summary>
        public void Hammer()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready)
                return;

            TileModel target = null;
            foreach (var tile in _board.SelectableTiles)
            {
                target = tile;
                break;
            }

            if (target == null)
                return;

            if (!_stateMachine.TrySetState(BoardState.Animating))
                return;

            _board.TrySelectTile(target);
            _boardController.Refresh();

            ResolveTurnOutcome();
        }
    }
}
