using System;

namespace PeriodicFolderSync.Models
{
    public class SyncStatistics
    {
        public int ChangedCount { get; set; }
        public int FoldersChangedCount { get; set; }
        public int FilesMoved { get; set; }
        public int FoldersMovedCount { get; set; }
        public int FilesInMovedFolders { get; set; }
        public int DeletedFiles { get; set; }
        public int DeletedFolders { get; set; }
    }
}