using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AidlyErp.Shared.Core;

[Table("sys_event_outbox")]
public class EventOutbox
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("event_no")]
    public long EventNo { get; set; }

    [Column("company_no")]
    public long CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("aggregate_type")]
    [StringLength(40)]
    public string AggregateType { get; set; } = string.Empty;

    [Column("aggregate_id")]
    [StringLength(60)]
    public string AggregateId { get; set; } = string.Empty;

    [Column("event_type")]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = string.Empty;

    [Column("status")]
    public short Status { get; set; } = 1; // 1=Pending, 2=Published, 3=Failed

    [Column("retry_count")]
    public short RetryCount { get; set; } = 0;

    [Column("available_at")]
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Convenience view of <see cref="Status"/>. There is no <c>is_processed</c> column — the Java
    /// schema tracks drain state via <c>status</c> (1 = pending) and <c>published_at</c>. Mapping
    /// this made every outbox query select a non-existent column, which crashed the GL posting
    /// consumer on every poll.
    /// </summary>
    [NotMapped]
    public short IsProcessed
    {
        get => (short)(PublishedAt != null ? 1 : 0);
        set => PublishedAt = value == 1 ? PublishedAt ?? DateTime.UtcNow : null;
    }
}
