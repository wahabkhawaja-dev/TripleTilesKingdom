using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Board;
using Domain.Levels;
using Domain.Matching;
using Domain.State;
using Domain.Tray;
using LevelSystem.Data;
using LevelSystem.Generation;
using Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Presentation
{
    /// <summary>
    /// Orchestrates the complete gameplay loop. Board + tray are world-space
    /// sprites (see <see cref="BoardController"/> / <see cref="TrayController"/>);
    /// HUD stays uGUI on a Screen-Space Overlay Canvas. Clicks on the sprite
    /// tiles come through the EventSystem via a <see cref="Physics2DRaycaster"/>
    /// on the gameplay camera — same input pipeline as the HUD, no separate
    /// polling.
    ///
    /// Tap flow (see <see cref="OnTileSelected"/>): domain mutations happen
    /// up-front so state is always consistent, but every visible response
    /// (slot-icon reveal, match pop, board refresh, next-turn unlock) is
    /// deferred to the flying tile's landing callback — that's what makes the
    /// tile feel like it truly arrives at the tray instead of teleporting in.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("Scene-authored refs (preferred) — leave empty to fall back to runtime construction")]
        [SerializeField] private BoardController _boardController;
        [SerializeField] private TrayController _trayController;
        [SerializeField] private GameplayHUD _hud;
        [SerializeField] private Transform _boardRoot;
        [SerializeField] private Transform _trayRoot;
        [SerializeField] private Camera _gameCamera;

        [Header("Camera framing (world units, portrait)")]
        [SerializeField] private float _cameraOrthoSize = 8f;
        [SerializeField] private Vector3 _boardWorldPosition = new Vector3(0f, 2f, 0f);
        [SerializeField] private Vector3 _trayWorldPosition = new Vector3(0f, -4.5f, 0f);

        private BoardModel _board;
        private TrayModel _tray;
        private BoardStateMachine _stateMachine;
        private MatchSystem _matchSystem;
        private LevelModel _level;
        private TileThemeSO _tileTheme;
        private Sprite[] _fruitSprites;
        private int _currentLevelIndex;
        private bool _isGameOver;

        /// <summary>
        /// Number of tiles currently mid-fly. Rapid taps queue up multiple
        /// concurrent flies (this counter tracks them so ResolveTurnOutcome
        /// only fires once the last one lands — otherwise the state machine
        /// bounces Animating→Ready→Animating between overlapping tiles).
        /// </summary>
        private int _pendingFlies;

        /// <summary>Every tile successfully selected through the tray, in order — replayed minus the last entry to implement Undo without needing reversible Domain mutations.</summary>
        private readonly List<TileId> _moveHistory = new();

        public event Action<bool> OnGameOver; // true = won, false = lost

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();

            if (_boardController == null || _trayController == null || _hud == null)
            {
                BuildFallbackScenePresentation();
            }
        }

        private void EnsureCamera()
        {
            if (_gameCamera == null) _gameCamera = Camera.main;
            if (_gameCamera == null)
            {
                var camGO = new GameObject("MainCamera");
                camGO.tag = "MainCamera";
                _gameCamera = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                _gameCamera.transform.position = new Vector3(0f, 0f, -10f);
            }

            _gameCamera.orthographic = true;
            _gameCamera.orthographicSize = _cameraOrthoSize;
            _gameCamera.clearFlags = CameraClearFlags.SolidColor;
            _gameCamera.backgroundColor = new Color(0.10f, 0.10f, 0.16f);
            _gameCamera.nearClipPlane = -30f;
            _gameCamera.farClipPlane = 30f;

            // The Physics2DRaycaster is what lets sprite tiles receive
            // IPointerClickHandler.OnPointerClick through the same EventSystem
            // pipeline as the HUD. Without this component, clicks on tiles do
            // nothing (which was the exact regression the last iteration hit).
            if (_gameCamera.GetComponent<Physics2DRaycaster>() == null)
            {
                _gameCamera.gameObject.AddComponent<Physics2DRaycaster>();
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// World-space board + tray, HUD on a Screen-Space Overlay Canvas.
        /// Same scene topology <c>Tools ▸ Build Game Scenes</c> bakes into
        /// Gameplay.unity, kept in code as a safety net so the scene never
        /// breaks in Play mode between rebuilds.
        /// </summary>
        private void BuildFallbackScenePresentation()
        {
            if (_boardRoot == null)
            {
                var boardGO = new GameObject("BoardRoot");
                boardGO.transform.SetParent(transform, false);
                boardGO.transform.position = _boardWorldPosition;
                _boardRoot = boardGO.transform;
            }
            if (_boardController == null)
            {
                _boardController = _boardRoot.gameObject.AddComponent<BoardController>();
            }

            if (_trayRoot == null)
            {
                var trayGO = new GameObject("TrayRoot");
                trayGO.transform.SetParent(transform, false);
                trayGO.transform.position = _trayWorldPosition;
                _trayRoot = trayGO.transform;
            }
            if (_trayController == null)
            {
                _trayController = _trayRoot.gameObject.AddComponent<TrayController>();
            }

            if (_hud == null)
            {
                var canvas = UIFactory.CreateScreenCanvas("HUDCanvas");
                canvas.transform.SetParent(transform, false);
                var hudRoot = UIFactory.CreateFullScreenContainer(canvas.transform, "HUD");
                _hud = hudRoot.gameObject.AddComponent<GameplayHUD>();
            }
        }

        private void Start()
        {
            InitializeGame();
        }

        private void InitializeGame()
        {
            _currentLevelIndex = PlayerPrefs.GetInt("LevelToPlay", 0);

            var collection = Resources.Load<LevelCollectionSO>("Levels/LevelCollection");
            if (collection == null || collection.Count == 0)
            {
                Debug.LogError("[GameFlowController] No LevelCollection at Resources/Levels/LevelCollection.asset — open Tools ▸ Level Designer and click Batch Generate.");
                return;
            }

            var def = collection.Get(_currentLevelIndex);
            _level = LevelDefinitionBuilder.Build(def);

            _tileTheme = Resources.Load<TileThemeSO>("TileTheme_Default");
            var typeCount = _level.TileLayout.Select(t => t.TileType.Value).Distinct().Count();
            _fruitSprites = LevelGenerator.SelectFruitSprites(_currentLevelIndex, typeCount, _tileTheme.FruitSprites);

            _board = BoardGenerator.CreateBoard(_level);
            _tray = new TrayModel(_level.TraySize);
            _stateMachine = new BoardStateMachine();
            _matchSystem = new MatchSystem(new ConsecutiveMatchRule(_level.MatchCount));
            _moveHistory.Clear();

            _boardController.Initialize(_board, _tileTheme.BaseTileSprite, _fruitSprites, OnTileSelected);
            _trayController.Initialize(_tray, _tileTheme, _fruitSprites);
            _hud.Initialize(this, _level, _currentLevelIndex);

            _isGameOver = false;
            _stateMachine.TrySetState(BoardState.Ready);
        }

        /// <summary>
        /// Tap handler. Accepts taps during Animating (another tile is still
        /// flying) so rapid multi-selections work — each tile is processed
        /// domain-side immediately and flies independently. Domain mutations
        /// happen up-front; every visible response (slot-icon reveal, match
        /// pop, board refresh, next-turn unlock) fires in the fly-landing
        /// callback so the tile is physically there before anything visible
        /// changes.
        /// </summary>
        private void OnTileSelected(TileId tileId)
        {
            if (_isGameOver) return;
            var state = _stateMachine.Current;
            if (state == BoardState.Paused || state == BoardState.Won || state == BoardState.Lost || state == BoardState.Loading) return;
            if (!_board.TryGetTile(tileId, out var tile) || !tile.IsSelectable) return;

            // Move to Animating on the first tap of a burst; subsequent taps in
            // the same burst stay in Animating (transition is a no-op).
            if (state == BoardState.Ready) _stateMachine.TrySetState(BoardState.Animating);

            var tileType = tile.TileType;

            if (!_tray.TryInsert(tileType, out var slotIndex))
            {
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

            var slotWorldPos = _trayController.GetSlotWorldPosition(slotIndex);
            var slotScale = _trayController.GetSlotIconWorldScale(slotIndex);

            _pendingFlies++;
            _boardController.FlyTileToSlot(tileId, slotWorldPos, slotScale, () =>
            {
                _trayController.ShowSlotIcon(slotIndex, tileType);
                _trayController.Refresh(completedMatch);
                _boardController.Refresh();
                _pendingFlies--;
                ResolveTurnOutcome();
            });
        }

        private void ResolveTurnOutcome()
        {
            if (_isGameOver) return;
            // Stay in Animating until every queued fly has landed — otherwise
            // an earlier fly's landing would flip state to Ready while later
            // flies are still in the air, letting a stray Undo/Shuffle fire
            // mid-air.
            if (_pendingFlies > 0) return;

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

            if (_stateMachine.Current == BoardState.Animating)
            {
                _stateMachine.TrySetState(BoardState.Ready);
            }
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
            if (_stateMachine.TrySetState(BoardState.Paused)) Time.timeScale = 0;
        }

        public void Resume()
        {
            if (_stateMachine.TrySetState(BoardState.Ready)) Time.timeScale = 1;
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

        public void Undo()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready || _moveHistory.Count == 0) return;
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
                if (!_board.TryGetTile(tileId, out var tile) || !tile.IsSelectable) continue;
                var type = tile.TileType;
                if (!_tray.TryInsert(type, out _)) continue;
                _board.TrySelectTile(tile);
                _moveHistory.Add(tileId);
                if (_matchSystem.TryEvaluate(_tray, type, out var matchResult))
                {
                    _tray.RemoveSlots(matchResult.SlotIndices);
                }
            }

            _boardController.Initialize(_board, _tileTheme.BaseTileSprite, _fruitSprites, OnTileSelected);
            _trayController.Initialize(_tray, _tileTheme, _fruitSprites);

            _isGameOver = false;
            _stateMachine.TrySetState(BoardState.Ready);
        }

        public void Shuffle()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready) return;

            var remainingCoords = new List<BoardCoordinate>();
            var remainingTypes = new List<TileTypeId>();
            foreach (var tile in _board.AllTiles)
            {
                if (tile.State == TileState.Removed) continue;
                remainingCoords.Add(tile.Coordinate);
                remainingTypes.Add(tile.TileType);
            }
            if (remainingCoords.Count == 0) return;

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
            _moveHistory.Clear();
            _boardController.Initialize(_board, _tileTheme.BaseTileSprite, _fruitSprites, OnTileSelected);
        }

        public void Hint()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready) return;
            foreach (var tile in _board.SelectableTiles)
            {
                _boardController.PulseTile(tile.Id);
                break;
            }
        }

        /// <summary>
        /// Removes one selectable tile. Flies it upward off-screen so the removal
        /// still reads as a physical action.
        /// </summary>
        public void Hammer()
        {
            if (_isGameOver || _stateMachine.Current != BoardState.Ready) return;

            TileModel target = null;
            foreach (var tile in _board.SelectableTiles) { target = tile; break; }
            if (target == null) return;

            if (_stateMachine.Current == BoardState.Ready) _stateMachine.TrySetState(BoardState.Animating);

            _board.TrySelectTile(target);

            var origin = _boardController.GetTileWorldPosition(target.Id);
            var offscreen = new Vector3(origin.x, _cameraOrthoSize + 3f, origin.z);
            _pendingFlies++;
            _boardController.FlyTileToSlot(target.Id, offscreen, Vector3.one * 0.3f, () =>
            {
                _boardController.Refresh();
                _pendingFlies--;
                ResolveTurnOutcome();
            });
        }
    }
}
