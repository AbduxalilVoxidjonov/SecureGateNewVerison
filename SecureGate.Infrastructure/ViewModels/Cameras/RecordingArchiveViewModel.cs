using SecureGate.Domain;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    public class RecordingArchiveViewModel
    {
        public Camera Camera { get; set; } = null!;
        public List<RecordingArchiveEntry> Entries { get; set; } = new();
    }

    public class RecordingArchiveEntry
    {
        public DateTime Date { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public long SizeBytes { get; set; }

        public string SizeDisplay
        {
            get
            {
                if (!Exists || SizeBytes == 0) return "â€”";
                double size = SizeBytes;
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                int u = 0;
                while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
                return $"{size:F1} {units[u]}";
            }
        }
    }
}
