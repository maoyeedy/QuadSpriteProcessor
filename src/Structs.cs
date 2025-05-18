namespace QuadSpriteProcessor
{
    public struct TextureInfo
    {
        public int Width;
        public int Height;
    }

    public struct ImportedTextureInfo
    {
        public int SourceWidth;
        public int SourceHeight;
        public int ImportedWidth;
        public int ImportedHeight;
        public int NewImportedWidth;
        public int NewImportedHeight;
        public int NewSourceWidth;
        public int NewSourceHeight;
        public bool NeedsProcessing;
    }

    // For Editor Window only
    public class TextureAsset
    {
        public string Path;
        public int CurrentWidth;
        public int CurrentHeight;
        public int NewWidth;
        public int NewHeight;

        // Original file res
        public int SourceWidth;
        public int SourceHeight;

        // New res needed
        // after applying importer max size
        public int NewSourceWidth;
        public int NewSourceHeight;

        public bool Selected;
    }
}
