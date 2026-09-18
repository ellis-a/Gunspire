using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Shared IMGUI look. The whole interface is immediate-mode so the project needs no
    /// canvas prefabs; swap it for uGUI or UI Toolkit when the game earns real art.
    /// </summary>
    public static class UIStyles
    {
        public static readonly Color Ink = new Color(0.93f, 0.94f, 0.98f);
        public static readonly Color Muted = new Color(0.68f, 0.70f, 0.78f);
        public static readonly Color Panel = new Color(0.06f, 0.06f, 0.09f, 0.88f);
        public static readonly Color PanelSoft = new Color(0.10f, 0.10f, 0.14f, 0.75f);
        public static readonly Color Accent = new Color(0.62f, 0.48f, 1f);
        public static readonly Color HealthColor = new Color(0.93f, 0.30f, 0.38f);
        public static readonly Color ManaColor = new Color(0.42f, 0.60f, 1f);
        public static readonly Color AmmoColor = new Color(1f, 0.83f, 0.45f);
        public static readonly Color Warning = new Color(1f, 0.45f, 0.35f);

        private static Texture2D _white;
        private static GUIStyle _label, _labelSmall, _title, _heading, _center, _rightAligned, _wrap;
        private static bool _ready;

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        private static void EnsureStyles()
        {
            if (_ready && _label != null) return;
            _ready = true;

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            _label.normal.textColor = Ink;

            _labelSmall = new GUIStyle(_label) { fontSize = 12 };
            _labelSmall.normal.textColor = Muted;

            _heading = new GUIStyle(_label) { fontSize = 19, fontStyle = FontStyle.Bold };
            _title = new GUIStyle(_label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _rightAligned = new GUIStyle(_label) { alignment = TextAnchor.MiddleRight };
            _wrap = new GUIStyle(_labelSmall) { wordWrap = true, alignment = TextAnchor.UpperLeft };
        }

        public static GUIStyle Label { get { EnsureStyles(); return _label; } }
        public static GUIStyle Small { get { EnsureStyles(); return _labelSmall; } }
        public static GUIStyle Heading { get { EnsureStyles(); return _heading; } }
        public static GUIStyle Title { get { EnsureStyles(); return _title; } }
        public static GUIStyle Center { get { EnsureStyles(); return _center; } }
        public static GUIStyle Right { get { EnsureStyles(); return _rightAligned; } }
        public static GUIStyle Wrap { get { EnsureStyles(); return _wrap; } }

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = previous;
        }

        public static void Outline(Rect rect, Color color, float thickness = 1f)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void Bar(Rect rect, float fraction, Color fill, Color background)
        {
            Fill(rect, background);
            fraction = Mathf.Clamp01(fraction);
            if (fraction > 0f)
                Fill(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), fill);
            Outline(rect, new Color(0f, 0f, 0f, 0.6f));
        }

        public static void Text(Rect rect, string text, GUIStyle style, Color? color = null)
        {
            Color previous = style.normal.textColor;
            if (color.HasValue) style.normal.textColor = color.Value;
            DrawLabel(rect, text, style);
            style.normal.textColor = previous;
        }

        // ---------------------------------------------------------------- scaling
        //
        // Every layout is written for a 1080-pixel-high screen. Each OnGUI calls BeginScaled first, which
        // scales the GUI matrix so the same numbers fill a larger screen, and lays out against Width and Height
        // rather than Screen.width and Screen.height. Mouse positions come back through the matrix, so hit
        // tests need nothing. Text is the exception: a scaled matrix stretches glyphs rendered at the small
        // size, so DrawLabel draws it unscaled at the real pixel size with a font scaled to match.

        /// <summary>The screen height every layout is written for.</summary>
        public const float ReferenceHeight = 1080f;

        public const float MinUserScale = 0.5f;
        public const float MaxUserScale = 2f;
        public const float UserScaleStep = 0.1f;

        private const string UserScaleKey = "ui_scale";
        private static float _userScale = -1f;

        /// <summary>The player's own size adjustment on top of the automatic one, kept between sessions. 1 is none.</summary>
        public static float UserScale
        {
            get
            {
                if (_userScale < 0f) _userScale = Mathf.Clamp(PlayerPrefs.GetFloat(UserScaleKey, 1f), MinUserScale, MaxUserScale);
                return _userScale;
            }
            set
            {
                // Rounded to the step so repeated presses never drift off the tenths.
                _userScale = Mathf.Clamp(Mathf.Round(value / UserScaleStep) * UserScaleStep, MinUserScale, MaxUserScale);
                PlayerPrefs.SetFloat(UserScaleKey, _userScale);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Pixels per layout unit: the screen height over 1080, never below 1 so a small window keeps the size it
        /// always had, times the player's adjustment. A 4K screen gets 2.
        /// </summary>
        public static float Scale => Mathf.Max(1f, Screen.height / ReferenceHeight) * UserScale;

        /// <summary>The screen's width and height in layout units. Use these in OnGUI, never Screen.width and height.</summary>
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;

        /// <summary>Call first in every OnGUI.</summary>
        public static void BeginScaled()
        {
            float scale = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        }

        /// <summary>A point from Camera.WorldToScreenPoint, in layout units with y down.</summary>
        public static Vector2 FromScreen(Vector3 screen)
        {
            float scale = Scale;
            return new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
        }

        private static readonly System.Collections.Generic.Dictionary<GUIStyle, GUIStyle> Scaled =
            new System.Collections.Generic.Dictionary<GUIStyle, GUIStyle>();

        /// <summary>
        /// GUI.Label, with the text rendered at full resolution. Use it in place of GUI.Label for anything that
        /// draws text; the rect is in layout units as usual.
        /// </summary>
        public static void DrawLabel(Rect rect, string text, GUIStyle style)
        {
            Matrix4x4 matrix = GUI.matrix;
            float scale = matrix.m00;
            if (Mathf.Approximately(scale, 1f))
            {
                GUI.Label(rect, text, style);
                return;
            }

            GUIStyle sized = SizedCopy(style, scale);
            GUI.matrix = Matrix4x4.identity;
            GUI.Label(new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale), text, sized);
            GUI.matrix = matrix;
        }

        private static GUIStyle SizedCopy(GUIStyle style, float scale)
        {
            int fontSize = Mathf.Max(1, Mathf.RoundToInt((style.fontSize > 0 ? style.fontSize : 13) * scale));

            if (!Scaled.TryGetValue(style, out GUIStyle sized) || sized.fontSize != fontSize)
            {
                sized = new GUIStyle(style) { fontSize = fontSize };
                sized.padding = ScaleOffset(style.padding, scale);
                sized.contentOffset = style.contentOffset * scale;
                Scaled[style] = sized;
            }

            // The callers recolour the base style per draw, so the copy follows it every time.
            sized.normal.textColor = style.normal.textColor;
            return sized;
        }

        private static RectOffset ScaleOffset(RectOffset offset, float scale) => new RectOffset(
            Mathf.RoundToInt(offset.left * scale), Mathf.RoundToInt(offset.right * scale),
            Mathf.RoundToInt(offset.top * scale), Mathf.RoundToInt(offset.bottom * scale));

        /// <summary>
        /// A settings row: a label, then "-  value  +". Returns -1 or +1 on the frame a button is clicked, otherwise 0.
        /// Each end greys out at its limit.
        /// </summary>
        public static int Stepper(Rect rect, string label, string value, bool canLower, bool canRaise)
        {
            const float valueWidth = 64f;
            float buttonWidth = rect.height;
            float x = rect.xMax - buttonWidth * 2f - valueWidth;

            Text(new Rect(rect.x, rect.y, x - rect.x - 8f, rect.height), label, Label, Muted);

            int step = 0;
            if (Button(new Rect(x, rect.y, buttonWidth, rect.height), "-", Muted, canLower)) step = -1;
            Text(new Rect(x + buttonWidth, rect.y, valueWidth, rect.height), value, Center, Ink);
            if (Button(new Rect(x + buttonWidth + valueWidth, rect.y, buttonWidth, rect.height), "+", Muted, canRaise)) step = 1;
            return step;
        }

        /// <summary>Panel with a coloured left edge, used for every card in the game.</summary>
        public static void Card(Rect rect, Color accent, bool highlighted)
        {
            Fill(rect, highlighted ? new Color(0.16f, 0.16f, 0.22f, 0.95f) : Panel);
            Fill(new Rect(rect.x, rect.y, 4f, rect.height), accent);
            Outline(rect, highlighted ? accent : new Color(1f, 1f, 1f, 0.12f), highlighted ? 2f : 1f);
        }

        /// <summary>
        /// Draws a content icon, or a placeholder if none has been assigned yet.
        ///
        /// The placeholder is deliberately a tinted plate with initials rather than nothing:
        /// an empty square reads as a broken image, whereas this reads as a slot waiting for
        /// art, and it keeps every screen laid out the same whether the icons exist or not.
        ///
        /// Icons are Texture2D rather than Sprite on purpose. This is a 3D project, so an
        /// imported PNG is a texture unless its import type is changed by hand; a Sprite field
        /// would refuse every icon until that was done to each file.
        /// </summary>
        public static void Icon(Rect rect, Texture2D icon, Color tint, string fallbackLabel = null)
        {
            if (icon != null)
            {
                Color previous = GUI.color;
                GUI.color = Color.white;

                // ScaleToFit so a non-square icon is letterboxed rather than stretched.
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                GUI.color = previous;
                return;
            }

            Fill(rect, new Color(tint.r, tint.g, tint.b, 0.16f));
            Outline(rect, new Color(tint.r, tint.g, tint.b, 0.55f));

            string initials = Initials(fallbackLabel);
            if (!string.IsNullOrEmpty(initials)) Text(rect, initials, Center, tint);
        }

        /// <summary>Up to two letters from a name, for the placeholder plate.</summary>
        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string text = "";
            bool atWordStart = true;

            for (int i = 0; i < name.Length && text.Length < 2; i++)
            {
                char c = name[i];
                if (c == ' ' || c == '-' || c == '_') { atWordStart = true; continue; }

                if (atWordStart) text += char.ToUpperInvariant(c);
                atWordStart = false;
            }

            return text;
        }

        public static bool Button(Rect rect, string label, Color accent, bool enabled = true)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, enabled
                ? (hover ? new Color(accent.r, accent.g, accent.b, 0.30f) : PanelSoft)
                : new Color(0.1f, 0.1f, 0.1f, 0.6f));
            Outline(rect, enabled ? accent : new Color(1f, 1f, 1f, 0.1f));
            Text(rect, label, Center, enabled ? Ink : Muted);

            return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }
}
