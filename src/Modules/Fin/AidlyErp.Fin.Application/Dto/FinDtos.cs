using System.Text.Json.Serialization;

namespace AidlyErp.Fin.Application.Dto;

public class Fin1001AccountDto
{
    [JsonPropertyName("account_no")]
    public long? AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("account_group_no")]
    public long AccountGroupNo { get; set; }

    [JsonPropertyName("root_type")]
    public short? RootType { get; set; }

    [JsonPropertyName("normal_balance")]
    public string? NormalBalance { get; set; }

    [JsonPropertyName("is_postable")]
    public short IsPostable { get; set; } = 1;

    [JsonPropertyName("control_type")]
    public short? ControlType { get; set; }

    [JsonPropertyName("requires_cost_center")]
    public short RequiresCostCenter { get; set; } = 0;

    [JsonPropertyName("requires_party")]
    public short RequiresParty { get; set; } = 0;

    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("opening_dr_cr")]
    public string? OpeningDrCr { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("account_group_name")]
    public string? AccountGroupName { get; set; }
}

public class Fin1002AccountGroupDto
{
    [JsonPropertyName("account_group_no")]
    public long? AccountGroupNo { get; set; }

    [JsonPropertyName("group_code")]
    public string? GroupCode { get; set; }

    [JsonPropertyName("group_name")]
    public string? GroupName { get; set; }

    [JsonPropertyName("parent_group_no")]
    public long? ParentGroupNo { get; set; }

    [JsonPropertyName("root_type")]
    public short RootType { get; set; }

    [JsonPropertyName("normal_balance")]
    public string? NormalBalance { get; set; }

    [JsonPropertyName("display_order")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("parent_group_name")]
    public string? ParentGroupName { get; set; }
}

public class Fin1003VoucherTypeDto
{
    [JsonPropertyName("voucher_type_no")]
    public long? VoucherTypeNo { get; set; }

    [JsonPropertyName("voucher_type_code")]
    public string? VoucherTypeCode { get; set; }

    [JsonPropertyName("voucher_type_name")]
    public string? VoucherTypeName { get; set; }

    [JsonPropertyName("base_kind")]
    public short BaseKind { get; set; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; set; }

    [JsonPropertyName("requires_approval")]
    public short RequiresApproval { get; set; } = 0;

    [JsonPropertyName("is_auto_numbered")]
    public short IsAutoNumbered { get; set; } = 1;

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }
}

public class Fin1004AccountDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("normal_balance")]
    public string? NormalBalance { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("opening_dr_cr")]
    public string? OpeningDrCr { get; set; }
}

public class Fin1004OpeningLineDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("opening_dr_cr")]
    public string OpeningDrCr { get; set; } = "dr";
}

public class Fin1004OpeningBalanceDto
{
    [JsonPropertyName("as_of_date")]
    public DateTime AsOfDate { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("lines")]
    public List<Fin1004OpeningLineDto> Lines { get; set; } = new();
}

public class Fin1004ResultDto
{
    [JsonPropertyName("voucher_no")]
    public long VoucherNo { get; set; }

    [JsonPropertyName("voucher_id")]
    public string? VoucherId { get; set; }

    [JsonPropertyName("total_debit")]
    public decimal TotalDebit { get; set; }

    [JsonPropertyName("total_credit")]
    public decimal TotalCredit { get; set; }
}

public class Fin1005BankAccountDto
{
    [JsonPropertyName("bank_account_no")]
    public long? BankAccountNo { get; set; }

    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("account_number")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("swift_code")]
    public string? SwiftCode { get; set; }

    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("gl_account_code")]
    public string? GlAccountCode { get; set; }

    [JsonPropertyName("gl_account_name")]
    public string? GlAccountName { get; set; }
}

public class Fin1006GlMapDto
{
    [JsonPropertyName("map_no")]
    public long? MapNo { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("leg_key")]
    public string? LegKey { get; set; }

    [JsonPropertyName("sub_key")]
    public string? SubKey { get; set; }

    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }
}

public class Fin1101VoucherLineDto
{
    [JsonPropertyName("voucher_dtl_no")]
    public long? VoucherDtlNo { get; set; }

    [JsonPropertyName("line_no")]
    public int LineNo { get; set; }

    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("dr_cr")]
    public string DrCr { get; set; } = "dr";

    [JsonPropertyName("debit_fc")]
    public decimal DebitFc { get; set; }

    [JsonPropertyName("credit_fc")]
    public decimal CreditFc { get; set; }

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("cost_center_no")]
    public long? CostCenterNo { get; set; }

    [JsonPropertyName("party_type")]
    public short? PartyType { get; set; }

    [JsonPropertyName("party_no")]
    public long? PartyNo { get; set; }

    [JsonPropertyName("against_voucher_no")]
    public long? AgainstVoucherNo { get; set; }

    [JsonPropertyName("line_narration")]
    public string? LineNarration { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }
}

public class Fin1101VoucherDto
{
    [JsonPropertyName("voucher_no")]
    public long? VoucherNo { get; set; }

    [JsonPropertyName("voucher_id")]
    public string? VoucherId { get; set; }

    [JsonPropertyName("voucher_type_no")]
    public long VoucherTypeNo { get; set; }

    [JsonPropertyName("voucher_date")]
    public DateTime VoucherDate { get; set; }

    [JsonPropertyName("fin_year_no")]
    public long? FinYearNo { get; set; }

    [JsonPropertyName("fin_period_no")]
    public long? FinPeriodNo { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("reference_no")]
    public string? ReferenceNo { get; set; }

    [JsonPropertyName("currency_no")]
    public long? CurrencyNo { get; set; }

    [JsonPropertyName("fx_rate")]
    public decimal FxRate { get; set; } = 1.0m;

    [JsonPropertyName("total_debit")]
    public decimal TotalDebit { get; set; }

    [JsonPropertyName("total_credit")]
    public decimal TotalCredit { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Posted 3=Cancelled

    [JsonPropertyName("source_module")]
    public short? SourceModule { get; set; }

    [JsonPropertyName("source_doc_type")]
    public string? SourceDocType { get; set; }

    [JsonPropertyName("source_doc_no")]
    public long? SourceDocNo { get; set; }

    [JsonPropertyName("reversal_of_no")]
    public long? ReversalOfNo { get; set; }

    [JsonPropertyName("approval_request_no")]
    public long? ApprovalRequestNo { get; set; }

    [JsonPropertyName("approved_by")]
    public long? ApprovedBy { get; set; }

    [JsonPropertyName("posted_at")]
    public DateTime? PostedAt { get; set; }

    [JsonPropertyName("posted_by")]
    public long? PostedBy { get; set; }

    [JsonPropertyName("branch_no")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("is_active")]
    public short IsActive { get; set; } = 1;

    [JsonPropertyName("row_version")]
    public long? RowVersion { get; set; }

    [JsonPropertyName("base_kind")]
    public short? BaseKind { get; set; }

    [JsonPropertyName("voucher_type_name")]
    public string? VoucherTypeName { get; set; }

    [JsonPropertyName("lines")]
    public List<Fin1101VoucherLineDto> Lines { get; set; } = new();
}

public class Fin1102BankAccountDto
{
    [JsonPropertyName("bank_account_no")]
    public long BankAccountNo { get; set; }

    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("bank_name")]
    public string? BankName { get; set; }

    [JsonPropertyName("account_number")]
    public string? AccountNumber { get; set; }
}

public class Fin1102LedgerLineDto
{
    [JsonPropertyName("ledger_no")]
    public long LedgerNo { get; set; }

    [JsonPropertyName("voucher_no")]
    public long VoucherNo { get; set; }

    [JsonPropertyName("voucher_id")]
    public string? VoucherId { get; set; }

    [JsonPropertyName("txn_date")]
    public DateTime TxnDate { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("cleared_date")]
    public DateTime? ClearedDate { get; set; }

    [JsonPropertyName("is_cleared")]
    public short IsCleared { get; set; } = 0;
}

public class Fin1102WorksheetDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("statement_date")]
    public DateTime StatementDate { get; set; }

    [JsonPropertyName("book_balance")]
    public decimal BookBalance { get; set; }

    [JsonPropertyName("lines")]
    public List<Fin1102LedgerLineDto> Lines { get; set; } = new();
}

public class Fin1102ReconDto
{
    [JsonPropertyName("recon_no")]
    public long? ReconNo { get; set; }

    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("statement_date")]
    public DateTime StatementDate { get; set; }

    [JsonPropertyName("statement_balance")]
    public decimal StatementBalance { get; set; }

    [JsonPropertyName("book_balance")]
    public decimal BookBalance { get; set; }

    [JsonPropertyName("cleared_debits")]
    public decimal ClearedDebits { get; set; }

    [JsonPropertyName("cleared_credits")]
    public decimal ClearedCredits { get; set; }

    [JsonPropertyName("reconciled_balance")]
    public decimal ReconciledBalance { get; set; }

    [JsonPropertyName("difference")]
    public decimal Difference { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; } = 1; // 1=Draft 2=Completed

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class Fin1102SaveDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("statement_date")]
    public DateTime StatementDate { get; set; }

    [JsonPropertyName("statement_balance")]
    public decimal StatementBalance { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("cleared_ledger_nos")]
    public List<long> ClearedLedgerNos { get; set; } = new();
}

public class Fin1201EventDto
{
    [JsonPropertyName("event_no")]
    public long EventNo { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("aggregate_type")]
    public string? AggregateType { get; set; }

    [JsonPropertyName("aggregate_id")]
    public string? AggregateId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("status")]
    public short Status { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}

public class FinStatementRowDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("root_type")]
    public short RootType { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("closing_balance")]
    public decimal ClosingBalance { get; set; }
}

public class Fin1301TrialBalanceRowDto : FinStatementRowDto { }

public class Fin1302LedgerRowDto
{
    [JsonPropertyName("ledger_no")]
    public long LedgerNo { get; set; }

    [JsonPropertyName("voucher_no")]
    public long VoucherNo { get; set; }

    [JsonPropertyName("voucher_id")]
    public string? VoucherId { get; set; }

    [JsonPropertyName("txn_date")]
    public DateTime TxnDate { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("running_balance")]
    public decimal RunningBalance { get; set; }
}

public class Fin1302LedgerDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("total_debit")]
    public decimal TotalDebit { get; set; }

    [JsonPropertyName("total_credit")]
    public decimal TotalCredit { get; set; }

    [JsonPropertyName("closing_balance")]
    public decimal ClosingBalance { get; set; }

    [JsonPropertyName("rows")]
    public List<Fin1302LedgerRowDto> Rows { get; set; } = new();
}

public class Fin1303DayBookRowDto
{
    [JsonPropertyName("voucher_no")]
    public long VoucherNo { get; set; }

    [JsonPropertyName("voucher_id")]
    public string? VoucherId { get; set; }

    [JsonPropertyName("voucher_type_name")]
    public string? VoucherTypeName { get; set; }

    [JsonPropertyName("voucher_date")]
    public DateTime VoucherDate { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }
}

public class Fin1304PnlDto
{
    [JsonPropertyName("revenue_rows")]
    public List<FinStatementRowDto> RevenueRows { get; set; } = new();

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("expense_rows")]
    public List<FinStatementRowDto> ExpenseRows { get; set; } = new();

    [JsonPropertyName("total_expense")]
    public decimal TotalExpense { get; set; }

    [JsonPropertyName("net_profit")]
    public decimal NetProfit { get; set; }
}

public class Fin1305BalanceSheetDto
{
    [JsonPropertyName("asset_rows")]
    public List<FinStatementRowDto> AssetRows { get; set; } = new();

    [JsonPropertyName("total_assets")]
    public decimal TotalAssets { get; set; }

    [JsonPropertyName("liability_rows")]
    public List<FinStatementRowDto> LiabilityRows { get; set; } = new();

    [JsonPropertyName("total_liabilities")]
    public decimal TotalLiabilities { get; set; }

    [JsonPropertyName("equity_rows")]
    public List<FinStatementRowDto> EquityRows { get; set; } = new();

    [JsonPropertyName("total_equity")]
    public decimal TotalEquity { get; set; }

    [JsonPropertyName("retained_earnings")]
    public decimal RetainedEarnings { get; set; }
}

/// <summary>One cash/bank account line of the cash-flow statement.</summary>
public class Fin1306CashFlowRowDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("control_type")]
    public short? ControlType { get; set; } // 3=Bank 4=Cash

    [JsonPropertyName("opening")]
    public decimal Opening { get; set; }

    [JsonPropertyName("receipts")]
    public decimal Receipts { get; set; } // debits into the cash account

    [JsonPropertyName("payments")]
    public decimal Payments { get; set; } // credits out of the cash account

    [JsonPropertyName("closing")]
    public decimal Closing { get; set; }
}

/// <summary>Cash Flow (FIN_1306) — direct method over cash &amp; bank GL accounts for a date window.</summary>
public class Fin1306CashFlowDto
{
    [JsonPropertyName("from_date")]
    public DateTime FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime ToDate { get; set; }

    [JsonPropertyName("opening_cash")]
    public decimal OpeningCash { get; set; }

    [JsonPropertyName("total_receipts")]
    public decimal TotalReceipts { get; set; }

    [JsonPropertyName("total_payments")]
    public decimal TotalPayments { get; set; }

    [JsonPropertyName("net_cash_flow")]
    public decimal NetCashFlow { get; set; }

    [JsonPropertyName("closing_cash")]
    public decimal ClosingCash { get; set; }

    [JsonPropertyName("rows")]
    public List<Fin1306CashFlowRowDto> Rows { get; set; } = new();
}

public class Fin1307AgingRowDto
{
    [JsonPropertyName("party_no")]
    public long PartyNo { get; set; }

    [JsonPropertyName("party_name")]
    public string? PartyName { get; set; }

    [JsonPropertyName("current_amount")]
    public decimal CurrentAmount { get; set; } // 0-30 days

    [JsonPropertyName("days_31_60")]
    public decimal Days3160 { get; set; }

    [JsonPropertyName("days_61_90")]
    public decimal Days6190 { get; set; }

    [JsonPropertyName("days_over_90")]
    public decimal DaysOver90 { get; set; }

    [JsonPropertyName("total_outstanding")]
    public decimal TotalOutstanding { get; set; }
}

public class Fin1307AgingDto
{
    [JsonPropertyName("as_of_date")]
    public DateTime AsOfDate { get; set; }

    [JsonPropertyName("party_type")]
    public short PartyType { get; set; } // 1=Customer (AR), 2=Supplier (AP)

    [JsonPropertyName("rows")]
    public List<Fin1307AgingRowDto> Rows { get; set; } = new();

    [JsonPropertyName("grand_total")]
    public decimal GrandTotal { get; set; }
}

public class Fin1308ChartOfAccountsRowDto
{
    [JsonPropertyName("account_no")]
    public long AccountNo { get; set; }

    [JsonPropertyName("account_code")]
    public string? AccountCode { get; set; }

    [JsonPropertyName("account_name")]
    public string? AccountName { get; set; }

    [JsonPropertyName("group_name")]
    public string? GroupName { get; set; }

    [JsonPropertyName("root_type_name")]
    public string? RootTypeName { get; set; }

    [JsonPropertyName("normal_balance")]
    public string? NormalBalance { get; set; }

    [JsonPropertyName("is_postable")]
    public short IsPostable { get; set; }
}

public class Fin1401PeriodDto
{
    [JsonPropertyName("fin_period_no")]
    public long FinPeriodNo { get; set; }

    [JsonPropertyName("period_name")]
    public string? PeriodName { get; set; }

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("is_closed")]
    public short IsClosed { get; set; }
}

public class Fin1401YearDto
{
    [JsonPropertyName("fin_year_no")]
    public long FinYearNo { get; set; }

    [JsonPropertyName("year_name")]
    public string? YearName { get; set; }

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("is_closed")]
    public short IsClosed { get; set; }

    [JsonPropertyName("periods")]
    public List<Fin1401PeriodDto> Periods { get; set; } = new();
}

public class Fin1401CloseRequestDto
{
    [JsonPropertyName("fin_year_no")]
    public long FinYearNo { get; set; }

    [JsonPropertyName("retained_earnings_account_no")]
    public long RetainedEarningsAccountNo { get; set; }

    [JsonPropertyName("income_summary_account_no")]
    public long IncomeSummaryAccountNo { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }
}

public class Fin1401CloseResultDto
{
    [JsonPropertyName("fin_year_no")]
    public long FinYearNo { get; set; }

    [JsonPropertyName("closing_voucher_no")]
    public long ClosingVoucherNo { get; set; }

    [JsonPropertyName("closing_voucher_id")]
    public string? ClosingVoucherId { get; set; }

    [JsonPropertyName("net_income_transferred")]
    public decimal NetIncomeTransferred { get; set; }
}
