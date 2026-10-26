using BookMyDoctor.Features.Doctors;
using static BookMyDoctor.Persistence.SeedIds;

namespace BookMyDoctor.Persistence;

public sealed class DoctorsRepo : IDoctorsRepo
{
    internal static readonly IReadOnlyList<Doctor> Seed =
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

    private readonly IReadOnlyList<Doctor> _doctors = Seed;

    public IReadOnlyList<Doctor> GetAll() => _doctors;

    public IReadOnlyList<Doctor> GetBySpecialty(Guid specialtyId) =>
        _doctors.Where(doctor => doctor.SpecialtyId == specialtyId).ToList();

    public Doctor? GetById(Guid id) => _doctors.FirstOrDefault(doctor => doctor.Id == id);
}
