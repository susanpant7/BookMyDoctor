namespace MyHospitalAI.Persistence.Entities;

public class DoctorEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int ExperienceYears { get; set; }
    public decimal ConsultationFee { get; set; }
    public decimal FollowUpFee { get; set; }

    /// <summary>Comma-separated, for example "English,Hindi".</summary>
    public string Languages { get; set; } = "";

    public string Bio { get; set; } = "";

    /// <summary>What the doctor signs in with, e.g. "muturog". Null when the doctor has no login.</summary>
    public string? Username { get; set; }

    public string? PasswordHash { get; set; }

    public Guid SpecialtyId { get; set; }
    public SpecialtyEntity Specialty { get; set; } = null!;

    public ICollection<DoctorScheduleEntity> Schedule { get; set; } = [];
}
