namespace DigitalTwin.Domain.Enums;

public enum NotificationType
{
    General = 0,
    PatientAssignment = 1,
    PatientUnassigned = 2,
    MedicationAdded = 3,
    MedicationDiscontinued = 4,
    MedicationDeleted = 5,
    CriticalAlert = 6
}
