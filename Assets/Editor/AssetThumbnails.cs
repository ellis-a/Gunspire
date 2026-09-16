#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Draws a spell, gun or boon asset in the Project window as its own icon rather than the same
    /// ScriptableObject box as everything else. With 94 spells in one folder, the icon is the only thing that
    /// tells them apart at a glance.
    ///
    /// The thumbnail is rendered from the icon texture rather than copied, because an imported PNG is not
    /// readable by default and reading its pixels directly would fail on every icon in the project.
    /// </summary>
    public abstract class IconThumbnailEditor : Editor
    {
        protected abstract Texture2D IconFor(Object asset);

        public override Texture2D RenderStaticPreview(string path, Object[] subAssets, int width, int height)
        {
            Texture2D icon = IconFor(target);
            if (icon == null) return null;

            RenderTexture buffer = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;

            try
            {
                // Cleared rather than blitted onto, so the transparent margin of a non-square icon stays transparent.
                RenderTexture.active = buffer;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(icon, buffer);

                var preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
                preview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                preview.Apply();
                return preview;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(buffer);
            }
        }
    }

    [CustomEditor(typeof(SpellAsset))]
    public class SpellAssetEditor : IconThumbnailEditor
    {
        protected override Texture2D IconFor(Object asset)
        {
            var spell = asset as SpellAsset;
            return spell != null && spell.Definition != null ? spell.Definition.Icon : null;
        }
    }

    [CustomEditor(typeof(WeaponAsset))]
    public class WeaponAssetEditor : IconThumbnailEditor
    {
        protected override Texture2D IconFor(Object asset)
        {
            var weapon = asset as WeaponAsset;
            return weapon != null && weapon.Definition != null ? weapon.Definition.Icon : null;
        }
    }

    [CustomEditor(typeof(BoonAsset))]
    public class BoonAssetEditor : IconThumbnailEditor
    {
        protected override Texture2D IconFor(Object asset)
        {
            var boon = asset as BoonAsset;
            return boon != null && boon.Boon != null ? boon.Boon.Icon : null;
        }
    }
}
#endif
