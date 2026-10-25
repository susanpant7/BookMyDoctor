using BookMyDoctor.Features.Chats;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Specialties;

namespace BookMyDoctor.Persistence;

public static class StaticData
{
    public static readonly Guid HariPatientId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid MadanPatientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly Guid CardiologyId = Guid.Parse("01111111-1111-1111-1111-111111111111");
    public static readonly Guid DermatologyId = Guid.Parse("02222222-2222-2222-2222-222222222222");
    public static readonly Guid PediatricsId = Guid.Parse("03333333-3333-3333-3333-333333333333");
    public static readonly Guid OrthopedicsId = Guid.Parse("04444444-4444-4444-4444-444444444444");
    public static readonly Guid GeneralPracticeId = Guid.Parse("05555555-5555-5555-5555-555555555555");

    public static readonly Guid CardiologyDoctorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DermatologyDoctorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid PediatricsDoctorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid OrthopedicsDoctorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid GeneralPracticeDoctorId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static IReadOnlyList<UserAccount> Users { get; } =
    [
        new(HariPatientId, "Hari Bahadur", "hb@gmail.com", "susan123"),
        new(MadanPatientId, "Madan Bahadur", "mb@gmail.com", "susan123")
    ];

    public static IReadOnlyList<Specialty> Specialties { get; } =
    [
        new(CardiologyId, "Cardiology", "Heart and blood vessel care"),
        new(DermatologyId, "Dermatology", "Skin, hair, and nail care"),
        new(PediatricsId, "Pediatrics", "Care for infants, children, and teens"),
        new(OrthopedicsId, "Orthopedics", "Bones, joints, and muscles"),
        new(GeneralPracticeId, "General Practice", "Primary care and routine checkups")
    ];

    public static IReadOnlyList<Doctor> Doctors { get; } =
    [
        new(CardiologyDoctorId, "Doctor Cardiology", CardiologyId, "Cardiology", "Austin", 12, 180),
        new(DermatologyDoctorId, "Doctor Dermatology", DermatologyId, "Dermatology", "Dallas", 8, 140),
        new(PediatricsDoctorId, "Doctor Pediatrics", PediatricsId, "Pediatrics", "Houston", 15, 120),
        new(OrthopedicsDoctorId, "Doctor Orthopedics", OrthopedicsId, "Orthopedics", "Austin", 10, 160),
        new(GeneralPracticeDoctorId, "Doctor General Practice", GeneralPracticeId, "General Practice", "Dallas", 6, 90)
    ];

    public static IReadOnlyList<ChatMessage> ChatMessages { get; } =
    [
        new(
            Guid.Parse("c1111111-1111-1111-1111-111111111111"),
            CardiologyDoctorId,
            HariPatientId,
            "patient",
            "Hi doctor, should I keep the same dosage this week?",
            new DateTimeOffset(2026, 10, 20, 9, 15, 0, TimeSpan.FromHours(-5))),
        new(
            Guid.Parse("c2222222-2222-2222-2222-222222222222"),
            CardiologyDoctorId,
            HariPatientId,
            "doctor",
            "Yes, stay on the current dose and we will review it at your visit.",
            new DateTimeOffset(2026, 10, 20, 9, 42, 0, TimeSpan.FromHours(-5)))
    ];
}

public sealed record UserAccount(Guid Id, string Name, string Email, string Password);
