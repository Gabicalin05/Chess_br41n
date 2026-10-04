using System;
using System.IO;
using BciChess.Core;
using UnityEngine;

namespace BciChess.UI
{
    /// <summary>Computer opponent configuration, tunable in the inspector.</summary>
    [Serializable]
    public sealed class ComputerSettings
    {
        [Tooltip("Play against the computer. When off, both sides are played on this machine.")]
        public bool playAgainstComputer = true;

        public PieceColor computerPlays = PieceColor.Black;

        [Tooltip("Stockfish executable. Relative paths are resolved against Assets/StreamingAssets.")]
        public string stockfishPath = "Stockfish/stockfish.exe";

        [Tooltip("Stockfish skill level: 0 = weakest, 20 = full strength.")]
        [Range(0, 20)] public int skillLevel = 5;

        [Tooltip("Thinking time per move in milliseconds (used when search depth is 0).")]
        [Min(50)] public int moveTimeMs = 800;

        [Tooltip("Fixed search depth; 0 = use the thinking time instead.")]
        [Min(0)] public int searchDepth = 0;

        [Tooltip("Show 'Computer thinking...' for at least this long, so moves don't appear instantly.")]
        [Min(0f)] public float minimumThinkSeconds = 0.8f;

        [Tooltip("If Stockfish is missing or crashes, continue with a simple built-in opponent.")]
        public bool useFallbackOpponent = true;

        public string ResolveStockfishPath()
        {
            if (string.IsNullOrWhiteSpace(stockfishPath))
                return string.Empty;
            return Path.IsPathRooted(stockfishPath)
                ? stockfishPath
                : Path.Combine(Application.streamingAssetsPath, stockfishPath);
        }
    }
}
