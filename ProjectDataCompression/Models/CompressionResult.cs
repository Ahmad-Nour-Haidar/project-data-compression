namespace ProjectDataCompression.Models;

public class CompressionResult
{
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio => OriginalSize == 0 ? 0 : (1.0 - (double)CompressedSize / OriginalSize) * 100;
    public TimeSpan Duration { get; set; }
}