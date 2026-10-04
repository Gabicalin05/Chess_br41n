using System;
using System.Collections.Generic;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BciChess.UI
{
    /// <summary>
    /// The 8x8 board. Presents the state of <see cref="ChessGame"/> and <see cref="SelectionController"/>
    /// and reports clicks; it contains no chess rules.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly SquareView[] _squares = new SquareView[64];
        private bool _flipped;

        /// <summary>Left click on a square.</summary>
        public event Action<Square> SquareClicked;

        /// <summary>Right click anywhere on the board.</summary>
        public event Action CancelRequested;

        /// <summary>When true, Black is at the bottom.</summary>
        public bool Flipped
        {
            get => _flipped;
            set
            {
                _flipped = value;
                Layout();
            }
        }

        public SquareView GetSquareView(Square square) => _squares[square.Index];

        public void Build(BoardTheme theme, Font glyphFont)
        {
            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                var rect = UiFactory.CreateRect(square.ToString(), transform);
                var view = rect.gameObject.AddComponent<SquareView>();
                view.Build(square, theme, glyphFont);
                view.Clicked += OnSquareClicked;
                _squares[i] = view;
            }
            Layout();
        }

        public void Render(ChessGame game, SelectionController selection, Square? cursor)
        {
            var lastMove = game.LastMove;
            Square? checkedKing = null;
            if (game.IsInCheck && game.Position.TryFindKing(game.SideToMove, out var king))
                checkedKing = king;

            var selectable = selection.State == InteractionState.SelectingPiece
                ? selection.SelectablePieces
                : (IReadOnlyList<Square>)Array.Empty<Square>();
            var destinations = selection.SelectableDestinations;

            var captureSquares = new HashSet<Square>();
            if (selection.SelectedPiece.HasValue)
            {
                foreach (var move in game.GetLegalMoves(selection.SelectedPiece.Value))
                {
                    if (move.IsCapture)
                        captureSquares.Add(move.To);
                }
            }

            for (int i = 0; i < 64; i++)
            {
                var square = new Square(i);
                bool isDestination = Contains(destinations, square);
                var visual = new SquareVisual
                {
                    Piece = game.Position[square],
                    IsLastMove = lastMove.HasValue && (lastMove.Value.From == square || lastMove.Value.To == square),
                    IsSelected = selection.SelectedPiece == square || selection.PromotionTarget == square,
                    IsSelectable = Contains(selectable, square),
                    IsDestination = isDestination,
                    IsCaptureDestination = isDestination && captureSquares.Contains(square),
                    IsCheck = checkedKing == square,
                    HasCursor = cursor == square
                };
                _squares[i].Render(visual);
            }
        }

        /// <summary>Converts a step in screen direction (right/up) into a step in board files/ranks.</summary>
        public Vector2Int ScreenStepToBoardStep(int right, int up) =>
            _flipped ? new Vector2Int(-right, -up) : new Vector2Int(right, up);

        private void Layout()
        {
            foreach (var view in _squares)
            {
                if (view == null)
                    continue;
                int column = _flipped ? 7 - view.Square.File : view.Square.File;
                int row = _flipped ? 7 - view.Square.Rank : view.Square.Rank;
                UiFactory.SetAnchors(view.Rect, new Vector2(column / 8f, row / 8f),
                    new Vector2((column + 1) / 8f, (row + 1) / 8f));
                view.SetCoordinateLabels(showFile: row == 0, showRank: column == 0);
            }
        }

        private void OnSquareClicked(SquareView view, PointerEventData.InputButton button)
        {
            if (button == PointerEventData.InputButton.Left)
                SquareClicked?.Invoke(view.Square);
            else if (button == PointerEventData.InputButton.Right)
                CancelRequested?.Invoke();
        }

        private static bool Contains(IReadOnlyList<Square> squares, Square square)
        {
            for (int i = 0; i < squares.Count; i++)
            {
                if (squares[i] == square)
                    return true;
            }
            return false;
        }
    }
}
