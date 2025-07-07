namespace ProjectDataCompression.Models;

public class ArchiveEntry
{
    public string FileName { get; set; }
    public string RelativePath { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public long DataOffset { get; set; }
    public long CompressedDataLength { get; set; }
    public Dictionary<byte, int> Frequencies { get; set; }
        
    public ArchiveEntry()
    {
        Frequencies = new Dictionary<byte, int>();
    }
}