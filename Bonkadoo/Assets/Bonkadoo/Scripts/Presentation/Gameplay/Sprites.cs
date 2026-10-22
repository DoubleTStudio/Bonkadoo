using System.Collections.Generic;
using UnityEngine;

namespace Bonkadoo.Presentation.Gameplay
{
    /// <summary>Placeholder art generated at runtime (no third-party assets). Replaced by reviewed art per ART_BIBLE.</summary>
    public static class Sprites
    {
        private static Sprite _circle, _rounded;

        public static Sprite Circle => _circle != null ? _circle : (_circle = Make(64, (x, y, s) =>
        {
            var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
            return Mathf.Clamp01(s / 2f - d);
        }, 0));

        public static Sprite Rounded => _rounded != null ? _rounded : (_rounded = Make(64, (x, y, s) =>
        {
            const float r = 16f;
            var cx = Mathf.Clamp(x + 0.5f, r, s - r);
            var cy = Mathf.Clamp(y + 0.5f, r, s - r);
            return Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) + 0.5f);
        }, 20));

        private static Sprite Make(int size, System.Func<int, int, int, float> alpha, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    px[y * size + x] = new Color(1, 1, 1, alpha(x, y, size));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        public static readonly Dictionary<string, string> CharacterColors = new Dictionary<string, string>
        {
            ["ti"] = "#e63946", ["bop"] = "#a8dadc", ["vuong"] = "#457b9d", ["coc"] = "#2a9d8f", ["mai"] = "#f4a261",
            ["lan"] = "#6d597a", ["logic"] = "#adb5bd", ["janitor"] = "#8d99ae", ["clerk"] = "#b5838d",
            ["inspector"] = "#6c757d", ["mayor"] = "#9d4edd", ["curator"] = "#bc6c25", ["baker"] = "#ddb892",
            ["crowd"] = "#8ecae6", ["guard"] = "#264653", ["printer"] = "#ced4da", ["sign"] = "#ffb703",
        };

        /// <summary>Stable pastel per prop id so props stay recognizable between visits.</summary>
        public static Color PropColor(string id)
        {
            unchecked
            {
                var h = 17;
                foreach (var c in id) h = h * 31 + c;
                return Color.HSVToRGB(((h & 0x7fffffff) % 360) / 360f, 0.28f, 0.95f);
            }
        }
    }
}
