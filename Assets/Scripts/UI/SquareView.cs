using System;
using BciChess.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Everything a square needs to draw itself for one frame.</summary>
    public struct SquareVisual
    {
        public Piece Piece;
        public bool IsLastMove;
        public bool IsSelected;
        public bool IsSelectable;
        public bool IsDestination;
        public bool IsCaptureDestination;
        public bool IsCheck;
        public bool HasCursor;
    }

    /// <summary>One board square: background, highlight tint, move marker, cursor frame and coordinate labels.</summary>
    public sealed class SquareView : MonoBehaviour, IPointerClickHandler
    {
        private BoardTheme _theme;
        private Image _tint;
        private Image _marker;
        private Image _cursor;
        private Text _fileLabel;
        private Text _rankLabel;

        public event Action<SquareView, PointerEventData.InputButton> Clicked;

        public Square Square { get; private set; }
        public ChessPieceView PieceView { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public void Build(Square square, BoardTheme theme, Font glyphFont)
        {
            Square = square;
            _theme = theme;

            var background = gameObject.AddComponent<Image>();
            background.color = square.IsLight ? theme.lightSquare : theme.darkSquare;
            background.raycastTarget = true;

            _tint = UiFactory.CreateImage("Tint", transform, Color.clear);
            UiFactory.Stretch(_tint.rectTransform);

            _marker = UiFactory.CreateImage("MoveMarker", transform, theme.destinationMarker, UiFactory.Circle);
            _marker.enabled = false;

            var labelColor = square.IsLight ? theme.darkSquare : theme.lightSquare;
            _fileLabel = UiFactory.CreateText("FileLabel", transform, ((char)('a' + square.File)).ToString(), 18,
                labelColor, TextAnchor.LowerRight);
            UiFactory.Stretch(_fileLabel.rectTransform, 5f);
            _rankLabel = UiFactory.CreateText("RankLabel", transform, (square.Rank + 1).ToString(), 18,
                labelColor, TextAnchor.UpperLeft);
            UiFactory.Stretch(_rankLabel.rectTransform, 5f);

            var pieceRect = UiFactory.CreateRect("Piece", transform);
            UiFactory.Stretch(pieceRect, 4f);
            PieceView = pieceRect.gameObject.AddComponent<ChessPieceView>();
            PieceView.Build(theme, glyphFont);

            _cursor = UiFactory.CreateImage("KeyboardCursor", transform, theme.cursor, UiFactory.Frame);
            _cursor.type = Image.Type.Sliced;
            UiFactory.Stretch(_cursor.rectTransform);
            _cursor.enabled = false;
        }

        public void SetCoordinateLabels(bool showFile, bool showRank)
        {
            _fileLabel.enabled = showFile;
            _rankLabel.enabled = showRank;
        }

        public void Render(in SquareVisual visual)
        {
            PieceView.SetPiece(visual.Piece);

            if (visual.IsCheck)
                _tint.color = _theme.checkTint;
            else if (visual.IsSelected)
                _tint.color = _theme.selectedTint;
            else if (visual.IsSelectable)
                _tint.color = _theme.selectableTint;
            else if (visual.IsLastMove)
                _tint.color = _theme.lastMoveTint;
            else
                _tint.color = Color.clear;

            _marker.enabled = visual.IsDestination;
            if (visual.IsDestination)
            {
                // Dot for quiet moves, ring around the victim for captures.
                _marker.sprite = visual.IsCaptureDestination ? UiFactory.Ring : UiFactory.Circle;
                if (visual.IsCaptureDestination)
                    UiFactory.SetAnchors(_marker.rectTransform, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f));
                else
                    UiFactory.SetAnchors(_marker.rectTransform, new Vector2(0.34f, 0.34f), new Vector2(0.66f, 0.66f));
            }

            _cursor.enabled = visual.HasCursor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this, eventData.button);
        }
    }
}
