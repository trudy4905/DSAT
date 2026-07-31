using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WpfApp.Services.DocumentReaders
{
    public class HwpxDocumentPreviewReader : IDocumentPreviewReader
    {
        public bool CanRead(string extension)
        {
            return string.Equals(extension, ".hwpx", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<DocumentPreviewResult> ExtractTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sb = new StringBuilder();

                    using (ZipArchive archive = ZipFile.OpenRead(filePath))
                    {
                        // 1. Try PrvText.txt in Preview/ package folder
                        var prvEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("PrvText.txt", StringComparison.OrdinalIgnoreCase) ||
                                                                           e.Name.Equals("PrvText.txt", StringComparison.OrdinalIgnoreCase));
                        if (prvEntry != null)
                        {
                            using (Stream stream = prvEntry.Open())
                            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                string prvContent = reader.ReadToEnd().Trim();
                                if (!string.IsNullOrWhiteSpace(prvContent))
                                {
                                    int lines = prvContent.Split('\n').Length;
                                    return new DocumentPreviewResult
                                    {
                                        ContentText = prvContent,
                                        LineCount = lines,
                                        CharCount = prvContent.Length,
                                        FormatType = "HWPX Document",
                                        Success = true
                                    };
                                }
                            }
                        }

                        // 2. Fallback: Parse section XML files
                        var sectionEntries = archive.Entries
                            .Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                                        (e.FullName.Contains("section", StringComparison.OrdinalIgnoreCase) ||
                                         e.Name.StartsWith("section", StringComparison.OrdinalIgnoreCase)))
                            .OrderBy(e => e.Name)
                            .ToList();

                        if (sectionEntries.Count == 0)
                        {
                            sectionEntries = archive.Entries
                                .Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(e => e.Name)
                                .ToList();
                        }

                        foreach (ZipArchiveEntry entry in sectionEntries)
                        {
                            using (Stream stream = entry.Open())
                            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                string xmlContent = reader.ReadToEnd();
                                string sectionText = ExtractTextFromHwpxXml(xmlContent);
                                if (!string.IsNullOrWhiteSpace(sectionText))
                                {
                                    sb.AppendLine(sectionText);
                                    sb.AppendLine();
                                }
                            }
                        }
                    }

                    string fullText = sb.ToString().Trim();
                    if (string.IsNullOrEmpty(fullText))
                    {
                        fullText = "(본문 텍스트를 추출할 수 없거나 빈 HWPX 문서입니다.)";
                    }

                    int lineCount = fullText.Split('\n').Length;

                    return new DocumentPreviewResult
                    {
                        ContentText = fullText,
                        LineCount = lineCount,
                        CharCount = fullText.Length,
                        FormatType = "HWPX",
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    return new DocumentPreviewResult
                    {
                        ContentText = $"[HWPX 읽기 오류] {ex.Message}",
                        FormatType = "HWPX",
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            });
        }

        private string ExtractTextFromHwpxXml(string xmlContent)
        {
            var sb = new StringBuilder();

            var matches = Regex.Matches(xmlContent, @"<[^>]*:t[^>]*>(.*?)</[^>]*:t>|<t[^>]*>(.*?)</t>", RegexOptions.Singleline);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    string val = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    if (string.IsNullOrEmpty(val)) continue;

                    val = Regex.Replace(val, @"<[^>]+>", " ");
                    string cleanText = WebUtility.HtmlDecode(val).Trim();
                    if (!string.IsNullOrEmpty(cleanText))
                    {
                        sb.AppendLine(cleanText);
                    }
                }
            }

            if (sb.Length == 0)
            {
                string rawText = Regex.Replace(xmlContent, @"<[^>]+>", " ");
                string cleanRaw = WebUtility.HtmlDecode(rawText);
                cleanRaw = Regex.Replace(cleanRaw, @"\s+", " ").Trim();
                if (!string.IsNullOrEmpty(cleanRaw))
                {
                    sb.AppendLine(cleanRaw);
                }
            }

            return sb.ToString().Trim();
        }
    }
}
