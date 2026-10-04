using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Side panel (turn, prompt, move list, buttons), promotion picker and game-over banner.</summary>
    public sealed class GameHudView : MonoBehaviour
    {
        private const int MaxMoveLines = 5;

        private BoardTheme _theme;
        private Text _turnText;
        private Text _promptText;
        private Text _stateText;
        private Text _movesText;
        private Button _undoButton;
        private GameObject _promotionOverlay;
        private readonly List<Text> _promotionGlyphs = new List<Text>();
        private readonly List<Outline> _promotionOutlines = new List<Outline>();
        private GameObject _gameOverBanner;
        private Text _gameOverText;
        private Text _bciStatusText;
        private Text _bciTargetsText;
        private Text _bciMessageText;
        private readonly List<Text> _promotionKeyLabels = new List<Text>();
        private Text _promotionCancelLabel;
        private readonly Dictionary<PieceType, RectTransform> _promotionButtons = new Dictionary<PieceType, RectTransform>();
        private RectTransform _promotionCancelButton;
        private Image _listeningDot;
        private Text _listeningText;
        private bool _listening;

        /// <summary>The promotion picker button for <paramref name="type"/> (for attaching BCI visuals).</summary>
        public RectTransform GetPromotionButton(PieceType type) => _promotionButtons[type];

        public RectTransform PromotionCancelButton => _promotionCancelButton;

        public event Action NewGameClicked;
        public event Action UndoClicked;
        public event Action FlipClicked;
        public event Action<PieceType> PromotionChosen;
        public event Action PromotionCancelled;

        public void Build(RectTransform canvasRoot, RectTransform panel, RectTransform boardFrame, BoardTheme theme,
            Font glyphFont)
        {
            _theme = theme;
            BuildSidePanel(panel);
            BuildGameOverBanner(boardFrame);
            BuildPromotionOverlay(canvasRoot, boardFrame, glyphFont);
        }

        public void Render(ChessGame game, SelectionController selection)
        {
            if (game.IsGameOver)
            {
                _turnText.text = "Game over";
                _turnText.color = _theme.text;
            }
            else
            {
                _turnText.text = game.SideToMove + " to move" + (game.IsInCheck ? "  -  CHECK!" : "");
                _turnText.color = game.IsInCheck ? _theme.warning : _theme.text;
            }

            _promptText.text = Prompt(game, selection);
            _stateText.text = "State: " + ToConstantName(selection.State.ToString());
            _movesText.text = FormatMoves(game.MoveHistory);
            _undoButton.interactable = game.MoveHistory.Count > 0;

            _gameOverBanner.SetActive(game.IsGameOver);
            if (game.IsGameOver)
                _gameOverText.text = ResultText(game);

            bool promoting = selection.State == InteractionState.SelectingPromotion;
            _promotionOverlay.SetActive(promoting);
            if (promoting)
            {
                var color = game.SideToMove;
                for (int i = 0; i < _promotionGlyphs.Count; i++)
                {
                    var piece = new Piece(SelectionController.PromotionPieces[i], color);
                    _promotionGlyphs[i].text = PieceGlyphs.Get(piece, _theme.glyphStyle);
                    _promotionGlyphs[i].color = color == PieceColor.White ? _theme.whitePiece : _theme.blackPiece;
                    _promotionOutlines[i].effectColor =
                        color == PieceColor.White ? _theme.whitePieceOutline : _theme.blackPieceOutline;
                }
            }
        }

        /// <summary>Shows BCI status and the current targets with their slot keys. Pass null when BCI is off.</summary>
        public void RenderBci(BciSelectionController bci)
        {
            var slotByTarget = new Dictionary<string, string>();
            _listening = bci != null && bci.Status == BciSessionStatus.AwaitingSelection;
            _listeningDot.enabled = _listening;
            _listeningText.enabled = _listening;
            if (bci == null)
            {
                _bciStatusText.text = "Off (mouse and keyboard only)";
                _bciTargetsText.text = string.Empty;
                _bciMessageText.text = string.Empty;
            }
            else
            {
                _bciStatusText.text = BciStatusText(bci);
                _bciMessageText.text = bci.Message;

                var list = new StringBuilder();
                foreach (var target in bci.Targets)
                {
                    string key = FakeBciKeyboardInput.KeyLabel(target.Stimulus.Value.Index);
                    slotByTarget[target.Id] = key;
                    if (list.Length > 0)
                        list.Append("    ");
                    list.Append('[').Append(key).Append("] ").Append(target.Label);
                }
                _bciTargetsText.text = list.ToString();
            }

            var pieces = SelectionController.PromotionPieces;
            for (int i = 0; i < _promotionKeyLabels.Count; i++)
            {
                string letter = MoveNotation.PieceLetter(pieces[i]);
                _promotionKeyLabels[i].text = slotByTarget.TryGetValue("promo:" + pieces[i], out var key)
                    ? $"{letter}  /  BCI {key}"
                    : letter;
            }
            _promotionCancelLabel.text = slotByTarget.TryGetValue("cancel", out var cancelKey)
                ? $"Cancel (Esc / BCI {cancelKey})"
                : "Cancel (Esc)";
        }

        private void Update()
        {
            if (!_listening)
                return;
            // Slow pulse so the player can see at a glance that the BCI is waiting for input.
            var color = _theme.warning;
            color.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            _listeningDot.color = color;
        }

        private static string BciStatusText(BciSelectionController bci)
        {
            switch (bci.Status)
            {
                case BciSessionStatus.Disabled:
                    return $"{bci.Selector.Name} - paused (F3 to resume)";
                case BciSessionStatus.Idle:
                    return $"{bci.Selector.Name} - idle";
                case BciSessionStatus.AwaitingSelection:
                    bool grouped = bci.Targets.Any(t => t.Payload is CandidateGroup);
                    string step = bci.Depth > 0 ? "inside group - choose an option or Back"
                        : grouped ? $"{bci.CandidateCount} options grouped - choose a group"
                        : "choose an option";
                    return $"{bci.Selector.Name}: {step}";
                case BciSessionStatus.TooManyCandidates:
                    return $"{bci.CandidateCount} options do not fit {bci.Capacity} BCI targets, even grouped - use mouse/keyboard";
                case BciSessionStatus.Unavailable:
                    return $"{bci.Selector.Name} - unavailable";
                default:
                    return string.Empty;
            }
        }

        public static string ResultText(ChessGame game)
        {
            switch (game.Status)
            {
                case GameStatus.Checkmate: return $"Checkmate - {game.Winner} wins";
                case GameStatus.Stalemate: return "Draw - stalemate";
                case GameStatus.DrawFiftyMoveRule: return "Draw - fifty-move rule";
                case GameStatus.DrawInsufficientMaterial: return "Draw - insufficient material";
                case GameStatus.DrawThreefoldRepetition: return "Draw - threefold repetition";
                default: return string.Empty;
            }
        }

        private static string Prompt(ChessGame game, SelectionController selection)
        {
            switch (selection.State)
            {
                case InteractionState.SelectingPiece:
                    return "Select a piece";
                case InteractionState.SelectingDestination:
                    var square = selection.SelectedPiece.Value;
                    return $"Select destination for {game.Position[square].Type.ToString().ToLowerInvariant()} on {square}";
                case InteractionState.SelectingPromotion:
                    return "Choose a promotion piece";
                case InteractionState.WaitingForPlayer:
                    return "Waiting for player...";
                case InteractionState.ProcessingSelection:
                    return "Processing selection...";
                case InteractionState.ComputerThinking:
                    return "Computer thinking...";
                case InteractionState.GameOver:
                    return ResultText(game);
                default:
                    return string.Empty;
            }
        }

        private static string FormatMoves(IReadOnlyList<ChessMove> moves)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            int number = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                bool startsLine = move.Piece.Color == PieceColor.White || i == 0;
                if (startsLine)
                {
                    if (line.Length > 0)
                        lines.Add(line.ToString());
                    line.Clear();
                    number++;
                    line.Append(number).Append(". ");
                    if (move.Piece.Color == PieceColor.Black)
                        line.Append("...      ");
                }
                line.Append(MoveNotation.ToLongAlgebraic(move)).Append("      ");
            }
            if (line.Length > 0)
                lines.Add(line.ToString());

            if (lines.Count == 0)
                return "-";
            int first = Mathf.Max(0, lines.Count - MaxMoveLines);
            return string.Join("\n", lines.GetRange(first, lines.Count - first));
        }

        /// <summary>"SelectingPiece" -> "SELECTING_PIECE".</summary>
        private static string ToConstantName(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append('_');
                sb.Append(char.ToUpperInvariant(name[i]));
            }
            return sb.ToString();
        }

        private void BuildSidePanel(RectTransform panel)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 28);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = UiFactory.CreateText("Title", panel, "BCI Chess", 46, _theme.accent, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiFactory.AddLayout(title, 60f);

            _turnText = UiFactory.CreateText("Turn", panel, "", 34, _theme.text, TextAnchor.MiddleLeft);
            _turnText.fontStyle = FontStyle.Bold;
            UiFactory.AddLayout(_turnText, 48f);

            _promptText = UiFactory.CreateText("Prompt", panel, "", 28, _theme.text, TextAnchor.UpperLeft);
            UiFactory.AddLayout(_promptText, 64f);

            _stateText = UiFactory.CreateText("State", panel, "", 18, _theme.mutedText, TextAnchor.MiddleLeft);
            UiFactory.AddLayout(_stateText, 26f);

            var divider = UiFactory.CreateImage("Divider", panel, new Color(1f, 1f, 1f, 0.08f));
            UiFactory.AddLayout(divider, 2f);

            var movesHeader = UiFactory.CreateText("MovesHeader", panel, "MOVES", 20, _theme.mutedText, TextAnchor.LowerLeft);
            UiFactory.AddLayout(movesHeader, 30f);

            _movesText = UiFactory.CreateText("Moves", panel, "-", 23, _theme.text, TextAnchor.UpperLeft);
            _movesText.lineSpacing = 1.05f;
            UiFactory.AddLayout(_movesText, 140f, flexibleHeight: 1f);

            var bciHeader = UiFactory.CreateText("BciHeader", panel, "BCI", 20, _theme.mutedText, TextAnchor.LowerLeft);
            UiFactory.AddLayout(bciHeader, 28f);

            _listeningDot = UiFactory.CreateImage("ListeningDot", bciHeader.transform, _theme.warning, UiFactory.Circle);
            var dotRect = _listeningDot.rectTransform;
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 0.4f);
            dotRect.sizeDelta = new Vector2(16f, 16f);
            dotRect.anchoredPosition = new Vector2(62f, 0f);
            _listeningDot.enabled = false;

            _listeningText = UiFactory.CreateText("ListeningLabel", bciHeader.transform, "LISTENING", 18,
                _theme.warning, TextAnchor.LowerLeft);
            UiFactory.Stretch(_listeningText.rectTransform);
            _listeningText.rectTransform.offsetMin = new Vector2(80f, 0f);
            _listeningText.enabled = false;

            _bciStatusText = UiFactory.CreateText("BciStatus", panel, "", 21, _theme.accent, TextAnchor.UpperLeft);
            UiFactory.AddLayout(_bciStatusText, 52f);

            _bciTargetsText = UiFactory.CreateText("BciTargets", panel, "", 20, _theme.text, TextAnchor.UpperLeft);
            _bciTargetsText.lineSpacing = 1.1f;
            UiFactory.AddLayout(_bciTargetsText, 96f);

            _bciMessageText = UiFactory.CreateText("BciMessage", panel, "", 19, _theme.warning, TextAnchor.UpperLeft);
            UiFactory.AddLayout(_bciMessageText, 24f);

            var buttonRow = UiFactory.CreateRect("Buttons", panel);
            var rowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            UiFactory.AddLayout(buttonRow, 64f);

            UiFactory.CreateButton("NewGame", buttonRow, "New game", _theme, () => NewGameClicked?.Invoke());
            _undoButton = UiFactory.CreateButton("Undo", buttonRow, "Undo", _theme, () => UndoClicked?.Invoke());
            UiFactory.CreateButton("Flip", buttonRow, "Flip board", _theme, () => FlipClicked?.Invoke());

            var help = UiFactory.CreateText("Help", panel,
                "Mouse: click a piece, then a destination. Right-click cancels.\n" +
                "Keyboard: arrows move the cursor, Enter/Space select, Tab cycles candidates, Esc cancels.\n" +
                "Q/R/B/N promote - Backspace undo - F2 new game - F flip board\n" +
                "Simulated BCI: number keys 1-9, 0 pick target slots 1-10 - F3 pauses BCI",
                17, _theme.mutedText, TextAnchor.LowerLeft);
            UiFactory.AddLayout(help, 110f);
        }

        private void BuildGameOverBanner(RectTransform boardFrame)
        {
            var banner = UiFactory.CreateImage("GameOverBanner", boardFrame, new Color(0.05f, 0.06f, 0.08f, 0.88f));
            UiFactory.Place(banner.rectTransform, Vector2.zero, new Vector2(720f, 180f));
            _gameOverBanner = banner.gameObject;

            _gameOverText = UiFactory.CreateText("Result", banner.transform, "", 44, _theme.text, TextAnchor.MiddleCenter);
            _gameOverText.fontStyle = FontStyle.Bold;
            UiFactory.SetAnchors(_gameOverText.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 1f));

            var hint = UiFactory.CreateText("Hint", banner.transform, "New game (F2)  -  Undo (Backspace)", 22,
                _theme.mutedText, TextAnchor.MiddleCenter);
            UiFactory.SetAnchors(hint.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.4f));

            _gameOverBanner.SetActive(false);
        }

        private void BuildPromotionOverlay(RectTransform canvasRoot, RectTransform boardFrame, Font glyphFont)
        {
            // Full-screen dimmer blocks clicks on the board while choosing.
            var dimmer = UiFactory.CreateImage("PromotionOverlay", canvasRoot, new Color(0f, 0f, 0f, 0.55f),
                raycastTarget: true);
            UiFactory.Stretch(dimmer.rectTransform);
            _promotionOverlay = dimmer.gameObject;

            var box = UiFactory.CreateImage("Box", dimmer.transform, _theme.panel);
            UiFactory.Place(box.rectTransform, boardFrame.anchoredPosition, new Vector2(640f, 330f));

            var title = UiFactory.CreateText("Title", box.transform, "Promote pawn to", 32, _theme.text,
                TextAnchor.MiddleCenter);
            UiFactory.SetAnchors(title.rectTransform, new Vector2(0f, 0.78f), new Vector2(1f, 0.97f));

            var pieces = SelectionController.PromotionPieces;
            const float buttonSize = 124f;
            const float spacing = 22f;
            float totalWidth = pieces.Count * buttonSize + (pieces.Count - 1) * spacing;
            for (int i = 0; i < pieces.Count; i++)
            {
                var type = pieces[i];
                var button = UiFactory.CreateImage(type.ToString(), box.transform, _theme.lightSquare, null, true)
                    .gameObject.AddComponent<Button>();
                UiFactory.DisableKeyboardSubmit(button);
                button.onClick.AddListener(() => PromotionChosen?.Invoke(type));
                float x = -totalWidth / 2f + buttonSize / 2f + i * (buttonSize + spacing);
                UiFactory.Place((RectTransform)button.transform, new Vector2(x, 10f), new Vector2(buttonSize, buttonSize));
                _promotionButtons[type] = (RectTransform)button.transform;

                var glyph = UiFactory.CreateText("Glyph", button.transform, "", 92, _theme.whitePiece,
                    TextAnchor.MiddleCenter, glyphFont);
                glyph.horizontalOverflow = HorizontalWrapMode.Overflow;
                glyph.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.Stretch(glyph.rectTransform);
                var outline = glyph.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                _promotionGlyphs.Add(glyph);
                _promotionOutlines.Add(outline);

                var key = UiFactory.CreateText("Key", box.transform, MoveNotation.PieceLetter(type), 22,
                    _theme.mutedText, TextAnchor.MiddleCenter);
                key.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.Place(key.rectTransform, new Vector2(x, 10f - buttonSize / 2f - 20f), new Vector2(buttonSize, 30f));
                _promotionKeyLabels.Add(key);
            }

            var cancel = UiFactory.CreateButton("Cancel", box.transform, "Cancel (Esc)", _theme,
                () => PromotionCancelled?.Invoke(), 22);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(0f, -132f), new Vector2(300f, 44f));
            _promotionCancelLabel = cancel.GetComponentInChildren<Text>();
            _promotionCancelButton = (RectTransform)cancel.transform;

            _promotionOverlay.SetActive(false);
        }
    }
}
