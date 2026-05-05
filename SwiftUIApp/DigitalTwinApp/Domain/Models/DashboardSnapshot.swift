import Foundation

struct DashboardSnapshot {
    let user: UserInfo?
    let patientProfile: PatientInfo?
    let recentVitals: [VitalSignInfo]
    let coachingAdvice: CoachingAdviceInfo?
    let environmentReading: EnvironmentReadingInfo?
    let sleepSessions: [SleepSessionInfo]
    let medications: [MedicationInfo]
}

