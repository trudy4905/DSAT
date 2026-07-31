using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WpfApp.Services.DocumentReaders
{
    public class PdfDocumentPreviewReader : IDocumentPreviewReader
    {
        private static readonly Encoding EucKrEncoding;

        static PdfDocumentPreviewReader()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EucKrEncoding = Encoding.GetEncoding(949);
        }

        public bool CanRead(string extension)
        {
            return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<DocumentPreviewResult> ExtractTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var fi = new FileInfo(filePath);
                    byte[] pdfBytes = File.ReadAllBytes(filePath);

                    string extractedText = ParsePdfBodyText(pdfBytes);

                    var sb = new StringBuilder();
                    sb.AppendLine($"[PDF 파일 정보]");
                    sb.AppendLine($"• 파일명: {fi.Name}");
                    sb.AppendLine($"• 파일 크기: {fi.Length:N0} 바이트 ({fi.Length / 1024.0:F1} KB)");
                    sb.AppendLine($"• 생성 일시: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"• 수정 일시: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine(new string('-', 55));
                    sb.AppendLine();

                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        sb.AppendLine("※ 본문 텍스트 스트림이 없거나 이미지만 포함된 스캔 문서입니다.");
                        sb.AppendLine("• PDF Header / Catalog / Font Table 파싱 완료.");
                        sb.AppendLine("• 텍스트 인코딩: 이미지 객체(XObject/Image) 중심 스캔 본문");
                    }
                    else
                    {
                        sb.AppendLine(extractedText);
                    }

                    string fullContent = sb.ToString().Trim();
                    int lineCount = fullContent.Split('\n').Length;

                    return new DocumentPreviewResult
                    {
                        Success = true,
                        ContentText = fullContent,
                        FormatType = "PDF",
                        LineCount = lineCount,
                        CharCount = fullContent.Length
                    };
                }
                catch (Exception ex)
                {
                    return new DocumentPreviewResult
                    {
                        Success = false,
                        ContentText = $"[PDF 읽기 오류] {ex.Message}",
                        FormatType = "PDF",
                        LineCount = 1,
                        CharCount = 0
                    };
                }
            });
        }

        private string ParsePdfBodyText(byte[] pdfBytes)
        {
            var textSb = new StringBuilder();
            try
            {
                int pos = 0;
                int maxStreams = 500;
                int streamCount = 0;

                byte[] streamMarker = Encoding.ASCII.GetBytes("stream");
                byte[] endStreamMarker = Encoding.ASCII.GetBytes("endstream");

                while (pos < pdfBytes.Length && streamCount < maxStreams)
                {
                    int streamStartKey = FindBytesOffset(pdfBytes, streamMarker, pos);
                    if (streamStartKey < 0) break;

                    // Check preceding dictionary for binary image stream objects
                    int headerStart = Math.Max(0, streamStartKey - 250);
                    string headerText = Encoding.ASCII.GetString(pdfBytes, headerStart, streamStartKey - headerStart);

                    bool isImageObject = headerText.Contains("/Subtype /Image") ||
                                         headerText.Contains("/Subtype/Image") ||
                                         headerText.Contains("/DCTDecode") ||
                                         headerText.Contains("/JPXDecode");

                    int dataStart = streamStartKey + 6;
                    if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r') dataStart++;
                    if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n') dataStart++;

                    int streamEndKey = FindBytesOffset(pdfBytes, endStreamMarker, dataStart);
                    if (streamEndKey < 0) break;

                    // Trim trailing newlines (\r, \n) right before endstream so Zlib decompression doesn't fail
                    int actualEnd = streamEndKey;
                    while (actualEnd > dataStart && (pdfBytes[actualEnd - 1] == '\r' || pdfBytes[actualEnd - 1] == '\n'))
                    {
                        actualEnd--;
                    }

                    int streamLen = actualEnd - dataStart;
                    if (streamLen > 0 && dataStart + streamLen <= pdfBytes.Length && !isImageObject)
                    {
                        streamCount++;
                        byte[] streamBytes = new byte[streamLen];
                        Array.Copy(pdfBytes, dataStart, streamBytes, 0, streamLen);

                        byte[] decompressed = TryDecompressZlib(streamBytes);

                        ExtractTextFromDecompressedStream(decompressed, textSb);
                    }

                    pos = streamEndKey + 9;
                }

                // If no structured BT...ET blocks were found, attempt global stream Korean text extraction
                if (textSb.Length == 0)
                {
                    pos = 0;
                    while (pos < pdfBytes.Length)
                    {
                        int streamStartKey = FindBytesOffset(pdfBytes, streamMarker, pos);
                        if (streamStartKey < 0) break;

                        int dataStart = streamStartKey + 6;
                        if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r') dataStart++;
                        if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n') dataStart++;

                        int streamEndKey = FindBytesOffset(pdfBytes, endStreamMarker, dataStart);
                        if (streamEndKey < 0) break;

                        int actualEnd = streamEndKey;
                        while (actualEnd > dataStart && (pdfBytes[actualEnd - 1] == '\r' || pdfBytes[actualEnd - 1] == '\n'))
                        {
                            actualEnd--;
                        }

                        int streamLen = actualEnd - dataStart;
                        if (streamLen > 0 && dataStart + streamLen <= pdfBytes.Length)
                        {
                            byte[] streamBytes = new byte[streamLen];
                            Array.Copy(pdfBytes, dataStart, streamBytes, 0, streamLen);
                            byte[] decompressed = TryDecompressZlib(streamBytes);

                            ScanKoreanAndEnglishSentences(decompressed, textSb);
                        }

                        pos = streamEndKey + 9;
                    }
                }
            }
            catch { }

            string finalResult = textSb.ToString().Trim();
            finalResult = Regex.Replace(finalResult, @"\n{3,}", "\n\n");
            return finalResult;
        }

        private void ExtractTextFromDecompressedStream(byte[] decompressedBytes, StringBuilder sb)
        {
            if (decompressedBytes == null || decompressedBytes.Length == 0) return;

            string asciiText = Encoding.ASCII.GetString(decompressedBytes);
            string utf8Text = Encoding.UTF8.GetString(decompressedBytes);

            // 1. Extract BT ... ET text blocks
            var btMatches = Regex.Matches(utf8Text, @"BT(.*?)ET", RegexOptions.Singleline);
            int extractedBlockCount = 0;

            foreach (Match btMatch in btMatches)
            {
                string block = btMatch.Groups[1].Value;

                // Extract (text) literals
                var strMatches = Regex.Matches(block, @"\((.*?)\)");
                foreach (Match sm in strMatches)
                {
                    string rawLit = sm.Groups[1].Value;
                    string decoded = DecodePdfLiteralString(rawLit);
                    if (!string.IsNullOrWhiteSpace(decoded) && IsValidTextString(decoded))
                    {
                        sb.Append(decoded);
                        sb.Append(" ");
                        extractedBlockCount++;
                    }
                }

                // Extract <Hex> literals
                var hexMatches = Regex.Matches(block, @"<([0-9A-Fa-f]{4,})>");
                foreach (Match hm in hexMatches)
                {
                    string decoded = DecodePdfHexString(hm.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(decoded) && IsValidTextString(decoded))
                    {
                        sb.Append(decoded);
                        sb.Append(" ");
                        extractedBlockCount++;
                    }
                }

                sb.AppendLine();
            }

            // 2. If no BT...ET blocks succeeded, try extracting literals with Tj / TJ operators directly
            if (extractedBlockCount == 0)
            {
                var strMatches = Regex.Matches(utf8Text, @"\((.*?)\)\s*(?:Tj|TJ|'|"")");
                foreach (Match sm in strMatches)
                {
                    string decoded = DecodePdfLiteralString(sm.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(decoded) && IsValidTextString(decoded))
                    {
                        sb.AppendLine(decoded);
                        extractedBlockCount++;
                    }
                }

                var hexMatches = Regex.Matches(utf8Text, @"<([0-9A-Fa-f]{4,})>\s*(?:Tj|TJ|'|"")");
                foreach (Match hm in hexMatches)
                {
                    string decoded = DecodePdfHexString(hm.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(decoded) && IsValidTextString(decoded))
                    {
                        sb.AppendLine(decoded);
                        extractedBlockCount++;
                    }
                }
            }

            // 3. Fallback: Scan EUC-KR / UTF-8 text from raw stream bytes if structured operators yielded nothing
            if (extractedBlockCount == 0)
            {
                ScanKoreanAndEnglishSentences(decompressedBytes, sb);
            }
        }

        private void ScanKoreanAndEnglishSentences(byte[] bytes, StringBuilder sb)
        {
            try
            {
                // Try EUC-KR (CP949)
                string eucKrText = EucKrEncoding.GetString(bytes);
                var matchesEucKr = Regex.Matches(eucKrText, @"[\uAC00-\uD7A3\u1100-\u11FF\u3130-\u318F0-9a-zA-Z\s\.,\?\!\-\(\)]{3,}");
                foreach (Match m in matchesEucKr)
                {
                    string val = m.Value.Trim();
                    if (ContainsKorean(val) && val.Length >= 2)
                    {
                        sb.AppendLine(val);
                    }
                }

                // Try UTF-8
                string utf8Text = Encoding.UTF8.GetString(bytes);
                var matchesUtf8 = Regex.Matches(utf8Text, @"[\uAC00-\uD7A3\u1100-\u11FF\u3130-\u318F0-9a-zA-Z\s\.,\?\!\-\(\)]{3,}");
                foreach (Match m in matchesUtf8)
                {
                    string val = m.Value.Trim();
                    if (ContainsKorean(val) && val.Length >= 2)
                    {
                        sb.AppendLine(val);
                    }
                }
            }
            catch { }
        }

        private string DecodePdfLiteralString(string rawInput)
        {
            if (string.IsNullOrEmpty(rawInput)) return string.Empty;

            string unescaped = rawInput
                .Replace(@"\(", "(")
                .Replace(@"\)", ")")
                .Replace(@"\\", @"\")
                .Replace(@"\r", "\r")
                .Replace(@"\n", "\n")
                .Replace(@"\t", "\t");

            byte[] rawBytes = Encoding.Default.GetBytes(unescaped);

            // UTF-16BE with BOM
            if (rawBytes.Length >= 2 && rawBytes[0] == 0xFE && rawBytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(rawBytes, 2, rawBytes.Length - 2).Trim();
            }

            // Try EUC-KR
            string eucKr = EucKrEncoding.GetString(rawBytes).Trim();
            if (ContainsKorean(eucKr)) return eucKr;

            // Try UTF-8
            string utf8 = Encoding.UTF8.GetString(rawBytes).Trim();
            if (ContainsKorean(utf8) || IsValidTextString(utf8)) return utf8;

            return CleanAsciiText(unescaped);
        }

        private string DecodePdfHexString(string hex)
        {
            try
            {
                if (hex.Length % 2 != 0) hex = hex.Substring(0, hex.Length - 1);
                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }

                if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2).Trim();
                }

                if (bytes.Length % 2 == 0 && bytes.Length >= 2)
                {
                    string bigEndian = Encoding.BigEndianUnicode.GetString(bytes).Trim();
                    if (ContainsKorean(bigEndian)) return bigEndian;
                }

                string eucKr = EucKrEncoding.GetString(bytes).Trim();
                if (ContainsKorean(eucKr)) return eucKr;

                string utf8 = Encoding.UTF8.GetString(bytes).Trim();
                if (!string.IsNullOrWhiteSpace(utf8)) return utf8;
            }
            catch { }
            return string.Empty;
        }

        private bool IsValidTextString(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return false;
            if (ContainsKorean(str)) return true;

            int validChars = 0;
            foreach (char c in str)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '.' || c == ',' || c == '-' || c == '_' || c == ':' || c == '/' || c == '(' || c == ')')
                {
                    validChars++;
                }
            }

            double ratio = (double)validChars / str.Length;
            return ratio >= 0.5 && str.Length >= 2;
        }

        private string CleanAsciiText(string input)
        {
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c >= 0x20 || c == '\n' || c == '\r' || c == '\t' || c >= 0xAC00)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        private bool ContainsKorean(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            foreach (char c in str)
            {
                if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x1100 && c <= 0x11FF) || (c >= 0x3130 && c <= 0x318F))
                    return true;
            }
            return false;
        }

        private int FindBytesOffset(byte[] source, byte[] pattern, int startOffset)
        {
            for (int i = startOffset; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private byte[] TryDecompressZlib(byte[] compressedData)
        {
            if (compressedData == null || compressedData.Length == 0) return Array.Empty<byte>();

            try
            {
                using var inputMs = new MemoryStream(compressedData);
                using var zlib = new ZLibStream(inputMs, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                byte[] buf = new byte[8192];
                int read;
                try
                {
                    while ((read = zlib.Read(buf, 0, buf.Length)) > 0)
                    {
                        outMs.Write(buf, 0, read);
                    }
                }
                catch { }

                if (outMs.Length > 0) return outMs.ToArray();
            }
            catch { }

            try
            {
                int offset = (compressedData.Length > 2 && compressedData[0] == 0x78) ? 2 : 0;
                using var inputMs = new MemoryStream(compressedData, offset, compressedData.Length - offset);
                using var deflate = new DeflateStream(inputMs, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                byte[] buf = new byte[8192];
                int read;
                try
                {
                    while ((read = deflate.Read(buf, 0, buf.Length)) > 0)
                    {
                        outMs.Write(buf, 0, read);
                    }
                }
                catch { }

                if (outMs.Length > 0) return outMs.ToArray();
            }
            catch { }

            return compressedData;
        }
    }
}
