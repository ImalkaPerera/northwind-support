using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// The escalation history of a ticket. Rows are written once and never updated, and they
/// refer to the ticket and the agents by id only.
/// </summary>
/// <remarks>
/// Every foreign key is <see cref="DeleteBehavior.Restrict"/>: the history is an audit trail, so
/// deleting a ticket or an agent must fail loudly instead of silently removing it.
/// </remarks>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", t =>
        {
            t.HasCheckConstraint("CK_TicketEscalations_FromPriority", "[FromPriority] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_TicketEscalations_ToPriority", "[ToPriority] BETWEEN 1 AND 4");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority).HasConversion<int>();

        builder.Property(e => e.ToPriority).HasConversion<int>();

        builder.Property(e => e.Reason).HasMaxLength(TicketEscalation.MaxReasonLength).IsRequired();

        builder.Property(e => e.EscalatedBy).HasMaxLength(TicketEscalation.MaxEscalatedByLength).IsRequired();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // Both agents are optional: a ticket can be escalated while it has no owner, and
        // reassignment can find nobody eligible.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Serves "the history of one ticket, newest first" and is the index on the TicketId
        // foreign key, so no separate TicketId index is needed.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}
