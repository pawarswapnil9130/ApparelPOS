using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApparelPOS.Models;

namespace ApparelPOS.Services;

public class LicenseService : ILicenseService
{
    private readonly IHardwareInfoService _hardwareInfoService;
    private LicenseInfo? _cachedLicenseInfo;

    // Embedded RSA-2048 Public Key in PEM format (SubjectPublicKeyInfo)
    private const string EmbeddedRsaPublicKeyPem = 
@"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA6Hk//zvd0N7BrxqHDwZP
W+mgSdQQQdTA6qyer12Vft3rnayhiVwq9hmbFBCSuHMCbfWqWBN+ZbYoMFtwQwsp
3IeenxemTPx42PKa+H0fneePH9fGOhu8RaO72YXrdzDzv9sd1fM9+q2xvlI9jQP0
HbcNgr+IRkPUT6lywXGTlKcs/2GXZRRUxx3bGFBoo2ZI9Wzlyu8dEZmnQeCOHQLg
piBviPjwNaQxTQVRip6AULGiuBq54ZMFIJdDrzHR4wpOWZU6A1CKz4s8vJzzVuwx
oqyhI8SpU2t6Hdc+hGtYFfcJM3WZ+1e1yERlb3EWxPLnuDrGCrEJVywjUC9aGxnA
7QIDAQAB
-----END PUBLIC KEY-----";

    private const string TrialSecretSalt = "ApparelPOS_90DayTrial_OfflineProtection_Salt_2026";
    private const int TotalTrialDays = 90;

    public string LicenseFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ApparelPOS");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "license.lic");
        }
    }

    public string TrialFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ApparelPOS");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, ".trial_state");
        }
    }

    public LicenseService(IHardwareInfoService hardwareInfoService)
    {
        _hardwareInfoService = hardwareInfoService;
    }

    public LicenseInfo GetCachedLicenseInfo()
    {
        return _cachedLicenseInfo ?? ValidateCurrentLicense();
    }

    public LicenseInfo ValidateCurrentLicense()
    {
        var machineCode = _hardwareInfoService.GetMachineCode();
        var filePath = LicenseFilePath;

        // 1. Check for Commercial RSA Signed License
        if (File.Exists(filePath))
        {
            try
            {
                var content = File.ReadAllText(filePath).Trim();
                var commercialResult = VerifyLicenseString(content);
                if (commercialResult.IsValid)
                {
                    commercialResult.IsTrial = false;
                    _cachedLicenseInfo = commercialResult;
                    return _cachedLicenseInfo;
                }
            }
            catch
            {
                // If commercial license is corrupt/invalid, fall through to trial check
            }
        }

        // 2. Evaluate 90-Day Free Trial State
        _cachedLicenseInfo = EvaluateTrialState(machineCode);
        return _cachedLicenseInfo;
    }

    public LicenseInfo EvaluateTrialState(string machineCode)
    {
        var trialState = LoadTrialState(machineCode);

        if (trialState == null)
        {
            // First run on this machine: Initialize 90-Day Free Trial
            var today = DateTime.Today;
            trialState = new TrialRecord
            {
                MachineCode = machineCode,
                FirstRunDate = today.ToString("yyyy-MM-dd"),
                LastRunDate = today.ToString("yyyy-MM-dd"),
                Checksum = ComputeChecksum(machineCode, today.ToString("yyyy-MM-dd"))
            };
            SaveTrialState(trialState);

            return new LicenseInfo
            {
                IsValid = true,
                IsTrial = true,
                TrialDaysRemaining = TotalTrialDays,
                MachineCode = machineCode,
                CustomerName = "Valued Retailer",
                StoreName = "Apparel Store (Free Trial)",
                LicenseType = "90-Day Free Trial",
                ExpiryDate = today.AddDays(TotalTrialDays).ToString("yyyy-MM-dd"),
                StatusMessage = $"Welcome! 90-Day Free Trial active ({TotalTrialDays} days remaining)."
            };
        }

        // Parse stored dates
        if (!DateTime.TryParse(trialState.FirstRunDate, out var firstRun) ||
            !DateTime.TryParse(trialState.LastRunDate, out var lastRun))
        {
            // Corrupt trial record
            return new LicenseInfo
            {
                IsValid = false,
                IsTrial = true,
                TrialDaysRemaining = 0,
                MachineCode = machineCode,
                StatusMessage = "Trial record invalid. Please activate software for ₹249/year."
            };
        }

        // Clock tampering check: Has system date been rolled back?
        if (DateTime.Today < lastRun.Date)
        {
            return new LicenseInfo
            {
                IsValid = false,
                IsTrial = true,
                IsClockTampered = true,
                TrialDaysRemaining = 0,
                MachineCode = machineCode,
                StatusMessage = $"Clock tampering detected! System date ({DateTime.Today:dd-MMM-yyyy}) is earlier than last recorded run ({lastRun:dd-MMM-yyyy}). Please set correct PC date."
            };
        }

        // Update last run date to today
        trialState.LastRunDate = DateTime.Today.ToString("yyyy-MM-dd");
        SaveTrialState(trialState);

        var elapsedDays = (int)(DateTime.Today - firstRun.Date).TotalDays;
        var remainingDays = TotalTrialDays - elapsedDays;

        if (remainingDays > 0)
        {
            return new LicenseInfo
            {
                IsValid = true,
                IsTrial = true,
                TrialDaysRemaining = remainingDays,
                MachineCode = machineCode,
                CustomerName = "Valued Retailer",
                StoreName = "Apparel Store (Free Trial)",
                LicenseType = "90-Day Free Trial",
                ExpiryDate = firstRun.AddDays(TotalTrialDays).ToString("yyyy-MM-dd"),
                StatusMessage = $"{remainingDays} days remaining in 90-day free trial."
            };
        }

        // Trial has expired (>90 days)
        return new LicenseInfo
        {
            IsValid = false,
            IsTrial = true,
            TrialDaysRemaining = 0,
            MachineCode = machineCode,
            LicenseType = "90-Day Free Trial (Expired)",
            ExpiryDate = firstRun.AddDays(TotalTrialDays).ToString("yyyy-MM-dd"),
            StatusMessage = "Your 90-Day Free Trial has expired. Please activate 1-year license for ₹249 to continue."
        };
    }

    private TrialRecord? LoadTrialState(string currentMachineCode)
    {
        TrialRecord? recordFromFile = null;
        TrialRecord? recordFromRegistry = null;

        // Try load from File
        if (File.Exists(TrialFilePath))
        {
            try
            {
                var json = File.ReadAllText(TrialFilePath, Encoding.UTF8);
                recordFromFile = JsonSerializer.Deserialize<TrialRecord>(json);
            }
            catch { }
        }

        // Try load from Registry
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ApparelPOS\Licensing");
            if (key != null)
            {
                var json = key.GetValue("TrialRecord") as string;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    recordFromRegistry = JsonSerializer.Deserialize<TrialRecord>(json);
                }
            }
        }
        catch { }

        // Choose the earliest recorded install date if either or both exist
        TrialRecord? chosen = null;
        if (recordFromFile != null && recordFromRegistry != null)
        {
            // Pick the one with the earlier FirstRunDate to prevent resetting
            if (DateTime.TryParse(recordFromFile.FirstRunDate, out var d1) &&
                DateTime.TryParse(recordFromRegistry.FirstRunDate, out var d2))
            {
                chosen = (d1 <= d2) ? recordFromFile : recordFromRegistry;
            }
            else
            {
                chosen = recordFromFile;
            }
        }
        else
        {
            chosen = recordFromFile ?? recordFromRegistry;
        }

        if (chosen == null) return null;

        // Validate MachineCode & Checksum integrity
        var expectedChecksum = ComputeChecksum(chosen.MachineCode, chosen.FirstRunDate);
        if (!string.Equals(chosen.Checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            return null; // Tampered checksum
        }

        if (!string.Equals(chosen.MachineCode, currentMachineCode, StringComparison.OrdinalIgnoreCase))
        {
            return null; // Copied from another machine
        }

        return chosen;
    }

    private void SaveTrialState(TrialRecord record)
    {
        var json = JsonSerializer.Serialize(record);

        // 1. Save to File
        try
        {
            File.WriteAllText(TrialFilePath, json, Encoding.UTF8);
            var fileInfo = new FileInfo(TrialFilePath);
            fileInfo.Attributes |= FileAttributes.Hidden;
        }
        catch { }

        // 2. Save to Registry
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\ApparelPOS\Licensing");
            key?.SetValue("TrialRecord", json);
        }
        catch { }
    }

    private static string ComputeChecksum(string machineCode, string firstRunDate)
    {
        var raw = $"{machineCode}_{firstRunDate}_{TrialSecretSalt}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private class TrialRecord
    {
        public string MachineCode { get; set; } = string.Empty;
        public string FirstRunDate { get; set; } = string.Empty;
        public string LastRunDate { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
    }

    public LicenseInfo ValidateAndSaveLicense(string licenseContent)
    {
        if (string.IsNullOrWhiteSpace(licenseContent))
        {
            return new LicenseInfo
            {
                IsValid = false,
                MachineCode = _hardwareInfoService.GetMachineCode(),
                StatusMessage = "License key cannot be empty."
            };
        }

        var validationResult = VerifyLicenseString(licenseContent.Trim());
        if (validationResult.IsValid)
        {
            // Persist valid license
            File.WriteAllText(LicenseFilePath, licenseContent.Trim(), Encoding.UTF8);
            _cachedLicenseInfo = validationResult;
        }

        return validationResult;
    }

    private LicenseInfo VerifyLicenseString(string rawLicense)
    {
        var machineCode = _hardwareInfoService.GetMachineCode();

        var parts = rawLicense.Split('.');
        if (parts.Length != 2)
        {
            return new LicenseInfo
            {
                IsValid = false,
                MachineCode = machineCode,
                StatusMessage = "Malformed license structure. Expected payload.signature format."
            };
        }

        byte[] payloadBytes;
        byte[] signatureBytes;
        try
        {
            payloadBytes = Convert.FromBase64String(parts[0]);
            signatureBytes = Convert.FromBase64String(parts[1]);
        }
        catch
        {
            return new LicenseInfo
            {
                IsValid = false,
                MachineCode = machineCode,
                StatusMessage = "Invalid base64 encoding in license string."
            };
        }

        // Cryptographic verification with embedded RSA public key
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(EmbeddedRsaPublicKeyPem);

            var isSignatureValid = rsa.VerifyData(
                payloadBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!isSignatureValid)
            {
                return new LicenseInfo
                {
                    IsValid = false,
                    MachineCode = machineCode,
                    StatusMessage = "Cryptographic signature check failed. Untrusted or tampered license."
                };
            }
        }
        catch (Exception ex)
        {
            return new LicenseInfo
            {
                IsValid = false,
                MachineCode = machineCode,
                StatusMessage = $"Cryptographic validation error: {ex.Message}"
            };
        }

        // Parse JSON payload
        try
        {
            var jsonString = Encoding.UTF8.GetString(payloadBytes);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var licMachineCode = root.GetProperty("MachineCode").GetString()?.Trim().ToUpperInvariant() ?? "";
            var customerName = root.TryGetProperty("CustomerName", out var c) ? c.GetString() ?? "" : "";
            var storeName = root.TryGetProperty("StoreName", out var s) ? s.GetString() ?? "" : "";
            var expiryStr = root.TryGetProperty("ExpiryDate", out var e) ? e.GetString() ?? "LIFETIME" : "LIFETIME";
            var issuedAt = root.TryGetProperty("IssuedAt", out var i) ? i.GetString() ?? "" : "";
            var licType = root.TryGetProperty("LicenseType", out var t) ? t.GetString() ?? "Commercial" : "Commercial";

            // Verify HWID match
            if (!string.Equals(licMachineCode, machineCode, StringComparison.OrdinalIgnoreCase))
            {
                return new LicenseInfo
                {
                    IsValid = false,
                    MachineCode = machineCode,
                    CustomerName = customerName,
                    StoreName = storeName,
                    ExpiryDate = expiryStr,
                    IssuedAt = issuedAt,
                    LicenseType = licType,
                    StatusMessage = $"Hardware ID mismatch! Licensed to '{licMachineCode}', but this machine is '{machineCode}'."
                };
            }

            // Verify Expiry
            if (!string.Equals(expiryStr, "LIFETIME", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(expiryStr, out var expiryDate))
                {
                    if (DateTime.Today > expiryDate.Date)
                    {
                        return new LicenseInfo
                        {
                            IsValid = false,
                            MachineCode = machineCode,
                            CustomerName = customerName,
                            StoreName = storeName,
                            ExpiryDate = expiryStr,
                            IssuedAt = issuedAt,
                            LicenseType = licType,
                            StatusMessage = $"License expired on {expiryDate:dd-MMM-yyyy}. Please renew with seller."
                        };
                    }
                }
                else
                {
                    return new LicenseInfo
                    {
                        IsValid = false,
                        MachineCode = machineCode,
                        StatusMessage = "Invalid expiry date format in license."
                    };
                }
            }

            return new LicenseInfo
            {
                IsValid = true,
                MachineCode = machineCode,
                CustomerName = customerName,
                StoreName = storeName,
                ExpiryDate = expiryStr,
                IssuedAt = issuedAt,
                LicenseType = licType,
                StatusMessage = "License active and valid."
            };
        }
        catch (Exception ex)
        {
            return new LicenseInfo
            {
                IsValid = false,
                MachineCode = machineCode,
                StatusMessage = $"Failed to parse license payload: {ex.Message}"
            };
        }
    }
}
