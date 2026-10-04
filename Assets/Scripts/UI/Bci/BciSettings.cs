using System;
using UnityEngine;

namespace BciChess.UI
{
    public enum BciMode
    {
        /// <summary>No BCI; mouse and keyboard only.</summary>
        Off,

        /// <summary>Fake BCI driven by number keys.</summary>
        Simulated
    }

    /// <summary>BCI configuration, tunable in the inspector.</summary>
    [Serializable]
    public sealed class BciSettings
    {
        public BciMode mode = BciMode.Simulated;

        [Tooltip("Start the game with BCI selection active (toggle at runtime with F3).")]
        public bool enabledOnStart = true;

        [Tooltip("ERP class ids used as stimuli, in assignment order. Must match the flash tag class ids " +
                 "configured in the g.tec ERP paradigm.")]
        public int[] stimulusClassIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        [Tooltip("Maximum number of targets presented at once. More candidates are split into groups. " +
                 "The simulated BCI has keys for the first 10 slots.")]
        [Min(1)] public int maxSimultaneousTargets = 6;

        [Tooltip("Choose automatically when there is only one candidate (one movable piece or one destination).")]
        public bool autoSelectSingleCandidate = true;

        [Tooltip("Restart a selection if no result arrives within this many seconds (0 = wait forever).")]
        [Min(0f)] public float selectionTimeoutSeconds = 0f;

        [Tooltip("Offer a 'Cancel' target while choosing a destination or promotion piece.")]
        public bool offerCancelTarget = true;
    }
}
