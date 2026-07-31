using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp.Services.DocumentReaders
{
    public class VirtualImagePreviewReader : IDocumentPreviewReader
    {
        public bool CanRead(string extension)
        {
            return false; // Specially checked via path prefix
        }

        public bool CanReadPath(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && filePath.StartsWith("[", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<DocumentPreviewResult> ExtractTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                string fileName = Path.GetFileName(filePath);
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                string formatType = (ext == ".pdf") ? "PDF" : (ext == ".hwpx") ? "HWPX" : "HWP";

                var sb = new StringBuilder();
                sb.AppendLine($"[포렌식 디스크 이미지 파일 정보]");
                sb.AppendLine($"• 파일명: {fileName}");
                sb.AppendLine($"• 추출 경로: {filePath}");
                sb.AppendLine($"• 문서 포맷: {formatType}");
                sb.AppendLine();

                string content = sb.ToString();
                int lineCount = content.Split('\n').Length;

                return new DocumentPreviewResult
                {
                    ContentText = content,
                    LineCount = lineCount,
                    CharCount = content.Length,
                    FormatType = formatType,
                    Success = true
                };
            });
        }
    }
}
