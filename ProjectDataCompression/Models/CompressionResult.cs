// File: CompressionResult.cs 
namespace ProjectDataCompression.Models;

public class CompressionResult
{
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio => OriginalSize == 0 ? 0 : (double)CompressedSize / OriginalSize;
    public TimeSpan Duration { get; set; }
}