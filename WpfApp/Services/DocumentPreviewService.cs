using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WpfApp.Services.DocumentReaders;

namespace WpfApp.Services
{
    public class DocumentPreviewResult
    {
        public string ContentText { get; set; } = string.Empty;
        public int LineCount { get; set; }
        public int CharCount { get; set; }
        public string FormatType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public static class DocumentPreviewService
    {
        private static readonly List<IDocumentPreviewReader> Readers = new()
        {
            new HwpDocumentPreviewReader(),
            new HwpxDocumentPreviewReader(),
            new PdfDocumentPreviewReader()
        };

        private static readonly VirtualImagePreviewReader VirtualImageReader = new();

        public static async Task<DocumentPreviewResult> ExtractTextAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new DocumentPreviewResult
                {
                    Success = false,
                    ErrorMessage = "파일 경로가 올바르지 않습니다."
                };
            }

            if (VirtualImageReader.CanReadPath(filePath))
            {
                return await VirtualImageReader.ExtractTextAsync(filePath);
            }

            if (!File.Exists(filePath))
            {
                return new DocumentPreviewResult
                {
                    Success = false,
                    ErrorMessage = "파일을 찾을 수 없습니다."
                };
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            var reader = Readers.FirstOrDefault(r => r.CanRead(ext));

            if (reader != null)
            {
                return await reader.ExtractTextAsync(filePath);
            }

            return new DocumentPreviewResult
            {
                Success = false,
                ErrorMessage = "지원하지 않는 파일 형식입니다."
            };
        }
    }
}
