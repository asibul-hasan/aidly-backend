using System.Text.Json.Serialization;

namespace AidlyErp.Application.Fin.Contract;

/// <summary>
/// The standard sys_event_outbox payload contract for GL posting.
/// Any emitter (HRM/INV/PUR/SAL) produces this envelope; FinPostingService resolves each
/// Leg's account via fin_gl_map(company, event_type, leg_key[, sub_key]) and posts one balanced voucher.
/// </summary>
public class GlPostingPayload
{
    [JsonPropertyName("voucherDate")]
    public DateTime? VoucherDate { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("branchNo")]
    public long? BranchNo { get; set; }

    [JsonPropertyName("legs")]
    public List<Leg> Legs { get; set; } = new();

    public class Leg
    {
        [JsonPropertyName("legKey")]
        public string LegKey { get; set; } = string.Empty;

        [JsonPropertyName("subKey")]
        public string? SubKey { get; set; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("drCr")]
        public string DrCr { get; set; } = "dr"; // dr=Debit cr=Credit

        [JsonPropertyName("costCenterNo")]
        public long? CostCenterNo { get; set; }

        [JsonPropertyName("partyType")]
        public short? PartyType { get; set; } // 1=Customer 2=Supplier 3=Employee

        [JsonPropertyName("partyNo")]
        public long? PartyNo { get; set; }
    }
}
