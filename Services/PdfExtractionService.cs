using UglyToad.PdfPig;

namespace KnowledgeAssistant.Api.Services;

public interface IPdfExtractionService
{
    List<PdfPageContent> ExtractPages(Stream pdfStream);
    List<PdfPageContent> ExtractPagesFromFile(string filePath);
}

public class PdfPageContent
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class PdfExtractionService : IPdfExtractionService
{
    private readonly ILogger<PdfExtractionService> _logger;

    public PdfExtractionService(ILogger<PdfExtractionService> logger)
    {
        _logger = logger;
    }

    public List<PdfPageContent> ExtractPages(Stream pdfStream)
    {
        var result = new List<PdfPageContent>();
        try
        {
            using var document = PdfDocument.Open(pdfStream);
            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new PdfPageContent
                    {
                        PageNumber = page.Number,
                        Text = text
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF stream");
            throw;
        }

        return result;
    }

    public List<PdfPageContent> ExtractPagesFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PDF file not found at {filePath}");
        }

        using var stream = File.OpenRead(filePath);
        return ExtractPages(stream);
    }
}
