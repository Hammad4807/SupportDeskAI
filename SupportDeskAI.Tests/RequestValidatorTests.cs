using SupportDeskAI.DTOs;
using SupportDeskAI.Validators;

namespace SupportDeskAI.Tests;

// Business rules for client input (FR-01, FR-04, FR-05, FR-02).
public class RequestValidatorTests
{
    [Fact]
    public void Create_ValidRequest_Passes()
    {
        var result = new CreateTicketRequestValidator().Validate(TestHelpers.ValidCreateRequest());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Title is required.")]
    [InlineData("   ", "Title is required.")]
    [InlineData("Hi", "Title must be at least 5 characters.")]
    public void Create_BadTitle_IsRejectedWithOneMessage(string title, string expectedMessage)
    {
        var result = new CreateTicketRequestValidator().Validate(TestHelpers.ValidCreateRequest(title: title));

        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedMessage, error.ErrorMessage);
    }

    [Fact]
    public void Create_TooLongDescription_IsRejected()
    {
        var result = new CreateTicketRequestValidator().Validate(
            TestHelpers.ValidCreateRequest(description: new string('x', 4001)));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(7)]
    public void Update_PriorityOutsideEnum_IsRejected(int priority)
    {
        var request = new UpdateTicketRequest { Title = "Valid title", Description = "A valid description.", Priority = priority };

        var result = new UpdateTicketRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == "Priority");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Status_ValidValues_Pass(int status)
    {
        var result = new UpdateTicketStatusRequestValidator().Validate(new UpdateTicketStatusRequest { Status = status });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Status_InvalidValue_IsRejected()
    {
        var result = new UpdateTicketStatusRequestValidator().Validate(new UpdateTicketStatusRequest { Status = 9 });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Query_NoFilters_Passes_And_InvalidPriority_IsRejected()
    {
        var validator = new TicketQueryParametersValidator();

        Assert.True(validator.Validate(new TicketQueryParameters()).IsValid);
        Assert.False(validator.Validate(new TicketQueryParameters { Priority = 9 }).IsValid);
    }
}
