using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// Simulated BCI for development and demos without the headset. Something external (keyboard input,
    /// tests) reports which stimulus the "player" attended to via <see cref="TrySelectSlot"/>.
    /// </summary>
    public sealed class FakeBciSelector : IBciSelector
    {
        private static readonly IReadOnlyList<BciTarget> NoTargets = Array.Empty<BciTarget>();

        private IReadOnlyList<BciTarget> _targets = NoTargets;
        private bool _isAvailable = true;

        public string Name => "Simulated (keyboard)";
        public bool IsAvailable => _isAvailable;
        public bool IsSelecting { get; private set; }
        public IReadOnlyList<BciTarget> CurrentTargets => IsSelecting ? _targets : NoTargets;

        public event Action AvailabilityChanged;
        public event Action<BciSelectionResult> SelectionFinished;

        public void StartSelection(IReadOnlyList<BciTarget> targets)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            foreach (var target in targets)
            {
                if (!target.Stimulus.HasValue)
                    throw new ArgumentException($"Target '{target.Id}' has no stimulus assigned.", nameof(targets));
            }

            _targets = targets;
            IsSelecting = _isAvailable;
        }

        public void StopSelection()
        {
            IsSelecting = false;
            _targets = NoTargets;
        }

        /// <summary>Simulates the player attending to the stimulus in slot <paramref name="slotIndex"/>.</summary>
        /// <returns>False when idle; otherwise true (an unknown slot finishes with an Invalid result).</returns>
        public bool TrySelectSlot(int slotIndex)
        {
            if (!IsSelecting)
                return false;

            foreach (var target in _targets)
            {
                if (target.Stimulus.Value.Index == slotIndex)
                {
                    Finish(BciSelectionResult.Selected(target));
                    return true;
                }
            }
            Finish(BciSelectionResult.Invalid($"No target on slot {slotIndex + 1}."));
            return true;
        }

        /// <summary>Simulates connecting/disconnecting the device.</summary>
        public void SetAvailable(bool available)
        {
            if (_isAvailable == available)
                return;
            _isAvailable = available;
            if (!available && IsSelecting)
                Finish(BciSelectionResult.Failed("Simulated BCI disconnected."));
            AvailabilityChanged?.Invoke();
        }

        private void Finish(BciSelectionResult result)
        {
            IsSelecting = false;
            _targets = NoTargets;
            SelectionFinished?.Invoke(result);
        }
    }
}
