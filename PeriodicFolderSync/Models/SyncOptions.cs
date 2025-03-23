namespace PeriodicFolderSync.Models;

public class SyncOptions
{
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public TimeSpan? Interval { get; set; }
    public bool Overwrite { get; set; }
    public bool Help { get; set; }
}