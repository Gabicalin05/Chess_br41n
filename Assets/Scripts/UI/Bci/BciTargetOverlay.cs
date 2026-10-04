using System.Collections.Generic;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Marks BCI targets on the board with a numbered badge showing their stimulus slot.
    /// Kept separate from the square and piece renderers; flashing stimulation replaces this in a later phase.
    /// </summary>
    public sealed class BciTargetOverlay : MonoBehaviour
    {
        private readonly GameObject[] _badges = new GameObject[64];
        private readonly Text[] _labels = new Text[64];

        public void Build(BoardView board, BoardTheme theme)
        {
            for (int i = 0; i < 64; i++)
            {
                var square = board.GetSquareView(new Square(i));
                var badge = UiFactory.CreateImage("BciBadge", square.transform, theme.accent, UiFactory.Circle);
                UiFactory.SetAnchors(badge.rectTransform, new Vector2(0.6f, 0.6f), new Vector2(0.97f, 0.97f));
                badge.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);

                var label = UiFactory.CreateText("Slot", badge.transform, "", 22, Color.white, TextAnchor.MiddleCenter);
                label.fontStyle = FontStyle.Bold;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.Stretch(label.rectTransform);

                badge.gameObject.SetActive(false);
                _badges[i] = badge.gameObject;
                _labels[i] = label;
            }
        }

        public void Render(IReadOnlyList<BciTarget> targets)
        {
            for (int i = 0; i < 64; i++)
                _badges[i].SetActive(false);

            foreach (var target in targets)
            {
                if (!(target.Payload is ChessTargetPayload payload) || !target.Stimulus.HasValue)
                    continue;
                if (payload.Kind != ChessTargetKind.Piece && payload.Kind != ChessTargetKind.Destination)
                    continue;

                int index = payload.Square.Index;
                _labels[index].text = FakeBciKeyboardInput.KeyLabel(target.Stimulus.Value.Index);
                _badges[index].SetActive(true);
            }
        }
    }
}
