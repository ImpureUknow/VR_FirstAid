using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace FirstAid
{
    internal static class FirstAidPracticeUi
    {
        private static TMP_FontAsset font;
        private static readonly List<OTL_FeatureTag> Typography = new()
        {
            OTL_FeatureTag.kern, OTL_FeatureTag.mark, OTL_FeatureTag.mkmk
        };

        private static TMP_FontAsset GetFont()
        {
            if (font != null)
                return font;

            var source = Resources.Load<Font>("Typography/NotoSansThai-Regular");
            if (source == null)
                return TMP_Settings.defaultFontAsset;

            font = TMP_FontAsset.CreateFontAsset(source, 52, 6, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            string characters = "·–";
            for (char value = ' '; value <= '~'; value++)
                characters += value;
            for (char value = 'ก'; value <= '๛'; value++)
                characters += value;
            font.TryAddCharacters(characters, true);
            return font;
        }

        public static Canvas Canvas(string name, Transform parent, float width, float height, float scale)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one * scale;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, float x, float y,
            float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static Image Fill(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, float x, float y,
            float width, float height, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = GetFont();
            label.fontFeatures = new List<OTL_FeatureTag>(Typography);
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.67f;
            label.fontSizeMax = size;
            label.color = color;
            label.alignment = align;
            label.richText = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }
    }
}
