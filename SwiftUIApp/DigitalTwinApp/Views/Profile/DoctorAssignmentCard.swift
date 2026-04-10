import SwiftUI

struct DoctorAssignmentCard: View {
    let doctors: [AssignedDoctorInfo]

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 10) {
                Image(systemName: "stethoscope")
                    .font(.title3)
                    .foregroundColor(LiquidGlass.tealPrimary)
                Text("Assigned Doctors")
                    .font(.subheadline.weight(.medium))
                    .foregroundColor(.white)
                Spacer()
                if !doctors.isEmpty {
                    Text("\(doctors.count)")
                        .font(.caption2.weight(.bold))
                        .foregroundColor(.white)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 3)
                        .background(RoundedRectangle(cornerRadius: LiquidGlass.radiusChip).fill(LiquidGlass.tealPrimary.opacity(0.3)))
                }
            }

            if doctors.isEmpty {
                HStack {
                    Spacer()
                    VStack(spacing: 8) {
                        Image(systemName: "person.badge.clock")
                            .font(.system(size: 28))
                            .foregroundColor(.white.opacity(0.2))
                        Text("No doctors assigned yet")
                            .font(.caption)
                            .foregroundColor(.white.opacity(0.4))
                    }
                    .padding(.vertical, 12)
                    Spacer()
                }
            } else {
                ForEach(doctors) { doctor in
                    DoctorRow(doctor: doctor)
                }
            }
        }
        .glassCard()
    }
}

// MARK: - Doctor Row

private struct DoctorRow: View {
    let doctor: AssignedDoctorInfo

    var body: some View {
        HStack(spacing: 12) {
            // Avatar
            AsyncImage(url: doctor.photoUrl.flatMap(URL.init)) { image in
                image.resizable().aspectRatio(contentMode: .fill)
            } placeholder: {
                Image(systemName: "person.circle.fill")
                    .font(.system(size: 28))
                    .foregroundColor(.white.opacity(0.3))
            }
            .frame(width: 40, height: 40)
            .clipShape(Circle())

            VStack(alignment: .leading, spacing: 2) {
                Text(doctor.fullName)
                    .font(.system(size: 14, weight: .medium))
                    .foregroundColor(.white)
                Text(doctor.email)
                    .font(.caption2)
                    .foregroundColor(.white.opacity(0.4))
                if let notes = doctor.notes, !notes.isEmpty {
                    Text(notes)
                        .font(.caption2)
                        .foregroundColor(.white.opacity(0.5))
                        .lineLimit(1)
                }
            }

            Spacer()

            Text(doctor.assignedAt, style: .date)
                .font(.caption2)
                .foregroundColor(.white.opacity(0.3))
        }
        .padding(.vertical, 4)
    }
}
