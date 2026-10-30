namespace ApparelPOS.Services;

public interface IHardwareInfoService
{
    string GetMachineCode();
    string GetRawHardwareFingerprint();
}
