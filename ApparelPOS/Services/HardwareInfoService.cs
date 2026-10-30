using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ApparelPOS.Services;

public class HardwareInfoService : IHardwareInfoService
{
    private string? _cachedMachineCode;

    public string GetMachineCode()
    {
        if (!string.IsNullOrEmpty(_cachedMachineCode))
        {
            return _cachedMachineCode;
        }

        var rawFingerprint = GetRawHardwareFingerprint();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawFingerprint));
        
        // Convert to hex uppercase string
        var hex = Convert.ToHexString(hashBytes).ToUpperInvariant();
        
        // Take first 16 characters and format into 4 blocks of 4: XXXX-XXXX-XXXX-XXXX
        var clean16 = hex[..16];
        _cachedMachineCode = $"{clean16[..4]}-{clean16.Substring(4, 4)}-{clean16.Substring(8, 4)}-{clean16.Substring(12, 4)}";
        
        return _cachedMachineCode;
    }

    public string GetRawHardwareFingerprint()
    {
        var sb = new StringBuilder();

        // 1. Motherboard UUID (Win32_ComputerSystemProduct)
        var mbUuid = GetWmiValue("Win32_ComputerSystemProduct", "UUID");
        if (string.IsNullOrWhiteSpace(mbUuid) || mbUuid.Equals("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", StringComparison.OrdinalIgnoreCase))
        {
            mbUuid = GetWmiValue("Win32_BaseBoard", "SerialNumber");
        }
        sb.Append("MBUUID:").Append(mbUuid ?? "GENERIC_MB").Append('|');

        // 2. CPU Processor ID (Win32_Processor)
        var cpuId = GetWmiValue("Win32_Processor", "ProcessorId");
        if (string.IsNullOrWhiteSpace(cpuId))
        {
            cpuId = GetWmiValue("Win32_Processor", "UniqueId") ?? Environment.ProcessorCount.ToString();
        }
        sb.Append("CPUID:").Append(cpuId ?? "GENERIC_CPU").Append('|');

        // 3. Primary Disk Serial (Win32_DiskDrive)
        var diskSerial = GetWmiValue("Win32_DiskDrive", "SerialNumber");
        if (string.IsNullOrWhiteSpace(diskSerial))
        {
            diskSerial = GetWmiValue("Win32_PhysicalMedia", "SerialNumber");
        }
        sb.Append("DISK:").Append(diskSerial?.Trim() ?? "GENERIC_DISK").Append('|');

        // Fallback salt from Windows MachineGuid to ensure uniqueness if running in virtualized or stripped environments
        var registryGuid = GetWindowsMachineGuid();
        if (!string.IsNullOrEmpty(registryGuid))
        {
            sb.Append("REGGUID:").Append(registryGuid);
        }

        return sb.ToString();
    }

    private static string? GetWmiValue(string wmiClass, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClass}");
            using var collection = searcher.Get();
            foreach (var item in collection)
            {
                var val = item[propertyName]?.ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    return val.Trim();
                }
            }
        }
        catch
        {
            // Fail-safe if WMI service is restricted or unavailable
        }
        return null;
    }

    private static string? GetWindowsMachineGuid()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                       .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}
