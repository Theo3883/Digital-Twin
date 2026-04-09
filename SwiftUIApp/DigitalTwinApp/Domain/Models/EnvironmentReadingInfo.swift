import Foundation

struct EnvironmentReadingInfo: Codable, Identifiable {
    var id: UUID
    let latitude: Double
    let longitude: Double
    let locationDisplayName: String?
    let pm25: Double?
    let pm10: Double?
    let o3: Double?
    let no2: Double?
    let temperature: Double?
    let humidity: Double?
    let airQualityLevel: Int  // AirQualityLevel enum
    let aqiIndex: Int?
    let timestamp: Date

    private enum CodingKeys: String, CodingKey {
        case id, latitude, longitude, locationDisplayName
        case pm25 = "pM25"
        case pm10 = "pM10"
        case o3
        case no2 = "nO2"
        case temperature, humidity
        case airQualityLevel = "airQuality"
        case aqiIndex, timestamp
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = (try? container.decode(UUID.self, forKey: .id)) ?? UUID()
        latitude = try container.decode(Double.self, forKey: .latitude)
        longitude = try container.decode(Double.self, forKey: .longitude)
        locationDisplayName = try container.decodeIfPresent(String.self, forKey: .locationDisplayName)
        pm25 = try container.decodeIfPresent(Double.self, forKey: .pm25)
        pm10 = try container.decodeIfPresent(Double.self, forKey: .pm10)
        o3 = try container.decodeIfPresent(Double.self, forKey: .o3)
        no2 = try container.decodeIfPresent(Double.self, forKey: .no2)
        temperature = try container.decodeIfPresent(Double.self, forKey: .temperature)
        humidity = try container.decodeIfPresent(Double.self, forKey: .humidity)
        airQualityLevel = try container.decode(Int.self, forKey: .airQualityLevel)
        aqiIndex = try container.decodeIfPresent(Int.self, forKey: .aqiIndex)
        timestamp = try container.decode(Date.self, forKey: .timestamp)
    }

    var airQualityDisplay: String {
        switch airQualityLevel {
        case 0: return "Good"
        case 1: return "Fair"
        case 2: return "Moderate"
        case 3: return "Poor"
        case 4: return "Very Poor"
        default: return "Unknown"
        }
    }

    var airQualityEmoji: String {
        switch airQualityLevel {
        case 0: return "🟢"
        case 1: return "🟡"
        case 2: return "🟠"
        case 3: return "🔴"
        case 4: return "🟣"
        default: return "⚪"
        }
    }
}

