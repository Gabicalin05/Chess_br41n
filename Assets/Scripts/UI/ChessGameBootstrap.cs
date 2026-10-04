using System;
using System.Threading.Tasks;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Engine;
using BciChess.Interaction;
using BciChess.Unicorn;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Scene entry point: creates the chess game, the selection pipeline, the BCI, the computer opponent and the UI,
    /// and wires them together.
    /// </summary>
    public sealed class ChessGameBootstrap : MonoBehaviour
    {
        [SerializeField] private BoardTheme theme = new BoardTheme();

        [Tooltip("Optional FEN to start from. Leave empty for the standard starting position.")]
        [SerializeField] private string startFen = "";

        [Tooltip("Show the board from Black's side.")]
        [SerializeField] private bool flipBoard = false;

        [SerializeField] private BciSettings bci = new BciSettings();

        [SerializeField] private ComputerSettings computer = new ComputerSettings();

        private ChessGame _game;
        private SelectionController _selection;
        private BoardView _board;
        private GameHudView _hud;
        private KeyboardBoardInput _keyboard;
        private BciSelectionController _bci;
        private BciCommandBar _commandBar;
        private ComputerPlayer _computer;
        private IChessEngine _engine;
        private IChessEngine _fallbackEngine;
        private UnicornBciRig _unicorn;
        private RectTransform _canvasRoot;
        private string _bciNotice = string.Empty;

        private const int GameCanvasSortingOrder = 0;

        public ChessGame Game => _game;
        public SelectionController Selection => _selection;

        private void Awake()
        {
            _game = new ChessGame(ResolveStartFen());
            _selection = new SelectionController(_game);

            BuildUi();

            _selection.StateChanged += Render;
            _keyboard.CursorChanged += Render;

            _board.SquareClicked += square => _selection.SelectSquare(square);
            _board.CancelRequested += _selection.Cancel;

            _hud.NewGameClicked += NewGame;
            _hud.UndoClicked += Undo;
            _hud.FlipClicked += Flip;
            _hud.PromotionChosen += type => _selection.SelectPromotion(type);
            _hud.PromotionCancelled += _selection.Cancel;

            _keyboard.NewGameRequested += NewGame;
            _keyboard.UndoRequested += Undo;
            _keyboard.FlipRequested += Flip;

            SetupComputer();
            SetupBci();
            Render();

            // Last, because the computer may move immediately when it plays White.
            if (_computer != null)
                _computer.Enabled = true;
        }

        private void Update()
        {
            if (_bci == null)
                return;
            if (Input.GetKeyDown(KeyCode.F3))
                _bci.Enabled = !_bci.Enabled;
            _bci.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _computer?.Dispose();
            _engine?.Dispose();
            _fallbackEngine?.Dispose();
            _bci?.Dispose();
            _selection?.Dispose();
        }

        private void SetupComputer()
        {
            if (!computer.playAgainstComputer)
                return;

            var stockfish = new StockfishClient(new StockfishOptions
            {
                ExecutablePath = computer.ResolveStockfishPath(),
                SkillLevel = computer.skillLevel,
                MoveTimeMs = computer.moveTimeMs,
                Depth = computer.searchDepth
            });
            stockfish.Diagnostic += message => Debug.Log(message);
            _engine = stockfish;
            _fallbackEngine = computer.useFallbackOpponent ? new SimpleEngine() : null;

            _computer = new ComputerPlayer(_game, _engine, computer.computerPlays, _fallbackEngine,
                computer.minimumThinkSeconds);
            _computer.StateChanged += Render;
            _selection.ComputerSide = computer.computerPlays;

            // Keep the human's pieces at the bottom.
            if (computer.computerPlays == PieceColor.White)
                _board.Flipped = !flipBoard;

            _ = WarmUpAsync(stockfish);
        }

        /// <summary>Starts Stockfish in the background so the first move is quick and a missing binary is reported early.</summary>
        private async Task WarmUpAsync(StockfishClient stockfish)
        {
            try
            {
                await stockfish.StartAsync();
                Debug.Log($"Stockfish ready ({computer.ResolveStockfishPath()}).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Stockfish unavailable: {e.Message}" +
                                 (computer.useFallbackOpponent ? " The built-in opponent will be used." : ""));
            }
        }

        private void SetupBci()
        {
            if (bci.mode == BciMode.Off)
                return;

            StimulusManager stimuli;
            try
            {
                stimuli = new StimulusManager(bci.stimulusClassIds ?? Array.Empty<int>(), bci.maxSimultaneousTargets);
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"BCI disabled: invalid stimulus configuration. {e.Message}", this);
                return;
            }

            IBciSelector selector = null;
            IStimulusSource stimulusSource = null;
            if (bci.mode == BciMode.Unicorn)
            {
                try
                {
                    // The g.tec UI (connect, signal quality, training) is drawn above the game UI.
                    _unicorn = UnicornBciRig.Create(bci.unicorn, stimuli.Slots, GameCanvasSortingOrder + 10);
                    selector = _unicorn.Selector;
                    stimulusSource = _unicorn.Selector;
                    _unicorn.StateChanged += Render;
                    gameObject.AddComponent<BciCalibrationView>().Build(_canvasRoot, theme, bci.visuals, _unicorn);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unicorn BCI setup failed: {e.Message}", this);
                    if (!bci.unicorn.fallBackToSimulated)
                        return;
                    _bciNotice = "Unicorn setup failed - using the simulated BCI. See the Console for details.";
                }
            }

            if (selector == null)
            {
                var fake = new FakeBciSelector();
                gameObject.AddComponent<FakeBciKeyboardInput>().Initialize(fake);
                selector = fake;
            }

            var options = new BciSelectionOptions
            {
                SelectionTimeoutSeconds = bci.selectionTimeoutSeconds,
                OfferCancelTarget = bci.offerCancelTarget,
                AutoSelectSingleCandidate = bci.autoSelectSingleCandidate
            };
            _bci = new BciSelectionController(_selection, selector, stimuli, options);
            _bci.Changed += Render;
            gameObject.AddComponent<BciStimulusPresenter>()
                .Initialize(_bci, bci.visuals, _board, _hud, _commandBar, stimulusSource);
            _bci.Enabled = bci.enabledOnStart;
        }

        private void NewGame() => _game.Reset(ResolveStartFen());

        /// <summary>Takes back the last move; against the computer, back to the human's previous turn.</summary>
        private void Undo()
        {
            if (!_game.Undo() || _computer == null)
                return;
            while (_game.SideToMove == _computer.Color && _game.MoveHistory.Count > 0)
                _game.Undo();
        }

        private void Flip()
        {
            _board.Flipped = !_board.Flipped;
            Render();
        }

        private void Render()
        {
            _board.Render(_game, _selection, _keyboard.CursorVisible ? _keyboard.Cursor : (Square?)null);
            _hud.Render(_game, _selection);
            _hud.RenderOpponent(_computer, computer);
            _hud.RenderBci(_bci, _bciNotice);
        }

        private string ResolveStartFen()
        {
            if (string.IsNullOrWhiteSpace(startFen))
                return ChessPosition.StartFen;
            try
            {
                ChessPosition.FromFen(startFen);
                return startFen;
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"Invalid start FEN, using the standard position instead. {e.Message}", this);
                return ChessPosition.StartFen;
            }
        }

        private void BuildUi()
        {
            EnsureEventSystem();
            var glyphFont = UiFactory.CreateGlyphFont(theme);

            var canvasObject = new GameObject("ChessCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = GameCanvasSortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)canvasObject.transform;
            _canvasRoot = root;

            var background = UiFactory.CreateImage("Background", root, theme.background);
            UiFactory.Stretch(background.rectTransform);

            var frame = UiFactory.CreateImage("BoardFrame", root, theme.boardFrame);
            UiFactory.Place(frame.rectTransform, new Vector2(-250f, 40f), new Vector2(880f, 880f));

            var boardRect = UiFactory.CreateRect("Board", frame.transform);
            UiFactory.Stretch(boardRect, 20f);
            _board = boardRect.gameObject.AddComponent<BoardView>();
            _board.Build(theme, glyphFont);
            _board.Flipped = flipBoard;

            // Off-board BCI targets (Cancel, Back) live in a strip under the board.
            var commandBarRect = UiFactory.CreateRect("BciCommandBar", root);
            UiFactory.Place(commandBarRect, new Vector2(-250f, -462f), new Vector2(880f, 76f));
            _commandBar = commandBarRect.gameObject.AddComponent<BciCommandBar>();
            _commandBar.Build(theme, bci.visuals);

            var panel = UiFactory.CreateImage("SidePanel", root, theme.panel);
            UiFactory.Place(panel.rectTransform, new Vector2(500f, 0f), new Vector2(540f, 920f));

            _hud = canvasObject.AddComponent<GameHudView>();
            _hud.Build(root, panel.rectTransform, frame.rectTransform, theme, glyphFont);

            _keyboard = gameObject.AddComponent<KeyboardBoardInput>();
            _keyboard.Initialize(_selection, _board);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
