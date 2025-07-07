namespace ProjectDataCompression.Models;

public class ArchiveMetadata
{
    public string Algorithm { get; set; }
    public string PasswordHash { get; set; }
    public List<ArchiveEntry> Entries { get; set; }
    public DateTime CreatedAt { get; set; }
        
    public ArchiveMetadata()
    {
        Entries = new List<ArchiveEntry>();
        CreatedAt = DateTime.Now;
    }
}