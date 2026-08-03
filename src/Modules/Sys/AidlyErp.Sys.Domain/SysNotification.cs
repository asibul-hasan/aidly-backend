using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AidlyErp.Shared.Core;

namespace AidlyErp.Sys.Domain;

[Table("sys_notification")]
public class SysNotification : AuditEntity, ICompanyScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("notification_no")]
    public long NotificationNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }

    [Column("target_user_no")]
    public long TargetUserNo { get; set; }

    [Column("target_employee_no")]
    public long? TargetEmployeeNo { get; set; }

    [Column("sender_user_no")]
    public long? SenderUserNo { get; set; }

    [Column("menu_no")]
    public long? MenuNo { get; set; }

    [Column("form_id")]
    [StringLength(50)]
    public string? FormId { get; set; }

    [Column("document_type")]
    [StringLength(30)]
    public string? DocumentType { get; set; }

    [Column("document_pk")]
    public long? DocumentPk { get; set; }

    [Column("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [Column("title")]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Column("message")]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [Column("payload_json")]
    public string? PayloadJson { get; set; }

    /// <summary>0 = Unread, 1 = Read, 2 = ActionTaken</summary>
    [Column("status")]
    public short Status { get; set; } = 0;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }
}

[Table("sys_notification_template")]
public class SysNotificationTemplate : AuditEntity, ICompanyScopedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("template_no")]
    public long TemplateNo { get; set; }

    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("menu_no")]
    public long? MenuNo { get; set; }

    [Column("form_id")]
    [StringLength(50)]
    public string? FormId { get; set; }

    [Column("trigger_event")]
    [StringLength(50)]
    public string TriggerEvent { get; set; } = string.Empty;

    [Column("recipient_rule")]
    [StringLength(50)]
    public string RecipientRule { get; set; } = string.Empty;

    [Column("title_template")]
    [StringLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Column("message_template")]
    public string MessageTemplate { get; set; } = string.Empty;
}
