using BookMyDoctor.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BookMyDoctor.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SpecialtyEntity> Specialties => Set<SpecialtyEntity>();
    public DbSet<DoctorEntity> Doctors => Set<DoctorEntity>();
    public DbSet<DoctorScheduleEntity> DoctorSchedules => Set<DoctorScheduleEntity>();
    public DbSet<TimeOffEntity> TimeOff => Set<TimeOffEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AppointmentEntity> Appointments => Set<AppointmentEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<HospitalInfoEntity> HospitalInfo => Set<HospitalInfoEntity>();
    public DbSet<HospitalHoursEntity> HospitalHours => Set<HospitalHoursEntity>();
    public DbSet<ServicePriceEntity> ServicePrices => Set<ServicePriceEntity>();
    public DbSet<InsurancePlanEntity> InsurancePlans => Set<InsurancePlanEntity>();
    public DbSet<KnowledgeChunkEntity> KnowledgeChunks => Set<KnowledgeChunkEntity>();
    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    // SQLite cannot compare DateTimeOffset values in a query. Stored as UTC ticks, they compare as plain numbers.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpecialtyEntity>(entity =>
        {
            entity.Property(specialty => specialty.Name).UseCollation("NOCASE");
            entity.HasIndex(specialty => specialty.Name).IsUnique();
        });

        modelBuilder.Entity<DoctorEntity>(entity =>
        {
            entity.HasOne(doctor => doctor.Specialty).WithMany(specialty => specialty.Doctors)
                .HasForeignKey(doctor => doctor.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(doctor => doctor.Schedule).WithOne()
                .HasForeignKey(block => block.DoctorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimeOffEntity>(entity =>
        {
            entity.HasOne<DoctorEntity>().WithMany()
                .HasForeignKey(timeOff => timeOff.DoctorId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(timeOff => new { timeOff.DoctorId, timeOff.Date });
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.Property(user => user.Email).UseCollation("NOCASE");
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<AppointmentEntity>(entity =>
        {
            entity.Property(appointment => appointment.Status).HasConversion<string>();
            entity.HasOne<DoctorEntity>().WithMany()
                .HasForeignKey(appointment => appointment.DoctorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserEntity>().WithMany()
                .HasForeignKey(appointment => appointment.PatientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(appointment => new { appointment.DoctorId, appointment.StartsAt });
            entity.HasIndex(appointment => appointment.PatientId);
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasOne<UserEntity>().WithMany()
                .HasForeignKey(message => message.PatientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(message => new { message.PatientId, message.SentAt });
        });

        modelBuilder.Entity<HospitalInfoEntity>().Property(info => info.Id).ValueGeneratedNever();
        modelBuilder.Entity<HospitalHoursEntity>().HasKey(hours => hours.Day);
        modelBuilder.Entity<KnowledgeChunkEntity>().HasIndex(chunk => chunk.Source);
        modelBuilder.Entity<SettingEntity>().HasKey(setting => setting.Key);
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
}
