using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Doctors;

[ApiController]
[Route("api/doctors")]
public sealed class DoctorsController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Doctor>> GetAll([FromQuery] Guid? specialtyId) =>
        Ok(_doctorService.GetAll(specialtyId));

    [HttpGet("{id:guid}")]
    public ActionResult<Doctor> GetById(Guid id)
    {
        var doctor = _doctorService.GetById(id);
        return doctor is null ? NotFound() : Ok(doctor);
    }
}
