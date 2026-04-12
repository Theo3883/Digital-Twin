namespace DigitalTwin.Mobile.Domain.Interfaces;

public interface ISensitiveDataSanitizer
{
    string Sanitize(string text);
    string BuildSanitizedPreview(IEnumerable<string> pageTexts, int maxLength = 2000);
}
