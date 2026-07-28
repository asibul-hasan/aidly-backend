using System.Buffers.Binary;
using System.Security.Cryptography;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Sys.Application.Interfaces;
using AidlyErp.Hrm.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Sys.Application.Dto;
using AidlyErp.Sys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AidlyErp.Sys.Application.Services;

/// <summary>An uploaded file, decoupled from any web framework type.</summary>
public sealed record UploadedFile(string? FileName, string? ContentType, byte[] Bytes)
{
    public long Size => Bytes.LongLength;

    public bool IsEmpty => Bytes.Length == 0;
}

/// <summary>Carrier for the content endpoint — kept out of any DTO so bytes never reach JSON.</summary>
public sealed record FileContent(byte[] Bytes, string? ContentType, string? FileName);

public interface ISys1203Service
{
    Task<List<Sys1203FileDto>> GetFilesAsync(short? entityType, long? entityNo,
                                             CancellationToken cancellationToken = default);

    Task<Sys1203FileDto> UploadAsync(UploadedFile file, short? entityType, long? entityNo, short? isPrimary,
                                     CancellationToken cancellationToken = default);

    Task<FileContent> GetContentAsync(long fileNo, CancellationToken cancellationToken = default);

    Task DeleteAsync(long fileNo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces the primary file for an entity: soft-deletes every existing live file
    /// for the slot, then uploads the new one as primary. Intended for single-primary slots like a
    /// company logo or employee photo — the caller never needs to delete first.
    /// </summary>
    Task<Sys1203FileDto> ReplaceForEntityAsync(short? entityType, long? entityNo, UploadedFile file,
                                               CancellationToken cancellationToken = default);
}

/// <summary>
/// SYS1203 File/Image Manager (<c>sys_file</c>) — upload, list (metadata only), stream content,
/// and soft-delete binaries (company logo, employee photo/signature, etc.), scoped to the active
/// company. Files are stored in the DB (<c>storage_type = 1</c>, BYTEA).
/// </summary>
public class Sys1203Service : ISys1203Service
{
    private const short Deleted = 0;

    /// <summary>10 MB safety cap.</summary>
    private const long MaxBytes = 10L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<short, string> EntityLabels = new Dictionary<short, string>
    {
        [1] = "Company Logo",
        [2] = "Employee Photo",
        [3] = "Employee Signature",
        [4] = "User Avatar",
        [5] = "Product Image",
        [6] = "Branch Logo",
        [7] = "Document",
        [8] = "Other"
    };

    private readonly ISysDbContext _db;
    private readonly IUnitOfWork<ISysDbContext> _unitOfWork;
    private readonly ICompanyBranchContext _ctx;
    private readonly ILogger<Sys1203Service> _logger;

    public Sys1203Service(ISysDbContext db, IUnitOfWork<ISysDbContext> unitOfWork, ICompanyBranchContext ctx,
                          ILogger<Sys1203Service> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _ctx = ctx;
        _logger = logger;
    }

    public async Task<List<Sys1203FileDto>> GetFilesAsync(short? entityType, long? entityNo,
                                                          CancellationToken cancellationToken = default)
    {
        var rows = await SearchAsync(Company(), entityType, entityNo, cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public Task<Sys1203FileDto> UploadAsync(UploadedFile file, short? entityType, long? entityNo, short? isPrimary,
                                            CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct => await UploadCoreAsync(file, entityType, entityNo, isPrimary, ct),
            cancellationToken);

    private async Task<Sys1203FileDto> UploadCoreAsync(UploadedFile file, short? entityType, long? entityNo,
                                                       short? isPrimary, CancellationToken ct)
    {
        if (file == null || file.IsEmpty) throw new ValidationException("No file provided");

        if (entityType == null || !EntityLabels.ContainsKey(entityType.Value))
        {
            throw new ValidationException("Invalid file category");
        }

        if (file.Size > MaxBytes)
        {
            throw new ValidationException("File exceeds the 10 MB limit");
        }

        var companyNo = Company();
        var bytes = file.Bytes;

        var e = new SysFile
        {
            CompanyNo = companyNo,
            EntityType = entityType.Value,
            EntityNo = entityNo,
            FileName = SafeName(file.FileName),
            ContentType = file.ContentType ?? "application/octet-stream",
            FileExtension = ExtensionOf(file.FileName),
            FileSize = bytes.LongLength,
            StorageType = 1,
            FileBytes = bytes,
            ChecksumSha256 = Sha256(bytes),
            IsPrimary = isPrimary ?? 1
        };

        ApplyImageDimensions(e, bytes);

        _db.SysFiles.Add(e);
        await _db.SaveChangesAsync(ct);

        // Exactly one primary per slot — demote any previous holder.
        if (e.IsPrimary == 1)
        {
            var others = await _db.SysFiles
                .Where(f => f.CompanyNo == companyNo && f.EntityType == entityType
                            && f.EntityNo == entityNo && f.FileNo != e.FileNo
                            && f.IsPrimary == 1 && f.IsDeleted == Deleted)
                .ToListAsync(ct);

            foreach (var other in others) other.IsPrimary = 0;

            if (others.Count > 0) await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("SYS1203: uploaded file {FileNo} ({Size} bytes, type={EntityType}) for companyNo={CompanyNo}",
            e.FileNo, e.FileSize, entityType, companyNo);

        return ToDto(e);
    }

    /// <summary>
    /// Stream file bytes — always scoped to the authenticated user's company. A user in company A
    /// cannot read files belonging to company B even if they know the <c>fileNo</c>.
    /// </summary>
    public async Task<FileContent> GetContentAsync(long fileNo, CancellationToken cancellationToken = default)
    {
        var companyNo = Company();

        var e = await _db.SysFiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FileNo == fileNo && f.CompanyNo == companyNo
                                              && f.IsDeleted == Deleted, cancellationToken)
                ?? throw new NotFoundException("File not found");

        if (e.FileBytes == null) throw new NotFoundException("File has no stored content");

        return new FileContent(e.FileBytes, e.ContentType, e.FileName);
    }

    public Task DeleteAsync(long fileNo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var companyNo = Company();

            // Company-scoped: a file outside the caller's company is "not found", not "forbidden".
            var e = await _db.SysFiles
                        .FirstOrDefaultAsync(f => f.FileNo == fileNo && f.CompanyNo == companyNo
                                                  && f.IsDeleted == Deleted, ct)
                    ?? throw new NotFoundException("File not found");

            e.PerformSoftDelete(_ctx.CurrentUserNo());
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task<Sys1203FileDto> ReplaceForEntityAsync(short? entityType, long? entityNo, UploadedFile file,
                                                      CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteAsync(async ct =>
        {
            var companyNo = Company();
            var userNo = _ctx.CurrentUserNo();

            // Soft-delete every existing file for this slot, then upload the replacement.
            var existing = await _db.SysFiles
                .Where(f => f.CompanyNo == companyNo && f.EntityType == entityType
                            && f.EntityNo == entityNo && f.IsDeleted == Deleted)
                .ToListAsync(ct);

            foreach (var old in existing) old.PerformSoftDelete(userNo);

            if (existing.Count > 0) await _db.SaveChangesAsync(ct);

            return await UploadCoreAsync(file, entityType, entityNo, 1, ct);
        }, cancellationToken);

    // ── helpers ───────────────────────────────────────────────────────────────

    private long Company() =>
        _ctx.CompanyNo ?? throw new ValidationException("No active company in context");

    /// <summary>Metadata-only projection — never selects <c>file_bytes</c>.</summary>
    private Task<List<SysFile>> SearchAsync(long companyNo, short? entityType, long? entityNo, CancellationToken ct)
    {
        var query = _db.SysFiles
            .AsNoTracking()
            .Where(f => f.CompanyNo == companyNo && f.IsDeleted == Deleted);

        if (entityType != null) query = query.Where(f => f.EntityType == entityType);
        if (entityNo != null) query = query.Where(f => f.EntityNo == entityNo);

        return query
            .OrderByDescending(f => f.IsPrimary)
            .ThenByDescending(f => f.FileNo)
            .Select(f => new SysFile
            {
                FileNo = f.FileNo,
                CompanyNo = f.CompanyNo,
                EntityType = f.EntityType,
                EntityNo = f.EntityNo,
                FileName = f.FileName,
                ContentType = f.ContentType,
                FileExtension = f.FileExtension,
                FileSize = f.FileSize,
                StorageType = f.StorageType,
                IsPrimary = f.IsPrimary,
                WidthPx = f.WidthPx,
                HeightPx = f.HeightPx,
                ChecksumSha256 = f.ChecksumSha256,
                CreatedAt = f.CreatedAt,
                RowVersion = f.RowVersion
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Best-effort pixel dimensions, read straight from the image header. Java uses
    /// <c>ImageIO.read</c>; parsing the few bytes that matter avoids pulling in an imaging
    /// dependency, and — as in Java — any failure is silently ignored.
    /// </summary>
    private static void ApplyImageDimensions(SysFile e, byte[] bytes)
    {
        if (e.ContentType == null || !e.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            var span = bytes.AsSpan();

            // PNG: 8-byte signature, then IHDR with width/height as big-endian uint32.
            if (span.Length >= 24 && span[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                e.WidthPx = (int)BinaryPrimitives.ReadUInt32BigEndian(span.Slice(16, 4));
                e.HeightPx = (int)BinaryPrimitives.ReadUInt32BigEndian(span.Slice(20, 4));
                return;
            }

            // GIF: "GIF87a"/"GIF89a", then width/height as little-endian uint16.
            if (span.Length >= 10 && span[0] == 'G' && span[1] == 'I' && span[2] == 'F')
            {
                e.WidthPx = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(6, 2));
                e.HeightPx = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8, 2));
                return;
            }

            // JPEG: walk the segment chain to the SOFn frame header that carries the dimensions.
            if (span.Length >= 4 && span[0] == 0xFF && span[1] == 0xD8)
            {
                var i = 2;
                while (i + 9 < span.Length)
                {
                    if (span[i] != 0xFF) { i++; continue; }

                    var marker = span[i + 1];
                    var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(i + 2, 2));

                    // SOF0-SOF15, excluding the non-frame markers DHT (C4), JPG (C8) and DAC (CC).
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        e.HeightPx = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(i + 5, 2));
                        e.WidthPx = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(i + 7, 2));
                        return;
                    }

                    i += 2 + segmentLength;
                }
            }
        }
        catch
        {
            // dimensions are best-effort
        }
    }

    private static string? Sha256(byte[] bytes)
    {
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        catch
        {
            return null;
        }
    }

    private static string SafeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "file";

        var cleaned = name.Replace('\\', '_').Replace('/', '_').Trim();
        return cleaned.Length <= 255 ? cleaned : cleaned[..255];
    }

    private static string? ExtensionOf(string? name)
    {
        if (name == null) return null;

        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1) return null;

        var ext = name[(dot + 1)..].ToLowerInvariant();
        return ext.Length <= 10 ? ext : ext[..10];
    }

    private static Sys1203FileDto ToDto(SysFile e) => new()
    {
        FileNo = e.FileNo,
        EntityType = e.EntityType,
        EntityTypeLabel = EntityLabels.TryGetValue(e.EntityType, out var label) ? label : "Other",
        EntityNo = e.EntityNo,
        FileName = e.FileName,
        ContentType = e.ContentType,
        FileExtension = e.FileExtension,
        FileSize = e.FileSize,
        StorageType = e.StorageType,
        IsPrimary = e.IsPrimary,
        WidthPx = e.WidthPx,
        HeightPx = e.HeightPx,
        ChecksumSha256 = e.ChecksumSha256,
        CreatedAt = e.CreatedAt,
        RowVersion = e.RowVersion,
        ServePath = ServePath(e.FileNo)
    };

    /// <summary>Canonical relative URL to stream this file's bytes.</summary>
    private static string ServePath(long fileNo) => $"/api/v1/sys/files/{fileNo}/content";
}
