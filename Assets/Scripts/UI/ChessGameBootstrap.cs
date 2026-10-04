using System;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Scene entry point: creates the chess game, the selection pipeline, the BCI and the UI, and wires input to them.
    /// Both sides are currently played by humans on one machine; the computer opponent is added in a later phase.
    /// </summary>
    public sealed class ChessGameBootstrap : MonoBehaviour
    {
        [SerializeField] private BoardTheme theme = new BoardTheme();

        [Tooltip("Optional FEN to start from. Leave empty for the standard starting position.")]
        [SerializeField] private string startFen = "";

        [Tooltip("Show the board from Black's side.")]
        [SerializeField] private bool flipBoard = false;

        [SerializeField] private BciSettings bci = new BciSettings();

        private ChessGame _game;
        private SelectionController _selection;
        private BoardView _board;
        private GameHudView _hud;
        private KeyboardBoardInput _keyboard;
        private BciSelectionController _bci;
        private BciTargetOverlay _bciOverlay;

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

            SetupBci();
            Render();
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
            _bci?.Dispose();
            _selection?.Dispose();
        }

        private void SetupBci()
        {
            _bciOverlay = _board.gameObject.AddComponent<BciTargetOverlay>();
            _bciOverlay.Build(_board, theme);

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

            var fake = new FakeBciSelector();
            gameObject.AddComponent<FakeBciKeyboardInput>().Initialize(fake);

            var options = new BciSelectionOptions
            {
                SelectionTimeoutSeconds = bci.selectionTimeoutSeconds,
                OfferCancelTarget = bci.offerCancelTarget,
                AutoSelectSingleCandidate = bci.autoSelectSingleCandidate
            };
            _bci = new BciSelectionController(_selection, fake, stimuli, options);
            _bci.Changed += Render;
            _bci.Enabled = bci.enabledOnStart;
        }

        private void NewGame() => _game.Reset(ResolveStartFen());

        private void Undo() => _game.Undo();

        private void Flip()
        {
            _board.Flipped = !_board.Flipped;
            Render();
        }

        private void Render()
        {
            _board.Render(_game, _selection, _keyboard.CursorVisible ? _keyboard.Cursor : (Square?)null);
            _hud.Render(_game, _selection);
            _hud.RenderBci(_bci);
            if (_bciOverlay != null)
                _bciOverlay.Render(_bci != null ? _bci.Targets : Array.Empty<BciTarget>());
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
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)canvasObject.transform;

            var background = UiFactory.CreateImage("Background", root, theme.background);
            UiFactory.Stretch(background.rectTransform);

            var frame = UiFactory.CreateImage("BoardFrame", root, theme.boardFrame);
            UiFactory.Place(frame.rectTransform, new Vector2(-250f, 0f), new Vector2(920f, 920f));

            var boardRect = UiFactory.CreateRect("Board", frame.transform);
            UiFactory.Stretch(boardRect, 20f);
            _board = boardRect.gameObject.AddComponent<BoardView>();
            _board.Build(theme, glyphFont);
            _board.Flipped = flipBoard;

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
