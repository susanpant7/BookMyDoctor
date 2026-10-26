namespace BookMyDoctor.Persistence;

// Fixed ids shared by the seed data in each repo, so rows can reference each other (like foreign keys).
public static class SeedIds
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
}
