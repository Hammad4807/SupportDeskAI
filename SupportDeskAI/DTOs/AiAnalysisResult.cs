namespace SupportDeskAI.DTOs;

// The JSON shape we ask the AI to return for an analysis.
// Every value is a plain string here because it comes from the AI and is NOT trusted yet:
// AiAnalysisResultValidator checks it against the allowed values before anything is saved.
public class AiAnalysisResult
{
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
}
