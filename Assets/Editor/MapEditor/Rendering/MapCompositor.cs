using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapCompositor
    {
        public static Texture2D Compose(LevelMapData data)
        {
            if (data == null || data.groundTexture == null) return null;
            int width = data.groundTexture.width;
            int height = data.groundTexture.height;
            Texture2D result = MapGenerator.CreateBlank("Composite", width, height);
            Color[] ground = data.groundTexture.GetPixels();
            Color[] paint = Read(data.paintTexture, width, height);
            Color[] groundMask = Read(data.groundEraseMask, width, height);
            Color[] paintMask = Read(data.paintEraseMask, width, height);
            var pixels = new Color[width * height];
            Color groundColor = data.groundColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                float groundAlpha = ground[i].a * (1f - (groundMask != null ? groundMask[i].a : 0f));
                Color baseColor = new Color(groundColor.r, groundColor.g, groundColor.b, groundAlpha);
                Color paintColor = paint != null ? paint[i] : Color.clear;
                paintColor.a *= 1f - (paintMask != null ? paintMask[i].a : 0f);
                pixels[i] = new Color(
                    baseColor.r * (1f - paintColor.a) + paintColor.r * paintColor.a,
                    baseColor.g * (1f - paintColor.a) + paintColor.g * paintColor.a,
                    baseColor.b * (1f - paintColor.a) + paintColor.b * paintColor.a,
                    Mathf.Max(baseColor.a, paintColor.a));
            }
            result.SetPixels(pixels);
            result.Apply(false, false);
            return result;
        }

        private static Color[] Read(Texture2D texture, int width, int height)
        {
            if (texture == null || texture.width != width || texture.height != height) return null;
            return texture.GetPixels();
        }
    }
}
