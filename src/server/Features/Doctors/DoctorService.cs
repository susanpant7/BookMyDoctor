using BookMyDoctor.Features.Appointments;
using BookMyDoctor.Features.Common;
using BookMyDoctor.Persistence;

namespace BookMyDoctor.Features.Doctors;

public sealed class DoctorService : IDoctorService
{
    private const int MaxNameLength = 100;
    private const int MaxBioLength = 1000;

    private readonly IDoctorsRepo _doctorsRepo;
    private readonly ISpecialtiesRepo _specialtiesRepo;
    private readonly IDoctorSchedulesRepo _schedulesRepo;

    public DoctorService(IDoctorsRepo doctorsRepo, ISpecialtiesRepo specialtiesRepo, IDoctorSchedulesRepo schedulesRepo)
    {
        _doctorsRepo = doctorsRepo;
        _specialtiesRepo = specialtiesRepo;
        _schedulesRepo = schedulesRepo;
    }

    public IReadOnlyList<Doctor> GetAll(Guid? specialtyId = null) =>
        specialtyId is null ? _doctorsRepo.GetAll() : _doctorsRepo.GetBySpecialty(specialtyId.Value);

    public Doctor? GetById(Guid id) => _doctorsRepo.GetById(id);

    public IReadOnlyList<ScheduleBlock> GetSchedule(Guid id)
    {
        if (_doctorsRepo.GetById(id) is null)
        {
            throw new KeyNotFoundException("Doctor not found.");
        }
        return _schedulesRepo.GetWeekly(id).Select(block => new ScheduleBlock(block.Day, block.Start, block.End, block.SlotMinutes)).ToList();
    }

    public Doctor Create(DoctorRequest request)
    {
        var (doctor, schedule) = Check(Guid.NewGuid(), request);
        _doctorsRepo.Add(doctor);
        _schedulesRepo.ReplaceWeekly(doctor.Id, schedule);
        return _doctorsRepo.GetById(doctor.Id)!;
    }

    public Doctor Update(Guid id, DoctorRequest request)
    {
        if (_doctorsRepo.GetById(id) is null)
        {
            throw new KeyNotFoundException("Doctor not found.");
        }

        var (doctor, schedule) = Check(id, request);
        _doctorsRepo.Update(doctor);
        _schedulesRepo.ReplaceWeekly(id, schedule);
        return _doctorsRepo.GetById(id)!;
    }

    public void Delete(Guid id)
    {
        if (_doctorsRepo.GetById(id) is null)
        {
            throw new KeyNotFoundException("Doctor not found.");
        }
        if (_doctorsRepo.HasAppointments(id))
        {
            throw new ConflictException("This doctor has appointments, so they cannot be deleted.");
        }
        _doctorsRepo.Delete(id);
    }

    public LoadDefaultsResult LoadDefaults()
    {
        var added = 0;
        foreach (var doctor in DefaultData.Doctors)
        {
            // Skipped when already there, even if renamed since (same id).
            if (_doctorsRepo.GetById(doctor.Id) is not null || _doctorsRepo.GetByName(doctor.Name) is not null)
            {
                continue;
            }

            var specialty = _specialtiesRepo.GetByName(doctor.Specialty)
                ?? throw new ArgumentException($"The specialty '{doctor.Specialty}' does not exist. Load the default specialties first.");
            _doctorsRepo.Add(doctor with { SpecialtyId = specialty.Id });
            _schedulesRepo.ReplaceWeekly(doctor.Id, DefaultData.Schedules.Where(block => block.DoctorId == doctor.Id).ToList());
            added++;
        }
        return new LoadDefaultsResult(added, DefaultData.Doctors.Count - added);
    }

    private (Doctor Doctor, IReadOnlyList<DoctorSchedule> Schedule) Check(Guid id, DoctorRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            throw new ArgumentException($"The name is required and can be up to {MaxNameLength} characters.");
        }
        if (_doctorsRepo.GetByName(name) is { } existing && existing.Id != id)
        {
            throw new ConflictException("A doctor with this name already exists.");
        }

        var specialty = _specialtiesRepo.GetById(request.SpecialtyId) ?? throw new ArgumentException("Choose an existing specialty.");
        if (request.ExperienceYears is < 0 or > 70)
        {
            throw new ArgumentException("Experience must be from 0 to 70 years.");
        }
        if (request.ConsultationFee is < 0 or > 100_000 || request.FollowUpFee is < 0 or > 100_000)
        {
            throw new ArgumentException("Fees must be from 0 to 100000.");
        }

        var languages = (request.Languages ?? []).Select(language => language.Trim()).Where(language => language.Length > 0).Distinct().ToList();
        if (languages.Count > 10 || languages.Any(language => language.Length > 50 || language.Contains(',')))
        {
            throw new ArgumentException("Give up to 10 languages of up to 50 characters, without commas.");
        }

        var bio = request.Bio?.Trim() ?? "";
        if (bio.Length > MaxBioLength)
        {
            throw new ArgumentException($"The bio can be up to {MaxBioLength} characters.");
        }

        var schedule = CheckSchedule(id, request.Schedule ?? []);
        var doctor = new Doctor(id, name, specialty.Id, specialty.Name, request.ExperienceYears,
            request.ConsultationFee, request.FollowUpFee, languages, bio);
        return (doctor, schedule);
    }

    private static List<DoctorSchedule> CheckSchedule(Guid doctorId, IReadOnlyList<ScheduleBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.SlotMinutes is < 5 or > 240)
            {
                throw new ArgumentException("A slot must be from 5 to 240 minutes long.");
            }
            if (block.End.ToTimeSpan() - block.Start.ToTimeSpan() < TimeSpan.FromMinutes(block.SlotMinutes))
            {
                throw new ArgumentException("Each schedule block must end at least one slot after it starts.");
            }
        }

        foreach (var day in blocks.GroupBy(block => block.Day))
        {
            var ordered = day.OrderBy(block => block.Start).ToList();
            if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.Second.Start < pair.First.End))
            {
                throw new ArgumentException($"The schedule blocks on {day.Key} overlap.");
            }
        }

        return blocks.Select(block => new DoctorSchedule(doctorId, block.Day, block.Start, block.End, block.SlotMinutes)).ToList();
    }
}
