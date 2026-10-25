using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Specialties;

[ApiController]
[Route("api/specialties")]
public sealed class SpecialtiesController : ControllerBase
{
    private readonly ISpecialtyService _specialtyService;

    public SpecialtiesController(ISpecialtyService specialtyService)
    {
        _specialtyService = specialtyService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Specialty>> GetAll() => Ok(_specialtyService.GetAll());
}
