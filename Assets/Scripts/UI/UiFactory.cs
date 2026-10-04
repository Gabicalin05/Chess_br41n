using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BciChess.UI
{
    /// <summary>Small helpers for building uGUI hierarchies from code, plus generated sprites.</summary>
    public static class UiFactory
    {
        private static Font _uiFont;
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _frame;

        public static Font UiFont
        {
            get
            {
                if (_uiFont == null)
                    _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _uiFont;
            }
        }

        public static Font CreateGlyphFont(BoardTheme theme)
        {
            if (theme.glyphStyle == PieceGlyphStyle.Letters || theme.glyphFontNames == null || theme.glyphFontNames.Length == 0)
                return UiFont;
            return Font.CreateDynamicFontFromOSFont(theme.glyphFontNames, 96);
        }

        public static Sprite Circle => _circle != null ? _circle : (_circle = CreateRadialSprite(128, 0f));
        public static Sprite Ring => _ring != null ? _ring : (_ring = CreateRadialSprite(128, 0.8f));
        public static Sprite Frame => _frame != null ? _frame : (_frame = CreateFrameSprite(32, 6));

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre-anchored rect at a fixed position and size.</summary>
        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null,
            bool raycastTarget = false)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment, Font font = null)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font != null ? font : UiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, BoardTheme theme,
            UnityAction onClick, int fontSize = 26)
        {
            var image = CreateImage(name, parent, Color.white, null, raycastTarget: true);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = theme.button;
            colors.highlightedColor = Color.Lerp(theme.button, Color.white, 0.18f);
            colors.selectedColor = theme.button;
            colors.pressedColor = Color.Lerp(theme.button, Color.black, 0.25f);
            colors.disabledColor = new Color(theme.button.r, theme.button.g, theme.button.b, 0.4f);
            button.colors = colors;
            DisableKeyboardSubmit(button);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var text = CreateText("Label", image.transform, label, fontSize, theme.buttonText, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 4f);
            return button;
        }

        /// <summary>
        /// Keyboard input is handled explicitly by KeyboardBoardInput, so a clicked button must not stay
        /// selected, or Space/Enter would click it again.
        /// </summary>
        public static void DisableKeyboardSubmit(Button button)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            });
        }

        public static LayoutElement AddLayout(Component component, float preferredHeight, float flexibleHeight = 0f)
        {
            var element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = Mathf.Min(preferredHeight, 20f);
            element.flexibleHeight = flexibleHeight;
            return element;
        }

        /// <summary>Anti-aliased white disc, or ring when <paramref name="innerRadius"/> &gt; 0 (fraction of radius).</summary>
        private static Sprite CreateRadialSprite(int size, float innerRadius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = innerRadius > 0f ? "GeneratedRing" : "GeneratedCircle"
            };
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - d);
                    if (innerRadius > 0f)
                        alpha *= Mathf.Clamp01(d - innerRadius * radius);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>White square outline usable as a 9-sliced frame.</summary>
        private static Sprite CreateFrameSprite(int size, int border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "GeneratedFrame"
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = new Color32(255, 255, 255, edge ? (byte)255 : (byte)0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
