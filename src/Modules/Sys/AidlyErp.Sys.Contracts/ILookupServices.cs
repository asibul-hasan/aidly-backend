namespace AidlyErp.Sys.Contracts;

/// <summary>
/// Read-only lookup for VAT tax codes. Implemented in Sys.Infrastructure, consumed by SAL/PUR modules.
/// </summary>
public interface IVatTaxLookup
{
    Task<Dictionary<long, string>> GetTaxCodesAsync(IEnumerable<long> vatTaxNos, CancellationToken ct = default);

    /// <summary>
    /// Rate percentages for a set of tax codes. Selling modules price tax from here rather than
    /// trusting a rate sent by the client.
    /// </summary>
    Task<Dictionary<long, decimal>> GetTaxRatesAsync(IEnumerable<long> vatTaxNos, CancellationToken ct = default);
}

/// <summary>
/// Read-only lookup for currency info. Implemented in Sys.Infrastructure, consumed by FIN module.
/// </summary>
public interface ICurrencyLookup
{
    Task<long> GetBaseCurrencyNoAsync(long companyNo, CancellationToken ct = default);
}

/// <summary>
/// Read-only lookup for party names (customers, suppliers, employees). Implemented in Sys.Infrastructure.
/// partyType: 1=Customer 2=Supplier 3=Employee.
/// </summary>
public interface IPartyLookup
{
    Task<Dictionary<long, string>> GetPartyNamesAsync(short partyType, IEnumerable<long> partyNos, CancellationToken ct = default);

    /// <summary>
    /// Display names for <c>sys_user</c> keys. Distinct from <see cref="GetPartyNamesAsync"/>, whose
    /// party type 3 resolves <c>hrm_employee</c>: a cashier is a user, and a user number is not an
    /// employee number, so asking the party lookup for one silently returns the wrong person.
    /// </summary>
    Task<Dictionary<long, string>> GetUserNamesAsync(IEnumerable<long> userNos, CancellationToken ct = default);
}

/// <summary>A selectable option: surrogate key plus its display label.</summary>
public sealed record LookupOption(long No, string Name);

/// <summary>
/// Read-only option lists for inventory master data (warehouses, products, UOMs).
/// Implemented in Sys.Infrastructure, consumed by SAL and PUR whose own DbContexts do
/// not carry the INV entity sets. Mirrors the IPartyLookup pattern so no module ever
/// references another module's Domain project.
/// </summary>
public interface IInvLookup
{
    Task<List<LookupOption>> GetWarehousesAsync(long companyNo, long? branchNo, CancellationToken ct = default);
    Task<List<LookupOption>> GetProductsAsync(long companyNo, CancellationToken ct = default);
    Task<List<LookupOption>> GetUomsAsync(long companyNo, CancellationToken ct = default);

    /// <summary>Product categories — used where a rule is scoped to a whole category.</summary>
    Task<List<LookupOption>> GetCategoriesAsync(long companyNo, CancellationToken ct = default);

    /// <summary>Brands — used where a rule is scoped to a whole brand.</summary>
    Task<List<LookupOption>> GetBrandsAsync(long companyNo, CancellationToken ct = default);
}
