using AidlyErp.Pur.Domain;

namespace AidlyErp.Pur.Application.Interfaces;

// ═══════════════════════════════════════════════════════════════════════════
// PUR Repository Interfaces — port of Spring Data JPA repositories.
// Hot-path abstractions for invoice, payment, supplier, and AP ledger.
// ═══════════════════════════════════════════════════════════════════════════

public interface IPurSupplierRepository
{
    Task<List<PurSupplier>> FindByCompanyAsync(long companyNo, CancellationToken ct = default);
    Task<PurSupplier?> FindByIdAsync(long supplierNo, CancellationToken ct = default);
    Task<PurSupplier?> FindByIdAndCompanyAsync(long supplierNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsBySupplierIdAndCompanyAsync(string supplierId, long companyNo, long? excludeNo = null, CancellationToken ct = default);
    Task<bool> ExistsByMobileAndCompanyAsync(string mobileNo, long companyNo, long? excludeNo = null, CancellationToken ct = default);
    void Add(PurSupplier entity);
    void Update(PurSupplier entity);
}

public interface IPurInvoiceRepository
{
    Task<List<PurInvoice>> FindByCompanyAndBranchAsync(long companyNo, long branchNo, CancellationToken ct = default);
    Task<PurInvoice?> FindByIdAsync(long invoiceNo, CancellationToken ct = default);
    Task<PurInvoice?> FindByIdAndCompanyAsync(long invoiceNo, long companyNo, CancellationToken ct = default);
    Task<bool> ExistsBySupplierBillAsync(long supplierNo, string supplierInvoiceNo, long? excludeNo = null, CancellationToken ct = default);
    void Add(PurInvoice entity);
    void Update(PurInvoice entity);
}

public interface IPurPaymentRepository
{
    Task<List<PurPayment>> FindByCompanyAndBranchAsync(long companyNo, long branchNo, CancellationToken ct = default);
    Task<PurPayment?> FindByIdAsync(long paymentNo, CancellationToken ct = default);
    Task<PurPayment?> FindByIdAndCompanyAsync(long paymentNo, long companyNo, CancellationToken ct = default);
    void Add(PurPayment entity);
    void Update(PurPayment entity);
}

public interface IPurSupplierLedgerRepository
{
    Task<List<PurSupplierLedger>> FindBySupplierAsync(long supplierNo, long companyNo, CancellationToken ct = default);
    void Add(PurSupplierLedger entity);
}
