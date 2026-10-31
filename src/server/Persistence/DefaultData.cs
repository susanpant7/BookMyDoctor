using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Doctors;
using BookMyDoctor.Features.Hospital;
using BookMyDoctor.Features.Specialties;
using static BookMyDoctor.Persistence.SeedIds;

namespace BookMyDoctor.Persistence;

// What the admin pages load with "Load defaults", and what the hospital settings start with.
public static class DefaultData
{
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
        new(CardiologyDoctorId, "Doctor Cardiology", CardiologyId, "Cardiology", 12, 180, 120, ["English", "Hindi"],
            "Heart specialist focused on blood pressure, cholesterol and rhythm problems, with a strong interest in preventive care for patients with a family history of heart disease."),
        new(DermatologyDoctorId, "Doctor Dermatology", DermatologyId, "Dermatology", 8, 140, 90, ["English", "Spanish"],
            "Skin, hair and nail specialist who treats acne, eczema and psoriasis and runs yearly full-body skin cancer checks."),
        new(PediatricsDoctorId, "Doctor Pediatrics", PediatricsId, "Pediatrics", 15, 120, 80, ["English", "Nepali"],
            "Children's doctor caring for newborns to teenagers, known for a calm approach with anxious children and first-time parents."),
        new(OrthopedicsDoctorId, "Doctor Orthopedics", OrthopedicsId, "Orthopedics", 10, 160, 100, ["English"],
            "Bone and joint specialist with a sports-medicine background, focused on getting patients back to activity with physiotherapy-led recovery plans."),
        new(GeneralPracticeDoctorId, "Doctor General Practice", GeneralPracticeId, "General Practice", 6, 90, 60, ["English", "Hindi"],
            "Family doctor for everyday illnesses, yearly checkups and long-term conditions like diabetes and thyroid problems; refers patients to specialists when needed.")
    ];

    public static DayOfWeek[] Weekdays =>
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // Weekly hours each default doctor sees patients at the hospital.
    public static IReadOnlyList<DoctorSchedule> Schedules { get; } =
    [
        .. Weekly(CardiologyDoctorId, [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], 9, 16, 30),
        .. Weekly(DermatologyDoctorId, [DayOfWeek.Tuesday, DayOfWeek.Thursday], 10, 18, 20),
        .. Weekly(DermatologyDoctorId, [DayOfWeek.Saturday], 9, 13, 20),
        .. Weekly(PediatricsDoctorId, Weekdays, 8, 15, 20),
        .. Weekly(OrthopedicsDoctorId, [DayOfWeek.Tuesday, DayOfWeek.Thursday], 8, 17, 30),
        .. Weekly(OrthopedicsDoctorId, [DayOfWeek.Saturday], 9, 12, 30),
        .. Weekly(GeneralPracticeDoctorId, [.. Weekdays, DayOfWeek.Saturday], 9, 19, 15)
    ];

    public static IReadOnlyList<(Guid Id, string Name, string Email)> Patients { get; } =
    [
        (HariPatientId, "Hari Bahadur", "hb@gmail.com"),
        (MadanPatientId, "Madan Bahadur", "mb@gmail.com")
    ];

    public static HospitalDetails Hospital { get; } = new(
        new HospitalInfo(
            "BookMyDoctor Hospital",
            "1200 Riverside Medical Plaza, Austin, TX 78741",
            "(512) 555-0140",
            ["ECG room", "X-ray room", "Blood test lab", "Children's play area", "Breastfeeding room"],
            "Free parking in the Riverside Medical Plaza garage for up to 2 hours.",
            "5-minute walk from the Riverside bus stop (routes 7 and 20)."),
        [.. Weekdays.Append(DayOfWeek.Saturday).Select(day => new HospitalHours(day, new TimeOnly(8, 0), new TimeOnly(19, 0)))],
        [
            new(Guid.Empty, "ECG (electrocardiogram)", 45),
            new(Guid.Empty, "X-ray", 60),
            new(Guid.Empty, "Basic blood panel", 35),
            new(Guid.Empty, "Lipid profile (cholesterol)", 40),
            new(Guid.Empty, "Blood sugar (HbA1c) test", 30),
            new(Guid.Empty, "Allergy skin test", 120),
            new(Guid.Empty, "Wart or skin tag removal", 75),
            new(Guid.Empty, "Adult flu shot (September to March)", 25)
        ],
        [
            new(Guid.Empty, "Blue Cross Blue Shield", null),
            new(Guid.Empty, "Aetna", null),
            new(Guid.Empty, "Cigna", null),
            new(Guid.Empty, "UnitedHealthcare", null),
            new(Guid.Empty, "Humana", null),
            new(Guid.Empty, "Medicare", null),
            new(Guid.Empty, "Medicaid", "Children only")
        ]);

    // Hospital-wide days off in a year.
    public static IEnumerable<(DateOnly Date, string Name)> Holidays(int year) =>
    [
        (new DateOnly(year, 1, 1), "New Year's Day"),
        (LastWeekdayOfMonth(year, 5, DayOfWeek.Monday), "Memorial Day"),
        (new DateOnly(year, 7, 4), "Independence Day"),
        (NthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1), "Labor Day"),
        (NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving Day"),
        (new DateOnly(year, 12, 25), "Christmas Day")
    ];

    private static IEnumerable<DoctorSchedule> Weekly(Guid doctorId, DayOfWeek[] days, int startHour, int endHour, int slotMinutes) =>
        days.Select(day => new DoctorSchedule(doctorId, day, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), slotMinutes));

    private static DateOnly NthWeekdayOfMonth(int year, int month, DayOfWeek day, int n)
    {
        var first = new DateOnly(year, month, 1);
        return first.AddDays(((int)day - (int)first.DayOfWeek + 7) % 7 + 7 * (n - 1));
    }

    private static DateOnly LastWeekdayOfMonth(int year, int month, DayOfWeek day)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return last.AddDays(-(((int)last.DayOfWeek - (int)day + 7) % 7));
    }
}
