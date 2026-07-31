using System;
using System.Net;
using System.Text;

namespace WpfApp.Services
{
    public static class HwpHtmlDocumentGenerator
    {
        public static string GenerateHwpHtmlDocument(string title, string contentText, string formatType, string filePath, string fileSize, string lastModified)
        {
            string safeTitle = WebUtility.HtmlEncode(title);
            string safeFormat = WebUtility.HtmlEncode(formatType);
            string safeSize = WebUtility.HtmlEncode(fileSize);
            string safeModified = WebUtility.HtmlEncode(lastModified);

            string safeContent = WebUtility.HtmlEncode(contentText ?? string.Empty);
            safeContent = safeContent.Replace("\r\n", "<br/>").Replace("\n", "<br/>");

            return $@"<!DOCTYPE html>
<html lang='ko'>
<head>
<meta charset='utf-8'/>
<meta name='viewport' content='width=device-width, initial-scale=1.0'/>
<style>
    * {{
        box-sizing: border-box;
    }}
    body {{
        background-color: #F1F5F9;
        margin: 0;
        padding: 24px 16px;
        font-family: 'Malgun Gothic', '맑은 고딕', -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif;
        display: flex;
        justify-content: center;
        color: #1E293B;
    }}
    .paper-sheet {{
        background-color: #FFFFFF;
        width: 100%;
        max-width: 820px;
        min-height: 980px;
        padding: 56px 64px;
        box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08), 0 1px 3px rgba(0, 0, 0, 0.04);
        border: 1px solid #CBD5E1;
        border-radius: 4px;
        position: relative;
    }}
    .doc-header-bar {{
        border-bottom: 2px solid #3B82F6;
        padding-bottom: 16px;
        margin-bottom: 28px;
        display: flex;
        justify-content: space-between;
        align-items: flex-end;
    }}
    .doc-title {{
        font-size: 20px;
        font-weight: 700;
        color: #0F172A;
        letter-spacing: -0.5px;
    }}
    .doc-badge {{
        background-color: #3B82F6;
        color: #FFFFFF;
        padding: 4px 12px;
        border-radius: 4px;
        font-size: 12px;
        font-weight: 700;
        letter-spacing: 0.5px;
    }}
    .doc-body-content {{
        font-size: 14.5px;
        line-height: 1.85;
        color: #334155;
        word-break: break-all;
    }}
    .doc-footer {{
        margin-top: 60px;
        padding-top: 20px;
        border-top: 1px solid #F1F5F9;
        text-align: center;
        font-size: 11.5px;
        color: #94A3B8;
    }}
</style>
</head>
<body>
    <div class='paper-sheet'>
        <div class='doc-header-bar'>
            <div class='doc-title'>📄 {safeTitle}</div>
        </div>
        <div class='doc-body-content'>
            {safeContent}
        </div>
        <div class='doc-footer'>
            DSAT Forensic Document Viewer • 정적 격리 모드
        </div>
    </div>
</body>
</html>";
        }
    }
}
