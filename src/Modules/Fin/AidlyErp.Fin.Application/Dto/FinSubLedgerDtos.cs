using System.Text.Json.Serialization;

namespace AidlyErp.Fin.Application.Dto;

// ═══════════════════════════════════════════════════════════════════════════
// FIN_1202 (AP) / FIN_1203 (AR) — sub-ledger vs GL control reconciliation
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>One party's position, from both sides of the books.</summary>
public class FinSubLedgerPartyDto
{
    [JsonPropertyName("party_no")]
    public long PartyNo { get; set; }

    [JsonPropertyName("party_name")]
    public string PartyName { get; set; } = string.Empty;

    /// <summary>What PUR/SAL says the party owes (or is owed).</summary>
    [JsonPropertyName("subledger_balance")]
    public decimal SubLedgerBalance { get; set; }

    /// <summary>What the GL control account says, from legs tagged with this party.</summary>
    [JsonPropertyName("gl_balance")]
    public decimal GlBalance { get; set; }

    /// <summary>Sub-ledger minus GL. Non-zero means the two sides disagree about this party.</summary>
    [JsonPropertyName("difference")]
    public decimal Difference { get; set; }
}

public class FinSubLedgerReconDto
{
    [JsonPropertyName("as_of_date")]
    public DateTime AsOfDate { get; set; }

    /// <summary>1=Customer (AR, FIN_1203), 2=Supplier (AP, FIN_1202).</summary>
    [JsonPropertyName("party_type")]
    public short PartyType { get; set; }

    [JsonPropertyName("subledger_total")]
    public decimal SubLedgerTotal { get; set; }

    /// <summary>Every leg on the control accounts, whether or not it names a party.</summary>
    [JsonPropertyName("gl_control_total")]
    public decimal GlControlTotal { get; set; }

    /// <summary>
    /// Control-account movement that names no party — usually a manual journal posted straight to
    /// the control account. It cannot be attributed to anyone, so it can never be chased or aged,
    /// and it is the usual reason a control account stops agreeing with its sub-ledger.
    /// </summary>
    [JsonPropertyName("unattributed_gl")]
    public decimal UnattributedGl { get; set; }

    /// <summary>Sub-ledger total minus the full control total. Zero is the healthy state.</summary>
    [JsonPropertyName("difference")]
    public decimal Difference { get; set; }

    /// <summary>True when no account is flagged as the control account for this party type.</summary>
    [JsonPropertyName("no_control_account")]
    public bool NoControlAccount { get; set; }

    [JsonPropertyName("rows")]
    public List<FinSubLedgerPartyDto> Rows { get; set; } = new();
}

public class FinSubLedgerStatementRowDto
{
    [JsonPropertyName("txn_date")]
    public DateTime TxnDate { get; set; }

    [JsonPropertyName("ref_doc_type")]
    public short RefDocType { get; set; }

    [JsonPropertyName("ref_doc_no")]
    public string RefDocNo { get; set; } = string.Empty;

    [JsonPropertyName("debit")]
    public decimal Debit { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("running_balance")]
    public decimal RunningBalance { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class FinSubLedgerStatementDto
{
    [JsonPropertyName("party_no")]
    public long PartyNo { get; set; }

    [JsonPropertyName("party_name")]
    public string PartyName { get; set; } = string.Empty;

    [JsonPropertyName("party_type")]
    public short PartyType { get; set; }

    [JsonPropertyName("from_date")]
    public DateTime FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime ToDate { get; set; }

    [JsonPropertyName("opening_balance")]
    public decimal OpeningBalance { get; set; }

    [JsonPropertyName("closing_balance")]
    public decimal ClosingBalance { get; set; }

    [JsonPropertyName("rows")]
    public List<FinSubLedgerStatementRowDto> Rows { get; set; } = new();
}
