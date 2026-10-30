namespace ApparelPOS.Models;

public class LicenseInfo
{
    public string MachineCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = "LIFETIME";
    public string IssuedAt { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public bool IsTrial { get; set; }
    public int TrialDaysRemaining { get; set; }
    public bool IsClockTampered { get; set; }
}
