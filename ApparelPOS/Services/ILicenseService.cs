using ApparelPOS.Models;

namespace ApparelPOS.Services;

public interface ILicenseService
{
    string LicenseFilePath { get; }
    LicenseInfo ValidateCurrentLicense();
    LicenseInfo ValidateAndSaveLicense(string licenseContent);
    LicenseInfo GetCachedLicenseInfo();
}
