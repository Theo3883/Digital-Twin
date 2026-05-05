import Foundation
import MapKit

/// Service for converting city names to coordinates using MapKit
actor GeocodingService {

    struct GeocodingResult: Sendable {
        let latitude: Double
        let longitude: Double
        let displayName: String
    }

    func geocode(city: String) async -> GeocodingResult? {
        let request = MKLocalSearch.Request()
        request.naturalLanguageQuery = city
        let search = MKLocalSearch(request: request)
        do {
            let response = try await search.start()
            guard let item = response.mapItems.first else { return nil }
            let location = item.location
            let displayName = item.name ?? city
            return GeocodingResult(
                latitude: location.coordinate.latitude,
                longitude: location.coordinate.longitude,
                displayName: displayName
            )
        } catch {
            return nil
        }
    }
}
