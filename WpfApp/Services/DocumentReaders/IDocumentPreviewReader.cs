using System.Threading.Tasks;

namespace WpfApp.Services.DocumentReaders
{
    public interface IDocumentPreviewReader
    {
        bool CanRead(string extension);
        Task<DocumentPreviewResult> ExtractTextAsync(string filePath);
    }
}
