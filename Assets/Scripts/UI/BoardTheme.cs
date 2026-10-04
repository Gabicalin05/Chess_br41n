using System;
using UnityEngine;

namespace BciChess.UI
{
    public enum PieceGlyphStyle
    {
        /// <summary>Unicode chess symbols from an OS font (Segoe UI Symbol on Windows).</summary>
        UnicodeSymbols,

        /// <summary>Plain letters (K, Q, R, B, N, P); works with any font.</summary>
        Letters
    }

    /// <summary>Colours and glyph settings for the board and HUD. Tweak in the inspector.</summary>
    [Serializable]
    public sealed class BoardTheme
    {
        [Header("Layout")]
        public Color background = new Color32(0x1B, 0x1F, 0x27, 0xFF);
        public Color panel = new Color32(0x26, 0x2B, 0x35, 0xFF);
        public Color boardFrame = new Color32(0x12, 0x15, 0x1B, 0xFF);

        [Header("Squares")]
        public Color lightSquare = new Color32(0xEE, 0xD8, 0xB5, 0xFF);
        public Color darkSquare = new Color32(0xB0, 0x87, 0x63, 0xFF);

        [Header("Highlights")]
        public Color lastMoveTint = new Color(1f, 0.85f, 0.2f, 0.38f);
        public Color selectedTint = new Color(0.2f, 0.6f, 1f, 0.6f);
        public Color selectableTint = new Color(0.2f, 0.6f, 1f, 0.2f);
        public Color checkTint = new Color(0.9f, 0.1f, 0.1f, 0.65f);
        public Color destinationMarker = new Color(0.08f, 0.08f, 0.1f, 0.38f);
        public Color cursor = new Color(0.15f, 0.95f, 1f, 1f);

        [Header("Pieces")]
        public PieceGlyphStyle glyphStyle = PieceGlyphStyle.UnicodeSymbols;
        [Tooltip("OS fonts tried in order for Unicode chess symbols.")]
        public string[] glyphFontNames = { "Segoe UI Symbol", "DejaVu Sans", "Arial Unicode MS" };
        public Color whitePiece = new Color(0.98f, 0.97f, 0.94f);
        public Color whitePieceOutline = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        public Color blackPiece = new Color(0.1f, 0.1f, 0.12f);
        public Color blackPieceOutline = new Color(0.9f, 0.9f, 0.9f, 0.55f);

        [Header("Text & buttons")]
        public Color text = new Color(0.93f, 0.94f, 0.96f);
        public Color mutedText = new Color(0.6f, 0.64f, 0.7f);
        public Color accent = new Color32(0x4F, 0xA3, 0xFF, 0xFF);
        public Color warning = new Color32(0xFF, 0x5A, 0x4F, 0xFF);
        public Color button = new Color32(0x3A, 0x41, 0x50, 0xFF);
        public Color buttonText = new Color(0.95f, 0.96f, 0.98f);
    }
}
