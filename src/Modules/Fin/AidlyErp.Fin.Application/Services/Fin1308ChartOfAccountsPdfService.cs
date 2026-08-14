using AidlyErp.Fin.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AidlyErp.Shared.Core.Exceptions;
using AidlyErp.Shared.Core.Abstractions;
using AidlyErp.Shared.Contracts;
using AidlyErp.Sys.Contracts;
using AidlyErp.Shared.Core.Security;
using AidlyErp.Fin.Application.Dto;
using AidlyErp.Fin.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;

namespace AidlyErp.Fin.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Fin1308ChartOfAccountsPdfService — COA Export (PDF + Excel)
// Port of Java Fin1308ChartOfAccountsPdfService.java (235 LOC)
// ═══════════════════════════════════════════════════════════════════════════

public interface IFin1308ChartOfAccountsPdfService
{
    Task<List<Fin1308ChartOfAccountsRowDto>> GetCoaExportRowsAsync(CancellationToken ct = default);
    Task<byte[]> ExportPdfAsync(CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(CancellationToken ct = default);
}

public class Fin1308ChartOfAccountsPdfService : IFin1308ChartOfAccountsPdfService
{
    private readonly IFinDbContext _db;
    private readonly ICompanyBranchContext _ctx;

    // Category display order matching Java
    private static readonly string[] CategoryOrder =
    [
        "CAPITAL", "RETAINED EARNING", "FIXED ASSETS", "CURRENT ASSETS",
        "CURRENT LIABILITIES", "SALES", "SALES ADJUSTMENTS",
        "COST OF GOODS SOLD", "OTHER EXPENSES", "OTHER INCOME", "OTHER"
    ];

    public Fin1308ChartOfAccountsPdfService(IFinDbContext db, ICompanyBranchContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<List<Fin1308ChartOfAccountsRowDto>> GetCoaExportRowsAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var groups = await _db.FinAccountGroups.AsNoTracking()
            .Where(g => g.CompanyNo == companyNo && g.IsDeleted == 0)
            .ToDictionaryAsync(g => g.AccountGroupNo, g => g.GroupName, ct);

        return await _db.FinAccounts
            .AsNoTracking()
            .Where(a => a.CompanyNo == companyNo && a.IsDeleted == 0)
            .OrderBy(a => a.AccountCode)
            .Select(a => new Fin1308ChartOfAccountsRowDto
            {
                AccountNo = a.AccountNo,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                GroupName = groups.ContainsKey(a.AccountGroupNo) ? groups[a.AccountGroupNo] : null,
                RootTypeName = RootTypeName(a.RootType),
                NormalBalance = a.NormalBalance,
                IsPostable = a.IsPostable
            })
            .ToListAsync(ct);
    }

    public async Task<byte[]> ExportPdfAsync(CancellationToken ct = default)
    {
        long companyNo = _ctx.CurrentCompanyNo() ?? throw new ValidationException("Company context is required");
        var rows = await GetCoaExportRowsAsync(ct);

        // Group by category
        var grouped = rows
            .GroupBy(r => ResolveCategory(r.AccountCode ?? "", r.RootTypeName ?? "Other"))
            .OrderBy(g => Array.IndexOf(CategoryOrder, g.Key) >= 0 ? Array.IndexOf(CategoryOrder, g.Key) : 99)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(36);

                page.Header().Text("Chart of Accounts").FontSize(18).SemiBold().AlignCenter();
                page.Header().Text($"Company: {companyNo}").FontSize(10).AlignCenter();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Code
                        columns.RelativeColumn(4); // Name
                        columns.RelativeColumn(2); // Group
                        columns.RelativeColumn(1); // Type
                        columns.RelativeColumn(1); // Balance
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Code").FontSize(9).SemiBold();
                        header.Cell().Text("Account Name").FontSize(9).SemiBold();
                        header.Cell().Text("Group").FontSize(9).SemiBold();
                        header.Cell().Text("Type").FontSize(9).SemiBold();
                        header.Cell().Text("Balance").FontSize(9).SemiBold();
                    });

                    foreach (var category in grouped)
                    {
                        // Category header row
                        table.Cell().ColumnSpan(5)
                            .Text(category.Key).FontSize(10).SemiBold()
                            .FontColor(Colors.Blue.Darken2);

                        foreach (var row in category.OrderBy(r => r.AccountCode))
                        {
                            table.Cell().Text(row.AccountCode ?? "").FontSize(8);
                            table.Cell().Text(row.AccountName ?? "").FontSize(8);
                            table.Cell().Text(row.GroupName ?? "").FontSize(8);
                            table.Cell().Text(row.IsPostable == 1 ? "Postable" : "Header").FontSize(8);
                            table.Cell().Text(row.NormalBalance ?? "").FontSize(8);
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Generated: ").FontSize(8);
                        text.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8);
                        text.Span(" | Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                    });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportExcelAsync(CancellationToken ct = default)
    {
        var rows = await GetCoaExportRowsAsync(ct);

        var grouped = rows
            .GroupBy(r => ResolveCategory(r.AccountCode ?? "", r.RootTypeName ?? "Other"))
            .OrderBy(g => Array.IndexOf(CategoryOrder, g.Key) >= 0 ? Array.IndexOf(CategoryOrder, g.Key) : 99)
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Chart of Accounts");

        // Headers
        worksheet.Cell(1, 1).Value = "Code";
        worksheet.Cell(1, 2).Value = "Account Name";
        worksheet.Cell(1, 3).Value = "Group";
        worksheet.Cell(1, 4).Value = "Root Type";
        worksheet.Cell(1, 5).Value = "Type";
        worksheet.Cell(1, 6).Value = "Normal Balance";
        worksheet.Range(1, 1, 1, 6).Style.Font.Bold = true;

        int row = 2;
        foreach (var category in grouped)
        {
            worksheet.Cell(row, 1).Value = category.Key;
            worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.DarkBlue;
            row++;

            foreach (var item in category.OrderBy(r => r.AccountCode))
            {
                worksheet.Cell(row, 1).Value = item.AccountCode ?? "";
                worksheet.Cell(row, 2).Value = item.AccountName ?? "";
                worksheet.Cell(row, 3).Value = item.GroupName ?? "";
                worksheet.Cell(row, 4).Value = item.RootTypeName ?? "";
                worksheet.Cell(row, 5).Value = item.IsPostable == 1 ? "Postable" : "Header";
                worksheet.Cell(row, 6).Value = item.NormalBalance ?? "";
                row++;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string ResolveCategory(string code, string rootTypeName)
    {
        if (string.IsNullOrEmpty(code) || code.Length < 3) return "OTHER";

        if (code.StartsWith("100"))
        {
            if (code.Contains("9000") || code.Contains("9900")) return "RETAINED EARNING";
            return "CAPITAL";
        }
        if (code.StartsWith('1')) return "CAPITAL";
        if (code.StartsWith('2')) return "FIXED ASSETS";
        if (code.StartsWith('3')) return "CURRENT ASSETS";
        if (code.StartsWith('4')) return "CURRENT LIABILITIES";
        if (code.StartsWith("50")) return "SALES";
        if (code.StartsWith("51")) return "SALES ADJUSTMENTS";
        if (code.StartsWith('5')) return "SALES";
        if (code.StartsWith('6')) return "COST OF GOODS SOLD";
        if (code.StartsWith('7')) return "OTHER EXPENSES";
        if (code.StartsWith('8')) return "OTHER INCOME";

        return "OTHER";
    }

    private static string RootTypeName(short rootType) => rootType switch
    {
        1 => "Asset",
        2 => "Liability",
        3 => "Equity",
        4 => "Revenue",
        5 => "Expense",
        _ => "Other"
    };
}
