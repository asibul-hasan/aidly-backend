using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AidlyErp.Domain.Common;

/// <summary>
/// Base audit superclass for every soft-deletable aggregate in the SYS module.
///
/// <para>Mirrors the columns shared by every master table in <c>db-updated.md</c>:
/// <c>is_active</c>, <c>is_deleted</c>, <c>created_by/at</c>, <c>updated_by/at</c>,
/// <c>deleted_by/at</c>, <c>row_version</c>.</para>
///
/// <para><b>Optimistic locking:</b> <see cref="ConcurrencyCheckAttribute"/> on <c>row_version</c>.
/// The schema declares <c>DEFAULT 1</c>; the field initialises to <c>1</c> so an INSERT lands at
/// version 1 and every UPDATE increments. <see cref="InitVersion"/> guards against legacy
/// <c>NULL</c> values left over before the default was added.</para>
///
/// <para><b>Soft-delete:</b> <see cref="PerformSoftDelete"/> flips <c>is_deleted=1</c>, records
/// who/when, and calls <see cref="NullifyBusinessId"/> so subclasses can free unique constraint
/// slots (e.g. set <c>company_id = null</c>).</para>
/// </summary>
public abstract class AuditEntity : IAuditEntity, ISoftDelete
{
    // -----------------------------------------------------------------------
    // Lifecycle flags (schema: is_active, is_deleted — SMALLINT NOT NULL)
    // -----------------------------------------------------------------------

    /// <summary>1 = active, 0 = inactive. Default 1 on insert.</summary>
    [Column("is_active")]
    public short IsActive { get; set; } = 1;

    /// <summary>0 = live, 1 = soft-deleted. Default 0 on insert.</summary>
    [Column("is_deleted")]
    public short IsDeleted { get; set; } = 0;

    // -----------------------------------------------------------------------
    // Created audit — set once on INSERT, never updated
    // -----------------------------------------------------------------------

    [Column("created_by")]
    public long? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // -----------------------------------------------------------------------
    // Updated audit — refreshed on every save
    // -----------------------------------------------------------------------

    [Column("updated_by")]
    public long? UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // -----------------------------------------------------------------------
    // Soft-delete fields
    // -----------------------------------------------------------------------

    [Column("deleted_by")]
    public long? DeletedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // -----------------------------------------------------------------------
    // Optimistic locking — row_version BIGINT NOT NULL DEFAULT 1
    // -----------------------------------------------------------------------

    [ConcurrencyCheck]
    [Column("row_version")]
    public long RowVersion { get; set; } = 1L;

    /// <summary>
    /// Guard against legacy zero/NULL <c>row_version</c> rows so the optimistic-lock WHERE
    /// clause never targets version 0. Invoked on materialisation and before persist by the
    /// auditing interceptor (the .NET counterpart of JPA's <c>@PostLoad</c>/<c>@PrePersist</c>).
    /// </summary>
    public void InitVersion()
    {
        if (RowVersion <= 0) RowVersion = 1L;
    }

    // -----------------------------------------------------------------------
    // Soft-delete helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Marks this entity soft-deleted: <c>is_deleted=1</c>, records actor + timestamp, and
    /// invokes <see cref="NullifyBusinessId"/> so unique constraint slots are freed.
    /// </summary>
    /// <remarks>Non-virtual by design — mirrors the <c>final</c> method in the Java source.</remarks>
    public void PerformSoftDelete(long? userNo)
    {
        IsActive = 0;
        IsDeleted = 1;
        DeletedBy = userNo;
        DeletedAt = DateTime.UtcNow;
        NullifyBusinessId();
    }

    /// <summary>
    /// Default no-op so junction tables don't need a body. Aggregates with unique business
    /// codes override this to clear those fields (e.g. <c>CompanyId = null</c>).
    /// </summary>
    protected virtual void NullifyBusinessId() { }

    // -----------------------------------------------------------------------
    // Backward-compat shims for legacy callers (not mapped to columns)
    // -----------------------------------------------------------------------

    /// <summary>Legacy alias for <see cref="IsActive"/>; the schema column is <c>is_active</c>.</summary>
    [Obsolete("Use IsActive; the schema column is is_active.")]
    [NotMapped]
    public short ActiveStatus
    {
        get => IsActive;
        set => IsActive = value;
    }
}

/// <summary>
/// Multi-tenant base for every company/branch-scoped entity. Adds <c>company_no</c> +
/// <c>branch_no</c>; the tenant predicate (SYS re-plan §3.4 — the "cardinal rule") is applied
/// as an EF Core global query filter registered for every <see cref="BaseEntity"/> descendant
/// and evaluated per request from the JWT-derived tenant context.
///
/// <para>The condition keeps rows of the active company, and — unless the session is
/// COMPANY/GLOBAL scoped — only the active branch (or branch-agnostic rows where
/// <c>branch_no IS NULL</c>, e.g. a central warehouse). A forgotten activation yields zero rows
/// rather than a cross-tenant leak.</para>
/// </summary>
public abstract class BaseEntity : AuditEntity, IMultiTenantEntity
{
    [Column("company_no")]
    public long? CompanyNo { get; set; }

    [Column("branch_no")]
    public long? BranchNo { get; set; }
}

public interface IAuditEntity
{
    short IsActive { get; set; }
    short IsDeleted { get; set; }
    long? CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    long? UpdatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    long? DeletedBy { get; set; }
    DateTime? DeletedAt { get; set; }
    long RowVersion { get; set; }
}

public interface ISoftDelete
{
    short IsDeleted { get; set; }
    long? DeletedBy { get; set; }
    DateTime? DeletedAt { get; set; }
}

public interface IMultiTenantEntity
{
    long? CompanyNo { get; set; }
    long? BranchNo { get; set; }
}

public interface ICompanyScopedEntity
{
    long? CompanyNo { get; set; }
}

public interface IBranchScopedEntity : IMultiTenantEntity
{
}
