using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Events;
using DigitalTwin.Domain.Interfaces;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Infrastructure.Services.Notifications;

public sealed class NotificationEventHandlers :
    IDomainEventHandler<PatientAssignedEvent>,
    IDomainEventHandler<PatientUnassignedEvent>,
    IDomainEventHandler<MedicationAddedEvent>,
    IDomainEventHandler<MedicationDiscontinuedEvent>,
    IDomainEventHandler<MedicationDeletedEvent>
{
    private readonly IUserRepository _users;
    private readonly IPatientRepository _patients;
    private readonly INotificationRepository _notifications;
    private readonly ILogger<NotificationEventHandlers> _logger;

    public NotificationEventHandlers(
        IUserRepository users,
        IPatientRepository patients,
        INotificationRepository notifications,
        ILogger<NotificationEventHandlers> logger)
    {
        _users = users;
        _patients = patients;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task HandleAsync(PatientAssignedEvent domainEvent, CancellationToken ct = default)
    {
        var doctor = await _users.GetByIdAsync(domainEvent.DoctorId);
        var patient = await _patients.GetByIdAsync(domainEvent.PatientId);
        if (doctor is null || patient is null)
        {
            _logger.LogWarning(
                "[Notifications] Skipping patient assignment notification: doctor={DoctorId} patient={PatientId}",
                domainEvent.DoctorId,
                domainEvent.PatientId);
            return;
        }

        var doctorName = doctor.FullName;
        var patientUser = await _users.GetByIdAsync(patient.UserId);

        var doctorNotification = new NotificationItem
        {
            RecipientUserId = doctor.Id,
            RecipientRole = UserRole.Doctor,
            Title = "New patient assigned",
            Body = $"{domainEvent.PatientEmail} was assigned to you.",
            Type = NotificationType.PatientAssignment,
            Severity = NotificationSeverity.Info,
            PatientId = patient.Id,
            ActorUserId = doctor.Id,
            ActorName = doctorName
        };

        await _notifications.AddAsync(doctorNotification);

        if (patientUser is not null)
        {
            var patientNotification = new NotificationItem
            {
                RecipientUserId = patientUser.Id,
                RecipientRole = UserRole.Patient,
                Title = "Doctor assigned",
                Body = string.IsNullOrWhiteSpace(doctorName)
                    ? "A doctor was assigned to your profile."
                    : $"{doctorName} was assigned to your profile.",
                Type = NotificationType.PatientAssignment,
                Severity = NotificationSeverity.Info,
                PatientId = patient.Id,
                ActorUserId = doctor.Id,
                ActorName = doctorName
            };

            await _notifications.AddAsync(patientNotification);
        }
    }

    public async Task HandleAsync(PatientUnassignedEvent domainEvent, CancellationToken ct = default)
    {
        var doctor = await _users.GetByIdAsync(domainEvent.DoctorId);
        var patient = await _patients.GetByIdAsync(domainEvent.PatientId);
        if (doctor is null || patient is null)
        {
            _logger.LogWarning(
                "[Notifications] Skipping patient unassignment notification: doctor={DoctorId} patient={PatientId}",
                domainEvent.DoctorId,
                domainEvent.PatientId);
            return;
        }

        var doctorName = doctor.FullName;
        var patientUser = await _users.GetByIdAsync(patient.UserId);

        var doctorNotification = new NotificationItem
        {
            RecipientUserId = doctor.Id,
            RecipientRole = UserRole.Doctor,
            Title = "Patient unassigned",
            Body = $"A patient was removed from your assignments.",
            Type = NotificationType.PatientUnassigned,
            Severity = NotificationSeverity.Low,
            PatientId = patient.Id,
            ActorUserId = doctor.Id,
            ActorName = doctorName
        };

        await _notifications.AddAsync(doctorNotification);

        if (patientUser is not null)
        {
            var patientNotification = new NotificationItem
            {
                RecipientUserId = patientUser.Id,
                RecipientRole = UserRole.Patient,
                Title = "Doctor unassigned",
                Body = string.IsNullOrWhiteSpace(doctorName)
                    ? "A doctor was removed from your profile."
                    : $"{doctorName} was removed from your profile.",
                Type = NotificationType.PatientUnassigned,
                Severity = NotificationSeverity.Low,
                PatientId = patient.Id,
                ActorUserId = doctor.Id,
                ActorName = doctorName
            };

            await _notifications.AddAsync(patientNotification);
        }
    }

    public async Task HandleAsync(MedicationAddedEvent domainEvent, CancellationToken ct = default)
    {
        await CreatePatientMedicationNotification(
            domainEvent.PatientId,
            NotificationType.MedicationAdded,
            NotificationSeverity.Info,
            "New medication added",
            $"{domainEvent.Name} was added to your medications.");
    }

    public async Task HandleAsync(MedicationDiscontinuedEvent domainEvent, CancellationToken ct = default)
    {
        await CreatePatientMedicationNotification(
            domainEvent.PatientId,
            NotificationType.MedicationDiscontinued,
            NotificationSeverity.Medium,
            "Medication discontinued",
            "A medication was discontinued. Review your medication list for details.");
    }

    public async Task HandleAsync(MedicationDeletedEvent domainEvent, CancellationToken ct = default)
    {
        await CreatePatientMedicationNotification(
            domainEvent.PatientId,
            NotificationType.MedicationDeleted,
            NotificationSeverity.Low,
            "Medication removed",
            "A medication was removed from your list.");
    }

    private async Task CreatePatientMedicationNotification(
        Guid patientId,
        NotificationType type,
        NotificationSeverity severity,
        string title,
        string body)
    {
        var patient = await _patients.GetByIdAsync(patientId);
        if (patient is null)
            return;

        var patientUser = await _users.GetByIdAsync(patient.UserId);
        if (patientUser is null)
            return;

        await _notifications.AddAsync(new NotificationItem
        {
            RecipientUserId = patientUser.Id,
            RecipientRole = UserRole.Patient,
            Title = title,
            Body = body,
            Type = type,
            Severity = severity,
            PatientId = patient.Id
        });
    }
}
