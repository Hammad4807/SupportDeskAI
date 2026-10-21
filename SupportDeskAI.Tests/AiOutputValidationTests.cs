using SupportDeskAI.DTOs;
using SupportDeskAI.Helpers;
using SupportDeskAI.Services;
using SupportDeskAI.Validators;

namespace SupportDeskAI.Tests;

// The AI answer is untrusted input: it must be valid JSON AND use only allowed values.
public class AiOutputValidationTests
{
    private static AiAnalysisResult ValidAnalysis() => new()
    {
        Category = "Account Access",
        Priority = "High",
        Sentiment = "Frustrated",
        Summary = "The customer cannot sign in after resetting their password.",
        SuggestedAction = "Check account status and the password reset flow."
    };

    // ---------- Parsing (malformed output) ----------

    [Fact]
    public void Parser_ValidJson_ReturnsTypedResult()
    {
        string json = """
            {"category":"Billing","priority":"Medium","sentiment":"Neutral","summary":"Charged twice.","suggestedAction":"Refund check."}
            """;

        var result = AiResponseParser.ParseAnalysis(json);

        Assert.NotNull(result);
        Assert.Equal("Billing", result.Category);
        Assert.Equal("Medium", result.Priority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sure! Here is my analysis of the ticket.")]
    [InlineData("{\"category\": \"Billing\", ")]                  // cut-off JSON
    [InlineData("{\"category\": \"Billing\", \"priority\": 3}")]   // number where text is expected
    public void Parser_MalformedOutput_ReturnsNull(string content)
    {
        Assert.Null(AiResponseParser.ParseAnalysis(content));
    }

    // ---------- Allowed values ----------

    [Fact]
    public void Validator_ValidAnalysis_Passes()
    {
        Assert.True(new AiAnalysisResultValidator().Validate(ValidAnalysis()).IsValid);
    }

    [Fact]
    public void Validator_IsCaseInsensitive()
    {
        var analysis = ValidAnalysis();
        analysis.Category = "account access";
        analysis.Priority = "high";

        Assert.True(new AiAnalysisResultValidator().Validate(analysis).IsValid);
    }

    [Theory]
    [InlineData("Urgent")]  // not a priority name
    [InlineData("7")]       // numbers are not accepted from the AI
    [InlineData("")]
    public void Validator_UnknownPriority_IsRejected(string priority)
    {
        var analysis = ValidAnalysis();
        analysis.Priority = priority;

        var result = new AiAnalysisResultValidator().Validate(analysis);

        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
    }

    [Fact]
    public void Validator_UnknownCategoryOrSentiment_IsRejected()
    {
        var analysis = ValidAnalysis();
        analysis.Category = "Hacking";
        analysis.Sentiment = "Ecstatic";

        var result = new AiAnalysisResultValidator().Validate(analysis);

        Assert.Contains(result.Errors, e => e.PropertyName == "Category");
        Assert.Contains(result.Errors, e => e.PropertyName == "Sentiment");
    }

    [Fact]
    public void Validator_EmptyOrTooLongText_IsRejected()
    {
        var analysis = ValidAnalysis();
        analysis.Summary = "";
        analysis.SuggestedAction = new string('x', 501);

        var result = new AiAnalysisResultValidator().Validate(analysis);

        Assert.Contains(result.Errors, e => e.PropertyName == "Summary");
        Assert.Contains(result.Errors, e => e.PropertyName == "SuggestedAction");
    }

    [Fact]
    public async Task MockProvider_AlwaysProducesValidOutput()
    {
        var mock = new MockAiTicketService();

        var result = await mock.AnalyzeTicketAsync("Unable to access my account", "I reset my password but still cannot sign in.", CancellationToken.None);

        Assert.True(new AiAnalysisResultValidator().Validate(result).IsValid);
        Assert.Equal("Account Access", result.Category);
        Assert.StartsWith("[MOCK]", result.Summary);
    }

    // ---------- Personal data ----------

    [Fact]
    public void Redactor_RemovesEmailsAndLongNumbers()
    {
        string text = "Contact ali.khan@example.com or +92 300 1234567, card 4111 1111 1111 1111.";

        string redacted = PersonalDataRedactor.Redact(text);

        Assert.DoesNotContain("ali.khan@example.com", redacted);
        Assert.DoesNotContain("1234567", redacted);
        Assert.DoesNotContain("4111", redacted);
        Assert.Contains("[email]", redacted);
        Assert.Contains("[number]", redacted);
    }

    [Fact]
    public void Redactor_KeepsNormalText()
    {
        Assert.Equal("Error 404 on page 2", PersonalDataRedactor.Redact("Error 404 on page 2"));
    }
}
