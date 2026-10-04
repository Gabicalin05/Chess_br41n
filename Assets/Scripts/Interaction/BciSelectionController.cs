using System;
using System.Collections.Generic;
using BciChess.Bci;
using BciChess.Core;

namespace BciChess.Interaction
{
    public enum ChessTargetKind
    {
        Piece,
        Destination,
        Promotion,
        Cancel
    }

    /// <summary>Chess meaning of a <see cref="BciTarget"/>; stored as its opaque payload.</summary>
    public sealed class ChessTargetPayload
    {
        public ChessTargetPayload(ChessTargetKind kind, Square square = default, PieceType promotion = PieceType.None)
        {
            Kind = kind;
            Square = square;
            Promotion = promotion;
        }

        public ChessTargetKind Kind { get; }

        /// <summary>Board square for Piece and Destination targets.</summary>
        public Square Square { get; }

        /// <summary>Piece type for Promotion targets.</summary>
        public PieceType Promotion { get; }
    }

    public enum BciSessionStatus
    {
        /// <summary>BCI input is switched off.</summary>
        Disabled,

        /// <summary>Nothing to choose right now (e.g. game over).</summary>
        Idle,

        /// <summary>Targets are presented and the selector is running.</summary>
        AwaitingSelection,

        /// <summary>More candidates than stimulus slots; BCI cannot present them all at once.</summary>
        TooManyCandidates,

        /// <summary>The selector (device) is not available.</summary>
        Unavailable
    }

    /// <summary>
    /// Presents the current choices of <see cref="SelectionController"/> as BCI targets and feeds the
    /// BCI's result back into it. Owns no chess rules and no device code.
    /// </summary>
    public sealed class BciSelectionController : IDisposable
    {
        private static readonly IReadOnlyList<BciTarget> NoTargets = Array.Empty<BciTarget>();

        private readonly SelectionController _selection;
        private readonly IBciSelector _selector;
        private readonly StimulusManager _stimuli;
        private readonly float _timeoutSeconds;
        private readonly bool _offerCancelTarget;

        private IReadOnlyList<BciTarget> _targets = NoTargets;
        private bool _enabled;
        private float _elapsedSeconds;

        /// <param name="selectionTimeoutSeconds">Restart a selection after this long without a result (0 = never).</param>
        /// <param name="offerCancelTarget">Add a "Cancel" target when choosing a destination or promotion piece.</param>
        public BciSelectionController(SelectionController selection, IBciSelector selector, StimulusManager stimuli,
            float selectionTimeoutSeconds = 0f, bool offerCancelTarget = true)
        {
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _stimuli = stimuli ?? throw new ArgumentNullException(nameof(stimuli));
            _timeoutSeconds = selectionTimeoutSeconds;
            _offerCancelTarget = offerCancelTarget;

            _selection.StateChanged += Restart;
            _selector.SelectionFinished += OnSelectionFinished;
            _selector.AvailabilityChanged += Restart;
            Restart();
        }

        /// <summary>Raised whenever status, targets or message change.</summary>
        public event Action Changed;

        public IBciSelector Selector => _selector;
        public BciSessionStatus Status { get; private set; }

        /// <summary>Targets currently presented, each with its assigned stimulus.</summary>
        public IReadOnlyList<BciTarget> Targets => _targets;

        /// <summary>Number of candidates in the current step (also when they did not fit).</summary>
        public int CandidateCount { get; private set; }

        public int Capacity => _stimuli.Capacity;

        /// <summary>Last notice for the player (timeout, invalid selection, device error); empty if none.</summary>
        public string Message { get; private set; } = string.Empty;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                Message = string.Empty;
                Restart();
            }
        }

        /// <summary>Advances the selection timeout. Call once per frame.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Status != BciSessionStatus.AwaitingSelection || _timeoutSeconds <= 0f)
                return;

            _elapsedSeconds += deltaSeconds;
            if (_elapsedSeconds >= _timeoutSeconds)
            {
                Message = "No selection detected - starting again";
                Restart();
            }
        }

        public void Dispose()
        {
            _selection.StateChanged -= Restart;
            _selector.SelectionFinished -= OnSelectionFinished;
            _selector.AvailabilityChanged -= Restart;
            _selector.StopSelection();
        }

        /// <summary>The BCI choices for the selection controller's current state, in a stable order.</summary>
        public static List<BciTarget> BuildCandidates(SelectionController selection, bool offerCancelTarget)
        {
            var targets = new List<BciTarget>();
            var position = selection.Game.Position;

            switch (selection.State)
            {
                case InteractionState.SelectingPiece:
                    foreach (var square in selection.SelectablePieces)
                    {
                        targets.Add(new BciTarget("piece:" + square, $"{position[square].Type} {square}",
                            new ChessTargetPayload(ChessTargetKind.Piece, square)));
                    }
                    break;

                case InteractionState.SelectingDestination:
                    foreach (var square in selection.SelectableDestinations)
                    {
                        string label = position[square].IsNone ? square.ToString() : $"{square} (capture)";
                        targets.Add(new BciTarget("dest:" + square, label,
                            new ChessTargetPayload(ChessTargetKind.Destination, square)));
                    }
                    break;

                case InteractionState.SelectingPromotion:
                    foreach (var type in SelectionController.PromotionPieces)
                    {
                        targets.Add(new BciTarget("promo:" + type, type.ToString(),
                            new ChessTargetPayload(ChessTargetKind.Promotion, promotion: type)));
                    }
                    break;

                default:
                    return targets;
            }

            if (offerCancelTarget && selection.State != InteractionState.SelectingPiece)
                targets.Add(new BciTarget("cancel", "Cancel", new ChessTargetPayload(ChessTargetKind.Cancel)));

            return targets;
        }

        private void Restart()
        {
            _selector.StopSelection();
            _targets = NoTargets;
            _elapsedSeconds = 0f;
            CandidateCount = 0;

            if (!_enabled)
            {
                SetStatus(BciSessionStatus.Disabled);
                return;
            }

            var candidates = BuildCandidates(_selection, _offerCancelTarget);
            CandidateCount = candidates.Count;

            if (candidates.Count == 0)
            {
                SetStatus(BciSessionStatus.Idle);
            }
            else if (!_selector.IsAvailable)
            {
                SetStatus(BciSessionStatus.Unavailable);
            }
            else if (!_stimuli.TryAssign(candidates, out var assigned))
            {
                SetStatus(BciSessionStatus.TooManyCandidates);
            }
            else
            {
                _targets = assigned;
                // Set status first: the selector may (in theory) answer synchronously.
                SetStatus(BciSessionStatus.AwaitingSelection);
                _selector.StartSelection(assigned);
            }
        }

        private void OnSelectionFinished(BciSelectionResult result)
        {
            _targets = NoTargets;

            switch (result.Status)
            {
                case BciSelectionStatus.Selected:
                    Message = string.Empty;
                    // A successful apply changes the selection state, which restarts the session.
                    if (!Apply(result.Target.Payload as ChessTargetPayload))
                    {
                        Message = $"Could not use selection '{result.Target.Label}'";
                        Restart();
                    }
                    break;

                case BciSelectionStatus.Invalid:
                    Message = "Selection not recognised - try again";
                    Restart();
                    break;

                default:
                    Message = string.IsNullOrEmpty(result.Message) ? "BCI selection failed" : result.Message;
                    Restart();
                    break;
            }
        }

        private bool Apply(ChessTargetPayload payload)
        {
            if (payload == null)
                return false;

            switch (payload.Kind)
            {
                case ChessTargetKind.Piece:
                case ChessTargetKind.Destination:
                    return _selection.SelectSquare(payload.Square);
                case ChessTargetKind.Promotion:
                    return _selection.SelectPromotion(payload.Promotion);
                case ChessTargetKind.Cancel:
                    if (_selection.State != InteractionState.SelectingDestination &&
                        _selection.State != InteractionState.SelectingPromotion)
                        return false;
                    _selection.Cancel();
                    return true;
                default:
                    return false;
            }
        }

        private void SetStatus(BciSessionStatus status)
        {
            Status = status;
            Changed?.Invoke();
        }
    }
}
