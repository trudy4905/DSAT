using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WpfApp.Services.DocumentReaders
{
    public class HwpDocumentPreviewReader : IDocumentPreviewReader
    {
        private static readonly Encoding EucKrEncoding;

        static HwpDocumentPreviewReader()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            EucKrEncoding = Encoding.GetEncoding(949);
        }

        public bool CanRead(string extension)
        {
            return string.Equals(extension, ".hwp", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<DocumentPreviewResult> ExtractTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);

                    // 1. Check for PrvText stream in HWP 5.0 OLE Binary (Direct Preview Text)
                    string prvText = ExtractPrvTextFromHwpOle(fileBytes);
                    if (!string.IsNullOrWhiteSpace(prvText))
                    {
                        int lines = prvText.Split('\n').Length;
                        return new DocumentPreviewResult
                        {
                            ContentText = prvText,
                            LineCount = lines,
                            CharCount = prvText.Length,
                            FormatType = "HWP",
                            Success = true
                        };
                    }

                    // 2. Check for HWP 3.0 Format
                    if (fileBytes.Length >= 128)
                    {
                        string hwp3Magic = Encoding.ASCII.GetString(fileBytes, 0, 30);
                        if (hwp3Magic.StartsWith("HWP Document File V3.00", StringComparison.OrdinalIgnoreCase) ||
                            hwp3Magic.StartsWith("HWP", StringComparison.OrdinalIgnoreCase))
                        {
                            string hwp3Text = EucKrEncoding.GetString(fileBytes, 128, fileBytes.Length - 128);
                            hwp3Text = Regex.Replace(hwp3Text, @"[^\uAC00-\uD7A3\u1100-\u11FF\u3130-\u318Fa-zA-Z0-9\s\.,\?\!\-\(\)]", " ");
                            hwp3Text = Regex.Replace(hwp3Text, @"\s+", " ").Trim();

                            if (!string.IsNullOrWhiteSpace(hwp3Text))
                            {
                                int lineCount = hwp3Text.Split('\n').Length;
                                return new DocumentPreviewResult
                                {
                                    ContentText = hwp3Text,
                                    LineCount = lineCount,
                                    CharCount = hwp3Text.Length,
                                    FormatType = "HWP 3.0",
                                    Success = true
                                };
                            }
                        }
                    }

                    // 3. Fallback: Parse HWP 5.0 Section streams
                    string fullText = ParseHwpOleBodyText(fileBytes);

                    // 4. Fallback: Scan raw Korean text strings in UTF-16LE / EUC-KR
                    if (string.IsNullOrWhiteSpace(fullText))
                    {
                        fullText = ScanRawHwpKoreanText(fileBytes);
                    }

                    if (string.IsNullOrWhiteSpace(fullText))
                    {
                        fullText = "(HWP 문서 텍스트를 추출할 수 없거나 암호가 설정된 보안 문서입니다.)\n• OLE Compound File Header 및 FileHeader 파싱 완료.";
                    }

                    int totalLines = fullText.Split('\n').Length;

                    return new DocumentPreviewResult
                    {
                        ContentText = fullText,
                        LineCount = totalLines,
                        CharCount = fullText.Length,
                        FormatType = "HWP 5.0",
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    return new DocumentPreviewResult
                    {
                        ContentText = $"[HWP 읽기 오류] {ex.Message}",
                        FormatType = "HWP",
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            });
        }

        private class OleDirectoryEntry
        {
            public string Name { get; set; } = string.Empty;
            public int LeftSiblingId { get; set; }
            public int RightSiblingId { get; set; }
            public int ChildId { get; set; }
            public int StartSector { get; set; }
            public long Size { get; set; }
        }

        private string ExtractPrvTextFromHwpOle(byte[] data)
        {
            try
            {
                if (data.Length < 512) return string.Empty;
                byte[] oleMagic = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
                for (int i = 0; i < 8; i++)
                    if (data[i] != oleMagic[i]) return string.Empty;

                int sectorShift = ReadU16LE(data, 30);
                int sectorSize = 1 << sectorShift;
                int miniSectorShift = ReadU16LE(data, 32);
                int miniSectorSize = 1 << miniSectorShift;
                int fatCount = ReadS32LE(data, 44);
                int dirStartSector = ReadS32LE(data, 48);
                int miniFatStart = ReadS32LE(data, 60);
                long miniSizeLimit = ReadU32LE_Long(data, 56);

                var fatSectors = new List<int>();
                for (int i = 0; i < 109 && i < fatCount; i++)
                {
                    int sec = ReadS32LE(data, 76 + i * 4);
                    if (sec >= 0) fatSectors.Add(sec);
                }

                int totalFatEntries = fatSectors.Count * (sectorSize / 4);
                var fat = new int[totalFatEntries];
                for (int fi = 0; fi < fatSectors.Count; fi++)
                {
                    int secOff = SectorOffset(fatSectors[fi], sectorSize);
                    for (int j = 0; j < sectorSize / 4; j++)
                    {
                        int idx = fi * (sectorSize / 4) + j;
                        if (idx < fat.Length && secOff + j * 4 + 4 <= data.Length)
                            fat[idx] = ReadS32LE(data, secOff + j * 4);
                    }
                }

                byte[] dirData = ReadChain(data, fat, dirStartSector, sectorSize);
                int entryCount = dirData.Length / 128;
                var entries = new List<OleDirectoryEntry>();

                for (int e = 0; e < entryCount; e++)
                {
                    int off = e * 128;
                    if (off + 128 > dirData.Length) break;

                    int nameLen = ReadU16LE(dirData, off + 64);
                    string name = string.Empty;
                    if (nameLen >= 2)
                    {
                        nameLen = Math.Min(nameLen - 2, 62);
                        name = Encoding.Unicode.GetString(dirData, off, nameLen).TrimEnd('\0').Trim();
                    }

                    entries.Add(new OleDirectoryEntry
                    {
                        Name = name,
                        LeftSiblingId = ReadS32LE(dirData, off + 68),
                        RightSiblingId = ReadS32LE(dirData, off + 72),
                        ChildId = ReadS32LE(dirData, off + 76),
                        StartSector = ReadS32LE(dirData, off + 116),
                        Size = ReadU32LE_Long(dirData, off + 120)
                    });
                }

                int[] miniFat = Array.Empty<int>();
                if (miniFatStart >= 0)
                {
                    byte[] miniFatData = ReadChain(data, fat, miniFatStart, sectorSize);
                    miniFat = new int[miniFatData.Length / 4];
                    for (int i = 0; i < miniFat.Length; i++)
                        miniFat[i] = ReadS32LE(miniFatData, i * 4);
                }

                int rootMiniStreamSector = entries.Count > 0 ? entries[0].StartSector : -1;

                // Look for PrvText or \x05PrvText stream
                var prvEntry = entries.FirstOrDefault(e => e.Name.EndsWith("PrvText", StringComparison.OrdinalIgnoreCase) || e.Name.Contains("PrvText"));
                if (prvEntry != null && prvEntry.Size > 0)
                {
                    byte[] rawStream = ReadStreamData(data, fat, prvEntry.StartSector, (int)prvEntry.Size, sectorSize, miniSectorSize, miniSizeLimit, miniFat, rootMiniStreamSector);
                    if (rawStream.Length > 0)
                    {
                        // PrvText is UTF-16LE plain text stream stored by Hancom Office
                        string text = Encoding.Unicode.GetString(rawStream).TrimEnd('\0').Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        private string ParseHwpOleBodyText(byte[] data)
        {
            if (data.Length < 512) return string.Empty;
            byte[] oleMagic = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            for (int i = 0; i < 8; i++)
                if (data[i] != oleMagic[i]) return string.Empty;

            int sectorShift = ReadU16LE(data, 30);
            int sectorSize = 1 << sectorShift;
            int miniSectorShift = ReadU16LE(data, 32);
            int miniSectorSize = 1 << miniSectorShift;
            int fatCount = ReadS32LE(data, 44);
            int dirStartSector = ReadS32LE(data, 48);
            int miniFatStart = ReadS32LE(data, 60);
            long miniSizeLimit = ReadU32LE_Long(data, 56);

            var fatSectors = new List<int>();
            for (int i = 0; i < 109 && i < fatCount; i++)
            {
                int sec = ReadS32LE(data, 76 + i * 4);
                if (sec >= 0) fatSectors.Add(sec);
            }

            int totalFatEntries = fatSectors.Count * (sectorSize / 4);
            var fat = new int[totalFatEntries];
            for (int fi = 0; fi < fatSectors.Count; fi++)
            {
                int secOff = SectorOffset(fatSectors[fi], sectorSize);
                for (int j = 0; j < sectorSize / 4; j++)
                {
                    int idx = fi * (sectorSize / 4) + j;
                    if (idx < fat.Length && secOff + j * 4 + 4 <= data.Length)
                        fat[idx] = ReadS32LE(data, secOff + j * 4);
                }
            }

            byte[] dirData = ReadChain(data, fat, dirStartSector, sectorSize);

            int entryCount = dirData.Length / 128;
            var entries = new List<OleDirectoryEntry>();
            for (int e = 0; e < entryCount; e++)
            {
                int off = e * 128;
                if (off + 128 > dirData.Length) break;

                int nameLen = ReadU16LE(dirData, off + 64);
                string name = string.Empty;
                if (nameLen >= 2)
                {
                    nameLen = Math.Min(nameLen - 2, 62);
                    name = Encoding.Unicode.GetString(dirData, off, nameLen).TrimEnd('\0').Trim();
                }

                entries.Add(new OleDirectoryEntry
                {
                    Name = name,
                    LeftSiblingId = ReadS32LE(dirData, off + 68),
                    RightSiblingId = ReadS32LE(dirData, off + 72),
                    ChildId = ReadS32LE(dirData, off + 76),
                    StartSector = ReadS32LE(dirData, off + 116),
                    Size = ReadU32LE_Long(dirData, off + 120)
                });
            }

            bool isCompressed = true;
            var fileHeaderEntry = entries.FirstOrDefault(e => string.Equals(e.Name, "FileHeader", StringComparison.OrdinalIgnoreCase));
            if (fileHeaderEntry != null)
            {
                byte[] fhData = ReadStreamData(data, fat, fileHeaderEntry.StartSector, (int)fileHeaderEntry.Size, sectorSize, miniSectorSize, miniSizeLimit, null, -1);
                if (fhData.Length >= 36)
                {
                    uint flags = ReadU32LE_U(fhData, 32);
                    bool encrypted = (flags & 0x1) != 0;
                    isCompressed = (flags & 0x2) != 0;
                    if (encrypted) return "(보안 문서: 암호화되어 본문 텍스트를 추출할 수 없습니다.)";
                }
            }

            int[] miniFat = Array.Empty<int>();
            if (miniFatStart >= 0)
            {
                byte[] miniFatData = ReadChain(data, fat, miniFatStart, sectorSize);
                miniFat = new int[miniFatData.Length / 4];
                for (int i = 0; i < miniFat.Length; i++)
                    miniFat[i] = ReadS32LE(miniFatData, i * 4);
            }

            int rootMiniStreamSector = entries.Count > 0 ? entries[0].StartSector : -1;

            var sectionEntries = entries
                .Where(e => e.Name.StartsWith("Section", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Name)
                .ToList();

            if (sectionEntries.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var secEntry in sectionEntries)
            {
                byte[] rawStream = ReadStreamData(data, fat, secEntry.StartSector, (int)secEntry.Size, sectorSize, miniSectorSize, miniSizeLimit, miniFat, rootMiniStreamSector);

                byte[] sectionData = rawStream;
                if (isCompressed && rawStream.Length > 0)
                {
                    sectionData = TryDecompressZlib(rawStream);
                }

                ParseHwpRecords(sectionData, sb);
            }

            return sb.ToString().Trim();
        }

        private void ParseHwpRecords(byte[] data, StringBuilder sb)
        {
            int pos = 0;
            while (pos + 4 <= data.Length)
            {
                uint header = ReadU32LE_U(data, pos);
                pos += 4;

                int tagId = (int)(header & 0x3FF);
                int size = (int)((header >> 20) & 0xFFF);
                if (size == 0xFFF)
                {
                    if (pos + 4 > data.Length) break;
                    size = (int)ReadU32LE_U(data, pos);
                    pos += 4;
                }

                if (tagId == 67 && size > 0 && pos + size <= data.Length)
                {
                    int i = 0;
                    while (i + 1 < size)
                    {
                        char c = (char)ReadU16LE(data, pos + i);
                        i += 2;

                        if (c == 10 || c == 13 || c == 0x000D)
                        {
                            sb.AppendLine();
                        }
                        else if (c >= 1 && c <= 3)
                        {
                            i += 2;
                        }
                        else if ((c >= 4 && c <= 9) || c == 19 || c == 20)
                        {
                            i += 14;
                        }
                        else if (c >= 11 && c <= 18)
                        {
                            i += 2;
                        }
                        else if (c >= 24 && c <= 31)
                        {
                            i += 2;
                        }
                        else if (c >= 0x0020 && c != 0xFFFF)
                        {
                            sb.Append(c);
                        }
                    }
                    sb.AppendLine();
                }

                if (pos + size > data.Length) break;
                pos += size;
            }
        }

        private string ScanRawHwpKoreanText(byte[] bytes)
        {
            var sb = new StringBuilder();
            try
            {
                int len = bytes.Length;
                for (int i = 0; i + 1 < len; i += 2)
                {
                    char c = (char)(bytes[i] | (bytes[i + 1] << 8));
                    if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x1100 && c <= 0x11FF) || (c >= 0x3130 && c <= 0x318F) ||
                        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == ' ')
                    {
                        sb.Append(c);
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        sb.AppendLine();
                    }
                }
            }
            catch { }

            string res = sb.ToString();
            res = Regex.Replace(res, @"\s+", " ").Trim();
            return res.Length > 20 ? res : string.Empty;
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

        private byte[] ReadChain(byte[] data, int[] fat, int startSector, int sectorSize)
        {
            var result = new List<byte>();
            int current = startSector;
            var visited = new HashSet<int>();
            while (current >= 0 && current < fat.Length && !visited.Contains(current))
            {
                visited.Add(current);
                int off = SectorOffset(current, sectorSize);
                int end = Math.Min(off + sectorSize, data.Length);
                if (off >= data.Length) break;
                result.AddRange(data[off..end]);
                current = fat[current];
            }
            return result.ToArray();
        }

        private byte[] ReadStreamData(byte[] data, int[] fat, int startSector, int streamSize, int sectorSize, int miniSectorSize, long miniSizeLimit, int[]? miniFat, int rootMiniStreamSector)
        {
            if (streamSize > 0 && streamSize < miniSizeLimit && miniFat != null && miniFat.Length > 0 && rootMiniStreamSector >= 0)
            {
                byte[] miniStream = ReadChain(data, fat, rootMiniStreamSector, sectorSize);
                var result = new List<byte>();
                int current = startSector;
                var visited = new HashSet<int>();
                while (current >= 0 && current < miniFat.Length && !visited.Contains(current))
                {
                    visited.Add(current);
                    int off = current * miniSectorSize;
                    int end = Math.Min(off + miniSectorSize, miniStream.Length);
                    if (off >= miniStream.Length) break;
                    result.AddRange(miniStream[off..end]);
                    current = miniFat[current];
                }
                return result.Count > streamSize ? result.ToArray()[0..streamSize] : result.ToArray();
            }
            else
            {
                byte[] raw = ReadChain(data, fat, startSector, sectorSize);
                return raw.Length > streamSize ? raw[0..streamSize] : raw;
            }
        }

        private int SectorOffset(int sector, int sectorSize) => 512 + sector * sectorSize;
        private int ReadU16LE(byte[] data, int offset) => (offset + 1 >= data.Length) ? 0 : data[offset] | (data[offset + 1] << 8);
        private int ReadS32LE(byte[] data, int offset) => (offset + 3 >= data.Length) ? -1 : (int)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        private long ReadU32LE_Long(byte[] data, int offset) => (offset + 3 >= data.Length) ? 0 : (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        private uint ReadU32LE_U(byte[] data, int offset) => (offset + 3 >= data.Length) ? 0 : (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }
}
