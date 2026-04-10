import Foundation

struct HourlyDataPointInfo: Codable {
    let hour: Int
    let value: Double
}

struct EnvironmentAnalyticsInfo: Codable {
    let correlationR: Double?
    let footnote: String
    let heartRateSeries: [HourlyDataPointInfo]
    let pm25Series: [HourlyDataPointInfo]
}
