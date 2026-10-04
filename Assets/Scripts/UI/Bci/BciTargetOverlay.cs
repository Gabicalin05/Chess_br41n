using System.Collections.Generic;
using BciChess.Bci;
using BciChess.Core;
using BciChess.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>
    /// Marks BCI targets on the board with a numbered badge showing their stimulus slot. When candidates are
    /// grouped, every square of a group carries the group's number in a distinct badge colour.
    /// Kept separate from the square and piece renderers; flashing stimulation replaces this in a later phase.
    /// </summary>
    public sealed class BciTargetOverlay : MonoBehaviour
    {
        private static readonly Color GroupColor = new Color32(0xF2, 0x9A, 0x2E, 0xFF);

        private readonly Image[] _badges = new Image[64];
        private readonly Text[] _labels = new Text[64];
        private Color _targetColor;

        public void Build(BoardView board, BoardTheme theme)
        {
            _targetColor = theme.accent;
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
                _badges[i] = badge;
                _labels[i] = label;
            }
        }

        public void Render(IReadOnlyList<BciTarget> targets)
        {
            for (int i = 0; i < 64; i++)
                _badges[i].gameObject.SetActive(false);

            foreach (var target in targets)
            {
                if (!target.Stimulus.HasValue)
                    continue;
                string key = FakeBciKeyboardInput.KeyLabel(target.Stimulus.Value.Index);

                if (target.Payload is CandidateGroup group)
                {
                    foreach (var member in group.Members)
                        Show(member, key, GroupColor);
                }
                else
                {
                    Show(target, key, _targetColor);
                }
            }
        }

        private void Show(BciTarget target, string key, Color color)
        {
            if (!(target.Payload is ChessTargetPayload payload))
                return;
            if (payload.Kind != ChessTargetKind.Piece && payload.Kind != ChessTargetKind.Destination)
                return;

            int index = payload.Square.Index;
            _labels[index].text = key;
            _badges[index].color = color;
            _badges[index].gameObject.SetActive(true);
        }
    }
}
