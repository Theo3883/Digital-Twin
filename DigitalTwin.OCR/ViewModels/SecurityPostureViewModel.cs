using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Services;

namespace DigitalTwin.OCR.ViewModels;

public sealed class SecurityPostureViewModel
{
    private readonly SecurityService _securityService;
    private readonly VaultService _vault;
    private readonly OcrOptions _options;

    public SecurityPostureViewModel(
        SecurityService securityService,
        VaultService vault,
        OcrOptions options)
    {
        _securityService = securityService;
        _vault = vault;
        _options = options;
    }

    public SecurityPosture GetPosture()
        => _securityService.GetPosture(
            _options.SecurityMode,
            _vault.IsInitialized,
            _vault.IsUnlocked);
}
