using BciChess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Renders one chess piece glyph. Knows nothing about selection or BCI stimulation.</summary>
    public sealed class ChessPieceView : MonoBehaviour
    {
        private BoardTheme _theme;
        private Text _text;
        private Outline _outline;

        public Piece Piece { get; private set; }

        public void Build(BoardTheme theme, Font glyphFont)
        {
            _theme = theme;
            _text = gameObject.AddComponent<Text>();
            _text.font = glyphFont;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;

            _outline = gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(1.5f, -1.5f);

            UpdateFontSize();
            SetPiece(Piece.None);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_text != null)
                UpdateFontSize();
        }

        /// <summary>Scale the glyph with the square so the board works at any resolution.</summary>
        private void UpdateFontSize()
        {
            float height = ((RectTransform)transform).rect.height;
            float scale = _theme.glyphStyle == PieceGlyphStyle.Letters ? 0.6f : 0.82f;
            _text.fontSize = Mathf.Max(8, Mathf.RoundToInt(height * scale));
        }

        public void SetPiece(Piece piece)
        {
            Piece = piece;
            _text.text = PieceGlyphs.Get(piece, _theme.glyphStyle);
            bool white = piece.Color == PieceColor.White;
            _text.color = white ? _theme.whitePiece : _theme.blackPiece;
            _outline.effectColor = white ? _theme.whitePieceOutline : _theme.blackPieceOutline;
        }
    }

    public static class PieceGlyphs
    {
        public static string Get(Piece piece, PieceGlyphStyle style)
        {
            if (piece.IsNone)
                return string.Empty;
            if (style == PieceGlyphStyle.Letters)
                return piece.Type == PieceType.Pawn ? "P" : MoveNotation.PieceLetter(piece.Type);

            // Filled symbols for both colours; colour comes from the text tint.
            switch (piece.Type)
            {
                case PieceType.King: return "♚";
                case PieceType.Queen: return "♛";
                case PieceType.Rook: return "♜";
                case PieceType.Bishop: return "♝";
                case PieceType.Knight: return "♞";
                default: return "♟";
            }
        }
    }
}
