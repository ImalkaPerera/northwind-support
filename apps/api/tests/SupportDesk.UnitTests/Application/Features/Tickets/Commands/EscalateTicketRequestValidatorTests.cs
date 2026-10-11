using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    private bool IsValid(string? reason) =>
        _validator.Validate(new EscalateTicketRequest(reason!)).IsValid;

    [Fact]
    public void AValidRequest_Passes() =>
        Assert.True(IsValid("Customer is losing money"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("shrt")]
    [InlineData("  ab  ")]
    public void AMissingOrTooShortReason_Fails(string? reason) =>
        Assert.False(IsValid(reason));

    [Fact]
    public void AReasonOfExactlyTheMinimumLength_Passes() =>
        Assert.True(IsValid(new string('x', TicketEscalation.MinReasonLength)));

    [Fact]
    public void AReasonOfExactlyTheMaximumLength_Passes() =>
        Assert.True(IsValid(new string('x', TicketEscalation.MaxReasonLength)));

    [Fact]
    public void AReasonOverTheMaximumLength_Fails() =>
        Assert.False(IsValid(new string('x', TicketEscalation.MaxReasonLength + 1)));
}
