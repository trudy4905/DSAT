using System.Collections.Generic;
using System.Linq;

namespace WpfApp.Models
{
    public class ImageInspectionResult
    {
        public bool IsValidSupportedImage { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ImageTypeTag { get; set; } = string.Empty;
        public List<PartitionInfo> Partitions { get; set; } = new List<PartitionInfo>();

        public int TotalPartitionCount => Partitions.Count;
        public int SupportedPartitionCount => Partitions.Count(p => p.IsSupported);

        public string FilesystemSummary
        {
            get
            {
                var supportedList = Partitions.Where(p => p.IsSupported).ToList();
                if (supportedList.Count == 0) return "없음";

                string details = string.Join(", ", supportedList.Select(p => $"P{p.PartitionIndex}: {p.Filesystem}"));
                return $"파티션 {supportedList.Count}개 ({details})";
            }
        }
    }
}
