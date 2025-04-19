using UnityEngine;
using System.IO;

namespace QuadSpriteProcessor
{
    public static class QuadProcessorUtility
    {
        public static bool AreDimensionsDivisibleByFour(int width, int height)
        {
            return width % 4 == 0 && height % 4 == 0;
        }

        public static TextureInfo GetTextureInfo(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);

            var info = new TextureInfo
            {
                Width = texture.width,
                Height = texture.height
            };

            Object.DestroyImmediate(texture);
            return info;
        }

        // Helper function to safely get a pixel from the array
        public static Color GetPixelSafe(Color[] pixels, int x, int y, int width, int height)
        {
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x >= width) x = width - 1;
            if (y >= height) y = height - 1;

            return pixels[y * width + x];
        }

        public static int CalculateDivisibleByFour(int dimension)
        {
            return dimension % 4 != 0 ? (int)Mathf.Ceil(dimension / 4f) * 4 : dimension;
        }
    }
}