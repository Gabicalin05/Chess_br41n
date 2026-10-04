using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>Tells the visuals which stimulus slot is currently flashing.</summary>
    public interface IStimulusSource
    {
        /// <summary>True while stimuli are being presented.</summary>
        bool IsRunning { get; }

        /// <summary>The slot index that is lit right now, or null between flashes.</summary>
        int? LitSlot { get; }
    }

    /// <summary>
    /// ERP-style oddball flashing: within each round every active slot flashes exactly once, in random order,
    /// for <c>onTimeMs</c>, followed by <c>offTimeMs</c> of darkness. The same slot never flashes twice in a row,
    /// also not across round boundaries. Time is advanced explicitly with <see cref="Tick"/>.
    /// </summary>
    public sealed class FlashSequencer : IStimulusSource
    {
        private readonly Random _random;
        private readonly List<int> _slots = new List<int>();
        private readonly List<int> _round = new List<int>();
        private int _position;
        private bool _lit;
        private float _timeInPhase;
        private int _lastFlashed = -1;

        public FlashSequencer(float onTimeMs, float offTimeMs, int seed = 0)
        {
            if (onTimeMs <= 0f)
                throw new ArgumentOutOfRangeException(nameof(onTimeMs), "Flash on-time must be positive.");
            if (offTimeMs < 0f)
                throw new ArgumentOutOfRangeException(nameof(offTimeMs), "Flash off-time cannot be negative.");
            OnTimeSeconds = onTimeMs / 1000f;
            OffTimeSeconds = offTimeMs / 1000f;
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        /// <summary>Raised when a slot starts flashing (useful for logging or event markers).</summary>
        public event Action<int> FlashStarted;

        public float OnTimeSeconds { get; }
        public float OffTimeSeconds { get; }
        public bool IsRunning { get; private set; }
        public int? LitSlot => IsRunning && _lit ? _round[_position] : (int?)null;

        /// <summary>Completed rounds since <see cref="Start"/> (each slot flashed once per round).</summary>
        public int RoundsCompleted { get; private set; }

        /// <summary>Starts flashing the given slots. The first flash begins immediately.</summary>
        public void Start(IReadOnlyList<int> slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));

            _slots.Clear();
            foreach (int slot in slots)
            {
                if (!_slots.Contains(slot))
                    _slots.Add(slot);
            }

            RoundsCompleted = 0;
            _lastFlashed = -1;
            IsRunning = _slots.Count > 0;
            if (!IsRunning)
                return;

            NewRound();
            BeginFlash();
        }

        public void Stop()
        {
            IsRunning = false;
            _lit = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0f)
                return;

            _timeInPhase += deltaSeconds;
            while (IsRunning)
            {
                float phaseLength = _lit ? OnTimeSeconds : OffTimeSeconds;
                if (_timeInPhase < phaseLength)
                    break;
                _timeInPhase -= phaseLength;

                if (_lit)
                {
                    _lit = false;
                }
                else
                {
                    _position++;
                    if (_position >= _round.Count)
                    {
                        RoundsCompleted++;
                        NewRound();
                    }
                    BeginFlash();
                }
            }
        }

        private void BeginFlash()
        {
            _lit = true;
            _lastFlashed = _round[_position];
            FlashStarted?.Invoke(_lastFlashed);
        }

        private void NewRound()
        {
            _round.Clear();
            _round.AddRange(_slots);
            // Fisher-Yates shuffle.
            for (int i = _round.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_round[i], _round[j]) = (_round[j], _round[i]);
            }
            // Avoid flashing the same slot twice in a row across the round boundary.
            if (_round.Count > 1 && _round[0] == _lastFlashed)
                (_round[0], _round[1]) = (_round[1], _round[0]);
            _position = 0;
        }
    }
}
