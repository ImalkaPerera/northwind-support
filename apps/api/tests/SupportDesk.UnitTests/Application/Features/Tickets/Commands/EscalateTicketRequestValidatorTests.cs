using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    private bool IsValid(string? reason, string? escalatedBy) =>
        _validator.Validate(new EscalateTicketRequest(reason!, escalatedBy!)).IsValid;

    [Fact]
    public void AValidRequest_Passes() =>
        Assert.True(IsValid("Customer is losing money", "Alex Turner"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("shrt")]
    [InlineData("  ab  ")]
    public void AMissingOrTooShortReason_Fails(string? reason) =>
        Assert.False(IsValid(reason, "Alex Turner"));

    [Fact]
    public void AReasonOfExactlyTheMinimumLength_Passes() =>
        Assert.True(IsValid(new string('x', TicketEscalation.MinReasonLength), "Alex Turner"));

    [Fact]
    public void AReasonOfExactlyTheMaximumLength_Passes() =>
        Assert.True(IsValid(new string('x', TicketEscalation.MaxReasonLength), "Alex Turner"));

    [Fact]
    public void AReasonOverTheMaximumLength_Fails() =>
        Assert.False(IsValid(new string('x', TicketEscalation.MaxReasonLength + 1), "Alex Turner"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingActor_Fails(string? escalatedBy) =>
        Assert.False(IsValid("Customer is losing money", escalatedBy));

    [Fact]
    public void AnActorOverTheMaximumLength_Fails() =>
        Assert.False(IsValid("Customer is losing money", new string('x', TicketEscalation.MaxEscalatedByLength + 1)));
}
