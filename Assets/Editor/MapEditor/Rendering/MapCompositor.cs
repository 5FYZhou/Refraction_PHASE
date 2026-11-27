using UnityEngine;

namespace Refraction.MapEditor
{
    public static class MapCompositor
    {
        public static Texture2D Compose(LevelMapData data)
        {
            return CreateComposite(data);
        }

        public static Texture2D CreateComposite(LevelMapData data)
        {
            if (data == null || data.groundTexture == null) return null;
            int width = data.groundTexture.width;
            int height = data.groundTexture.height;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Composite",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            ComposeInto(data, result, default);
            return result;
        }

        public static void ComposeInto(LevelMapData data, Texture2D composite, RectInt dirty)
        {
            if (data == null || data.groundTexture == null || composite == null) return;
            int width = data.groundTexture.width;
            int height = data.groundTexture.height;
            if (composite.width != width || composite.height != height) return;

            RectInt rect = ResolveRect(dirty, width, height);
            Color[] ground = ReadRegion(data.groundTexture, rect);
            if (ground == null) return;
            Color[] paint = ReadRegion(data.paintTexture, rect);
            Color[] groundMask = ReadRegion(data.groundEraseMask, rect);
            Color[] paintMask = ReadRegion(data.paintEraseMask, rect);
            Color[] pixels = composite.GetPixels(rect.x, rect.y, rect.width, rect.height);
            Color groundColor = data.groundColor;
            for (int i = 0; i < pixels.Length; i++)
            {
                float groundAlpha = ground[i].a * (1f - (groundMask != null ? groundMask[i].a : 0f));
                Color paintColor = paint != null ? paint[i] : Color.clear;
                paintColor.a *= 1f - (paintMask != null ? paintMask[i].a : 0f);
                pixels[i] = new Color(
                    groundColor.r * (1f - paintColor.a) + paintColor.r * paintColor.a,
                    groundColor.g * (1f - paintColor.a) + paintColor.g * paintColor.a,
                    groundColor.b * (1f - paintColor.a) + paintColor.b * paintColor.a,
                    Mathf.Max(groundAlpha, paintColor.a));
            }
            composite.SetPixels(rect.x, rect.y, rect.width, rect.height, pixels);
            composite.Apply(false, false);
        }

        public static void Release(Texture2D composite)
        {
            if (composite != null) Object.DestroyImmediate(composite);
        }

        private static RectInt ResolveRect(RectInt dirty, int width, int height)
        {
            if (dirty.width <= 0 || dirty.height <= 0) return new RectInt(0, 0, width, height);
            int x0 = Mathf.Clamp(dirty.xMin, 0, width);
            int y0 = Mathf.Clamp(dirty.yMin, 0, height);
            int x1 = Mathf.Clamp(dirty.xMax, 0, width);
            int y1 = Mathf.Clamp(dirty.yMax, 0, height);
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        private static Color[] ReadRegion(Texture2D texture, RectInt rect)
        {
            if (texture == null || rect.x < 0 || rect.y < 0
                || rect.xMax > texture.width || rect.yMax > texture.height) return null;
            return texture.GetPixels(rect.x, rect.y, rect.width, rect.height);
        }
    }
}
