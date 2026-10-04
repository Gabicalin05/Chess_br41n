using System.Collections.Generic;
using System.Linq;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using NUnit.Framework;
using static BciChess.Tests.TestUtil;

namespace BciChess.Tests
{
    public class StimulusManagerTests
    {
        private static List<BciTarget> Targets(int count) =>
            Enumerable.Range(0, count).Select(i => new BciTarget("t" + i, "Target " + i)).ToList();

        [Test]
        public void EveryCandidateGetsExactlyOneUniqueStimulus()
        {
            var manager = new StimulusManager(new[] { 1, 2, 3, 4, 5 }, 5);
            Assert.IsTrue(manager.TryAssign(Targets(5), out var assigned));

            Assert.AreEqual(5, assigned.Count);
            Assert.IsTrue(assigned.All(t => t.Stimulus.HasValue));
            Assert.AreEqual(5, assigned.Select(t => t.Stimulus.Value.ClassId).Distinct().Count());
            Assert.AreEqual(5, assigned.Select(t => t.Stimulus.Value.Index).Distinct().Count());
            CollectionAssert.AreEqual(Targets(5).Select(t => t.Id), assigned.Select(t => t.Id));
        }

        [Test]
        public void FewerCandidatesThanSlots_UsesFirstSlotsInOrder()
        {
            var manager = new StimulusManager(new[] { 7, 3, 9 }, 0);
            Assert.IsTrue(manager.TryAssign(Targets(2), out var assigned));
            CollectionAssert.AreEqual(new[] { 7, 3 }, assigned.Select(t => t.Stimulus.Value.ClassId));
        }

        [Test]
        public void MoreCandidatesThanCapacity_Fails()
        {
            var manager = new StimulusManager(new[] { 1, 2, 3, 4, 5 }, 3);
            Assert.AreEqual(3, manager.Capacity);
            Assert.IsFalse(manager.TryAssign(Targets(4), out var assigned));
            Assert.AreEqual(0, assigned.Count);
        }

        [Test]
        public void DuplicateClassIds_AreIgnored()
        {
            var manager = new StimulusManager(new[] { 1, 1, 2, 2, 3 }, 0);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, manager.Slots.Select(s => s.ClassId));
        }

        [Test]
        public void AssignmentDoesNotModifyInputTargets()
        {
            var manager = new StimulusManager(new[] { 1, 2 }, 0);
            var input = Targets(2);
            manager.TryAssign(input, out _);
            Assert.IsTrue(input.All(t => !t.Stimulus.HasValue));
        }
    }

    public class FakeBciSelectorTests
    {
        private static IReadOnlyList<BciTarget> Assigned(params string[] ids)
        {
            var manager = new StimulusManager(new[] { 1, 2, 3, 4 }, 0);
            manager.TryAssign(ids.Select(id => new BciTarget(id, id)).ToList(), out var assigned);
            return assigned;
        }

        [Test]
        public void SelectingSlot_ReportsMatchingTarget_Once()
        {
            var selector = new FakeBciSelector();
            var results = new List<BciSelectionResult>();
            selector.SelectionFinished += results.Add;

            selector.StartSelection(Assigned("a", "b", "c"));
            Assert.IsTrue(selector.TrySelectSlot(1));
            Assert.IsFalse(selector.TrySelectSlot(1), "Selection already finished");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(BciSelectionStatus.Selected, results[0].Status);
            Assert.AreEqual("b", results[0].Target.Id);
            Assert.IsFalse(selector.IsSelecting);
        }

        [Test]
        public void UnknownSlot_IsInvalid()
        {
            var selector = new FakeBciSelector();
            BciSelectionResult? result = null;
            selector.SelectionFinished += r => result = r;
            selector.StartSelection(Assigned("a", "b"));
            selector.TrySelectSlot(3);
            Assert.AreEqual(BciSelectionStatus.Invalid, result.Value.Status);
        }

        [Test]
        public void StopSelection_RaisesNothing()
        {
            var selector = new FakeBciSelector();
            int finished = 0;
            selector.SelectionFinished += _ => finished++;
            selector.StartSelection(Assigned("a"));
            selector.StopSelection();
            Assert.IsFalse(selector.TrySelectSlot(0));
            Assert.AreEqual(0, finished);
        }

        [Test]
        public void TargetsWithoutStimulus_AreRejected()
        {
            var selector = new FakeBciSelector();
            Assert.Throws<System.ArgumentException>(() =>
                selector.StartSelection(new[] { new BciTarget("a", "a") }));
        }

        [Test]
        public void Disconnect_FailsRunningSelection()
        {
            var selector = new FakeBciSelector();
            BciSelectionResult? result = null;
            selector.SelectionFinished += r => result = r;
            selector.StartSelection(Assigned("a"));
            selector.SetAvailable(false);
            Assert.AreEqual(BciSelectionStatus.Failed, result.Value.Status);
            Assert.IsFalse(selector.IsAvailable);
        }
    }

    public class BciSelectionControllerTests
    {
        private static int[] ClassIds(int count) => Enumerable.Range(1, count).ToArray();

        private static (ChessGame game, SelectionController selection, FakeBciSelector fake, BciSelectionController bci)
            Create(string fen = ChessPosition.StartFen, int capacity = 12, float timeout = 0f)
        {
            var game = new ChessGame(fen);
            var selection = new SelectionController(game);
            var fake = new FakeBciSelector();
            var bci = new BciSelectionController(selection, fake, new StimulusManager(ClassIds(capacity), capacity), timeout)
            {
                Enabled = true
            };
            return (game, selection, fake, bci);
        }

        private static int SlotOf(BciSelectionController bci, string targetId) =>
            bci.Targets.Single(t => t.Id == targetId).Stimulus.Value.Index;

        [Test]
        public void PieceCandidates_AreExactlyThePiecesWithLegalMoves()
        {
            var (_, _, fake, bci) = Create();
            Assert.AreEqual(BciSessionStatus.AwaitingSelection, bci.Status);
            CollectionAssert.AreEquivalent(
                new[] { "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2", "b1", "g1" }.Select(s => "piece:" + s),
                bci.Targets.Select(t => t.Id));
            Assert.IsTrue(fake.IsSelecting);
        }

        [Test]
        public void DestinationCandidates_AreLegalDestinationsPlusCancel()
        {
            var (_, _, fake, bci) = Create();
            fake.TrySelectSlot(SlotOf(bci, "piece:g1"));
            CollectionAssert.AreEquivalent(new[] { "dest:f3", "dest:h3", "cancel" }, bci.Targets.Select(t => t.Id));
        }

        [Test]
        public void TwoSelections_PlayAMove_AndNextSessionStarts()
        {
            var (game, selection, fake, bci) = Create();
            fake.TrySelectSlot(SlotOf(bci, "piece:g1"));
            fake.TrySelectSlot(SlotOf(bci, "dest:f3"));

            Assert.AreEqual("g1f3", game.LastMove.Value.ToUci());
            Assert.AreEqual(InteractionState.SelectingPiece, selection.State);
            Assert.AreEqual(BciSessionStatus.AwaitingSelection, bci.Status);
            Assert.IsTrue(bci.Targets.All(t => t.Id.StartsWith("piece:")));
            Assert.IsTrue(fake.IsSelecting);
        }

        [Test]
        public void StimuliAreUniqueWithinEverySession()
        {
            var (_, _, fake, bci) = Create();
            Assert.AreEqual(bci.Targets.Count, bci.Targets.Select(t => t.Stimulus.Value.ClassId).Distinct().Count());
            fake.TrySelectSlot(SlotOf(bci, "piece:e2"));
            Assert.AreEqual(bci.Targets.Count, bci.Targets.Select(t => t.Stimulus.Value.ClassId).Distinct().Count());
        }

        [Test]
        public void CancelTarget_ReturnsToPieceSelection()
        {
            var (_, selection, fake, bci) = Create();
            fake.TrySelectSlot(SlotOf(bci, "piece:g1"));
            fake.TrySelectSlot(SlotOf(bci, "cancel"));
            Assert.AreEqual(InteractionState.SelectingPiece, selection.State);
            Assert.IsTrue(bci.Targets.Any(t => t.Id == "piece:g1"));
        }

        [Test]
        public void Promotion_IsChosenByBci()
        {
            var (game, _, fake, bci) = Create("8/P6k/8/8/8/8/8/K7 w - - 0 1");
            fake.TrySelectSlot(SlotOf(bci, "piece:a7"));
            fake.TrySelectSlot(SlotOf(bci, "dest:a8"));
            CollectionAssert.AreEquivalent(
                new[] { "promo:Queen", "promo:Rook", "promo:Bishop", "promo:Knight", "cancel" },
                bci.Targets.Select(t => t.Id));

            fake.TrySelectSlot(SlotOf(bci, "promo:Knight"));
            Assert.AreEqual(new Piece(PieceType.Knight, PieceColor.White), game.Position[Sq("a8")]);
        }

        [Test]
        public void TooManyCandidates_DoesNotStartSelection()
        {
            var (_, _, fake, bci) = Create(capacity: 4);
            Assert.AreEqual(BciSessionStatus.TooManyCandidates, bci.Status);
            Assert.AreEqual(10, bci.CandidateCount);
            Assert.AreEqual(0, bci.Targets.Count);
            Assert.IsFalse(fake.IsSelecting);
        }

        [Test]
        public void MouseInput_RestartsSessionWithNewCandidates()
        {
            var (_, selection, _, bci) = Create();
            selection.SelectSquare(Sq("e2"));
            CollectionAssert.AreEquivalent(new[] { "dest:e3", "dest:e4", "cancel" }, bci.Targets.Select(t => t.Id));
        }

        [Test]
        public void Disabled_StopsSelector()
        {
            var (_, _, fake, bci) = Create();
            bci.Enabled = false;
            Assert.AreEqual(BciSessionStatus.Disabled, bci.Status);
            Assert.IsFalse(fake.IsSelecting);
            Assert.AreEqual(0, bci.Targets.Count);
        }

        [Test]
        public void GameOver_IsIdle()
        {
            var (game, _, fake, bci) = Create();
            Play(game, "f2f3", "e7e5", "g2g4", "d8h4");
            Assert.AreEqual(BciSessionStatus.Idle, bci.Status);
            Assert.IsFalse(fake.IsSelecting);
        }

        [Test]
        public void Unavailable_ThenReconnected_Resumes()
        {
            var (_, _, fake, bci) = Create();
            fake.SetAvailable(false);
            Assert.AreEqual(BciSessionStatus.Unavailable, bci.Status);
            Assert.IsNotEmpty(bci.Message);

            fake.SetAvailable(true);
            Assert.AreEqual(BciSessionStatus.AwaitingSelection, bci.Status);
            Assert.IsTrue(fake.IsSelecting);
        }

        [Test]
        public void InvalidSelection_KeepsWaiting()
        {
            var (game, _, fake, bci) = Create();
            fake.TrySelectSlot(11); // capacity 12, only 10 candidates
            Assert.AreEqual(BciSessionStatus.AwaitingSelection, bci.Status);
            Assert.IsNotEmpty(bci.Message);
            Assert.AreEqual(0, game.MoveHistory.Count);
        }

        [Test]
        public void Timeout_RestartsSelection()
        {
            var (_, _, fake, bci) = Create(timeout: 5f);
            bci.Tick(3f);
            Assert.IsEmpty(bci.Message);
            bci.Tick(3f);
            Assert.IsNotEmpty(bci.Message);
            Assert.AreEqual(BciSessionStatus.AwaitingSelection, bci.Status);
            Assert.IsTrue(fake.IsSelecting);
        }
    }
}
