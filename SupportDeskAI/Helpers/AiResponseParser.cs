using System.Text.Json;
using SupportDeskAI.DTOs;

namespace SupportDeskAI.Helpers;

// Turns the AI's text answer into a typed AiAnalysisResult using the built-in System.Text.Json.
// Groq is asked for JSON mode, so the answer should be pure JSON; if it is not (malformed AI output),
// this returns null instead of throwing.
public static class AiResponseParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static AiAnalysisResult? ParseAnalysis(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AiAnalysisResult>(content, Options);
        }
        catch (JsonException)
        {
            // e.g. broken JSON, or a number where a string was expected
            return null;
        }
    }
}
