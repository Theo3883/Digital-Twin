using System.Linq;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Providers;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;
#if IOS || ANDROID
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
#endif
using Microsoft.Maui.Storage;

namespace DigitalTwin.Integrations.Environment;

/// <summary>
/// Resolves coordinates from device GPS (iOS/Android) or manual city geocoding; falls back to configured lat/lon when GPS is unavailable.
/// </summary>
public sealed class EnvironmentLocationSource : IEnvironmentLocationSource
{
    private const string PrefMode = "env_loc_mode";
    private const string PrefManualCity = "env_manual_city";
    private const string PrefCacheLat = "env_cache_lat";
    private const string PrefCacheLon = "env_cache_lon";
    private const string PrefCacheLabel = "env_cache_label";

    private const string ModeDevice = "device";
    private const string ModeManual = "manual";

    private readonly OpenWeatherGeocodingClient _geocoding;
    private readonly double _fallbackLat;
    private readonly double _fallbackLon;
    private readonly ILogger<EnvironmentLocationSource>? _logger;

    public EnvironmentLocationSource(
        OpenWeatherGeocodingClient geocoding,
        double fallbackLat,
        double fallbackLon,
        ILogger<EnvironmentLocationSource>? logger = null)
    {
        _geocoding = geocoding;
        _fallbackLat = fallbackLat;
        _fallbackLon = fallbackLon;
        _logger = logger;
    }

    public EnvironmentLocationMode Mode =>
        Preferences.Get(PrefMode, ModeDevice) == ModeManual
            ? EnvironmentLocationMode.Manual
            : EnvironmentLocationMode.Device;

    public string? ManualCityName
    {
        get
        {
            var s = Preferences.Get(PrefManualCity, string.Empty);
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
    }

    public void SetMode(EnvironmentLocationMode mode)
    {
        Preferences.Set(PrefMode, mode == EnvironmentLocationMode.Manual ? ModeManual : ModeDevice);
    }

    public void SetManualCityName(string? cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
            Preferences.Remove(PrefManualCity);
        else
            Preferences.Set(PrefManualCity, cityName.Trim());
    }

    public async Task<EnvironmentLocationResult> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (Mode == EnvironmentLocationMode.Manual)
            return await ResolveManualAsync(cancellationToken).ConfigureAwait(false);

        return await ResolveDeviceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<EnvironmentLocationResult> ResolveManualAsync(CancellationToken cancellationToken)
    {
        var query = ManualCityName;
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("Enter a city name, or switch to using your current location.");

        var hit = await _geocoding.GeocodeFirstAsync(query, cancellationToken).ConfigureAwait(false);
        if (hit is null)
            throw new InvalidOperationException("No location found for that city. Check spelling or add a country code (e.g. Paris, FR).");

        return new EnvironmentLocationResult(hit.Latitude, hit.Longitude, hit.DisplayName);
    }

    private async Task<EnvironmentLocationResult> ResolveDeviceAsync(CancellationToken cancellationToken)
    {
#if IOS || ANDROID
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false);
            if (status != PermissionStatus.Granted)
            {
                _logger?.LogWarning("Location permission not granted ({Status}); trying cache or fallback.", status);
                return TryCacheOrFallback("Location permission not granted");
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(25));
            var location = await Geolocation.Default.GetLocationAsync(request, cancellationToken).ConfigureAwait(false);
            if (location is not null)
            {
                var lat = location.Latitude;
                var lon = location.Longitude;
                var label = await TryReverseGeocodeLabelAsync(lat, lon, cancellationToken).ConfigureAwait(false)
                            ?? "Current location";
                Preferences.Set(PrefCacheLat, lat.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Preferences.Set(PrefCacheLon, lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Preferences.Set(PrefCacheLabel, label);
                return new EnvironmentLocationResult(lat, lon, label);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Device location failed; trying cache or fallback.");
        }

        return TryCacheOrFallback("Could not get device location");
#else
        return FallbackResult("Default location (configure LATITUDE/LONGITUDE or run on iOS/Android for GPS).");
#endif
    }

#if IOS || ANDROID
    private async Task<string?> TryReverseGeocodeLabelAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        try
        {
            var placemarks = await Geocoding.GetPlacemarksAsync(lat, lon).ConfigureAwait(false);
            var p = placemarks?.FirstOrDefault();
            if (p is null)
                return null;

            var locality = !string.IsNullOrWhiteSpace(p.Locality)
                ? p.Locality.Trim()
                : (!string.IsNullOrWhiteSpace(p.SubLocality) ? p.SubLocality.Trim() : null);

            var country = !string.IsNullOrWhiteSpace(p.CountryCode)
                ? p.CountryCode.Trim().ToUpperInvariant()
                : null;

            if (!string.IsNullOrEmpty(locality) && !string.IsNullOrEmpty(country))
                return $"{locality}, {country}";

            if (!string.IsNullOrEmpty(locality))
                return locality;

            if (!string.IsNullOrEmpty(p.FeatureName))
                return p.FeatureName.Trim();

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Reverse geocoding failed; using generic location label.");
            return null;
        }
    }
#endif

    private EnvironmentLocationResult TryCacheOrFallback(string logReason)
    {
        var latStr = Preferences.Get(PrefCacheLat, string.Empty);
        var lonStr = Preferences.Get(PrefCacheLon, string.Empty);
        if (double.TryParse(latStr, System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(lonStr, System.Globalization.CultureInfo.InvariantCulture, out var lon))
        {
            var label = Preferences.Get(PrefCacheLabel, "Last known location");
            _logger?.LogInformation("Using cached coordinates ({Reason})", logReason);
            return new EnvironmentLocationResult(lat, lon, label);
        }

        _logger?.LogWarning("{Reason}; using configured LATITUDE/LONGITUDE fallback.", logReason);
        return new EnvironmentLocationResult(_fallbackLat, _fallbackLon, "Default location");
    }

    private EnvironmentLocationResult FallbackResult(string displayName) =>
        new(_fallbackLat, _fallbackLon, displayName);
}
