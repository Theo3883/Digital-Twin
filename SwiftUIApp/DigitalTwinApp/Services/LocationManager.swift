import CoreLocation
import Foundation

final class LocationManager: NSObject, ObservableObject, CLLocationManagerDelegate {
    enum LocationMode: String {
        case current
        case manual
    }

    private enum Keys {
        static let mode = "environment.location.mode"
        static let manualLatitude = "environment.location.manual.latitude"
        static let manualLongitude = "environment.location.manual.longitude"
        static let manualDisplayName = "environment.location.manual.displayName"
        static let currentLatitude = "environment.location.current.latitude"
        static let currentLongitude = "environment.location.current.longitude"
    }

    private let manager = CLLocationManager()
    @Published var lastLocation: CLLocation?
    @Published var authorizationStatus: CLAuthorizationStatus = .notDetermined

    override init() {
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyHundredMeters
        authorizationStatus = manager.authorizationStatus

        if let cachedCurrent = Self.cachedCurrentLocationCoordinates() {
            lastLocation = CLLocation(latitude: cachedCurrent.latitude, longitude: cachedCurrent.longitude)
        }

        if manager.authorizationStatus == .authorizedWhenInUse || manager.authorizationStatus == .authorizedAlways {
            manager.startUpdatingLocation()
        }
        // Location permission is requested upfront in PermissionsOnboardingView
    }

    func requestLocation() {
        manager.requestLocation()
    }

    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        authorizationStatus = manager.authorizationStatus
        if manager.authorizationStatus == .authorizedWhenInUse || manager.authorizationStatus == .authorizedAlways {
            manager.startUpdatingLocation()
        }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let location = locations.last else { return }

        lastLocation = location
        UserDefaults.standard.set(location.coordinate.latitude, forKey: Keys.currentLatitude)
        UserDefaults.standard.set(location.coordinate.longitude, forKey: Keys.currentLongitude)
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        print("[LocationManager] Error: \(error)")
    }

    static func setUseCurrentLocation() {
        UserDefaults.standard.set(LocationMode.current.rawValue, forKey: Keys.mode)
    }

    static func saveManualLocation(latitude: Double, longitude: Double, displayName: String) {
        UserDefaults.standard.set(LocationMode.manual.rawValue, forKey: Keys.mode)
        UserDefaults.standard.set(latitude, forKey: Keys.manualLatitude)
        UserDefaults.standard.set(longitude, forKey: Keys.manualLongitude)
        UserDefaults.standard.set(displayName, forKey: Keys.manualDisplayName)
    }

    static func manualLocationCoordinatesIfSelected() -> (latitude: Double, longitude: Double)? {
        guard selectedLocationMode() == .manual else { return nil }

        return manualLocationCoordinates()
    }

    static func manualLocationCoordinates() -> (latitude: Double, longitude: Double)? {
        guard UserDefaults.standard.object(forKey: Keys.manualLatitude) != nil,
              UserDefaults.standard.object(forKey: Keys.manualLongitude) != nil else {
            return nil
        }

        let latitude = UserDefaults.standard.double(forKey: Keys.manualLatitude)
        let longitude = UserDefaults.standard.double(forKey: Keys.manualLongitude)
        return (latitude, longitude)
    }

    static func cachedCurrentLocationCoordinates() -> (latitude: Double, longitude: Double)? {
        guard UserDefaults.standard.object(forKey: Keys.currentLatitude) != nil,
              UserDefaults.standard.object(forKey: Keys.currentLongitude) != nil else {
            return nil
        }

        let latitude = UserDefaults.standard.double(forKey: Keys.currentLatitude)
        let longitude = UserDefaults.standard.double(forKey: Keys.currentLongitude)
        return (latitude, longitude)
    }

    static func selectedLocationMode() -> LocationMode {
        guard let raw = UserDefaults.standard.string(forKey: Keys.mode),
              let mode = LocationMode(rawValue: raw) else {
            return .current
        }

        return mode
    }
}

