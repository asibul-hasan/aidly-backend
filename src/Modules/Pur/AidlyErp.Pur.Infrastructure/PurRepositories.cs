using Microsoft.EntityFrameworkCore;
using AidlyErp.Pur.Application.Interfaces;
using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Infrastructure.Repositories;

// ═══════════════════════════════════════════════════════════════════════════
// PUR Repository Implementations — EF Core translations of Spring Data repos
// ═══════════════════════════════════════════════════════════════════════════

public class PurSupplierRepository : IPurSupplierRepository
{
    private readonly IPurDbContext _db;
    public PurSupplierRepository(IPurDbContext db) => _db = db;

    public async Task<List<PurSupplier>> FindByCompanyAsync(long companyNo, CancellationToken ct = default) =>
        await _db.PurSuppliers.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.IsDeleted == 0).OrderBy(x => x.SupplierNo).ToListAsync(ct);

    public async Task<PurSupplier?> FindByIdAsync(long supplierNo, CancellationToken ct = default) =>
        await _db.PurSuppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.IsDeleted == 0, ct);

    public async Task<PurSupplier?> FindByIdAndCompanyAsync(long supplierNo, long companyNo, CancellationToken ct = default) =>
        await _db.PurSuppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierNo == supplierNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsBySupplierIdAndCompanyAsync(string supplierId, long companyNo, long? excludeNo = null, CancellationToken ct = default) =>
        await _db.PurSuppliers.AnyAsync(x => x.SupplierId == supplierId && x.CompanyNo == companyNo && x.IsDeleted == 0
            && (!excludeNo.HasValue || x.SupplierNo != excludeNo.Value), ct);

    public async Task<bool> ExistsByMobileAndCompanyAsync(string mobileNo, long companyNo, long? excludeNo = null, CancellationToken ct = default) =>
        await _db.PurSuppliers.AnyAsync(x => x.MobileNo == mobileNo && x.CompanyNo == companyNo && x.IsDeleted == 0
            && (!excludeNo.HasValue || x.SupplierNo != excludeNo.Value), ct);

    public void Add(PurSupplier entity) => _db.PurSuppliers.Add(entity);
    public void Update(PurSupplier entity) => _db.PurSuppliers.Update(entity);
}

public class PurInvoiceRepository : IPurInvoiceRepository
{
    private readonly IPurDbContext _db;
    public PurInvoiceRepository(IPurDbContext db) => _db = db;

    public async Task<List<PurInvoice>> FindByCompanyAndBranchAsync(long companyNo, long branchNo, CancellationToken ct = default) =>
        await _db.PurInvoices.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.BranchNo == branchNo && x.IsDeleted == 0).OrderByDescending(x => x.InvoiceNo).ToListAsync(ct);

    public async Task<PurInvoice?> FindByIdAsync(long invoiceNo, CancellationToken ct = default) =>
        await _db.PurInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo && x.IsDeleted == 0, ct);

    public async Task<PurInvoice?> FindByIdAndCompanyAsync(long invoiceNo, long companyNo, CancellationToken ct = default) =>
        await _db.PurInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public async Task<bool> ExistsBySupplierBillAsync(long supplierNo, string supplierInvoiceNo, long? excludeNo = null, CancellationToken ct = default) =>
        await _db.PurInvoices.AnyAsync(x => x.SupplierNo == supplierNo && x.SupplierInvoiceNo == supplierInvoiceNo && x.IsDeleted == 0
            && (!excludeNo.HasValue || x.InvoiceNo != excludeNo.Value), ct);

    public void Add(PurInvoice entity) => _db.PurInvoices.Add(entity);
    public void Update(PurInvoice entity) => _db.PurInvoices.Update(entity);
}

public class PurPaymentRepository : IPurPaymentRepository
{
    private readonly IPurDbContext _db;
    public PurPaymentRepository(IPurDbContext db) => _db = db;

    public async Task<List<PurPayment>> FindByCompanyAndBranchAsync(long companyNo, long branchNo, CancellationToken ct = default) =>
        await _db.PurPayments.AsNoTracking().Where(x => x.CompanyNo == companyNo && x.BranchNo == branchNo && x.IsDeleted == 0).OrderByDescending(x => x.PaymentNo).ToListAsync(ct);

    public async Task<PurPayment?> FindByIdAsync(long paymentNo, CancellationToken ct = default) =>
        await _db.PurPayments.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentNo == paymentNo && x.IsDeleted == 0, ct);

    public async Task<PurPayment?> FindByIdAndCompanyAsync(long paymentNo, long companyNo, CancellationToken ct = default) =>
        await _db.PurPayments.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentNo == paymentNo && x.CompanyNo == companyNo && x.IsDeleted == 0, ct);

    public void Add(PurPayment entity) => _db.PurPayments.Add(entity);
    public void Update(PurPayment entity) => _db.PurPayments.Update(entity);
}

public class PurSupplierLedgerRepository : IPurSupplierLedgerRepository
{
    private readonly IPurDbContext _db;
    public PurSupplierLedgerRepository(IPurDbContext db) => _db = db;

    public async Task<List<PurSupplierLedger>> FindBySupplierAsync(long supplierNo, long companyNo, CancellationToken ct = default) =>
        await _db.PurSupplierLedgers.AsNoTracking()
            .Where(x => x.SupplierNo == supplierNo && x.CompanyNo == companyNo && x.IsDeleted == 0)
            .OrderBy(x => x.SupplierLedgerNo).ToListAsync(ct);

    public void Add(PurSupplierLedger entity) => _db.PurSupplierLedgers.Add(entity);
}
