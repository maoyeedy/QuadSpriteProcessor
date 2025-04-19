namespace QuadSpriteProcessor
{
    public struct TextureInfo
    {
        public int Width;
        public int Height;
    }

    // For Editor Window
    public class TextureAsset
    {
        public string Path;
        public int CurrentWidth;
        public int CurrentHeight;
        public int NewWidth;
        public int NewHeight;
        public bool Selected;
    }
}