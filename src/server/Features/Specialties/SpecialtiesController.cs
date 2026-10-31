using BookMyDoctor.Features.Auth;
using BookMyDoctor.Features.Common;
using Microsoft.AspNetCore.Authorization;
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

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public ActionResult<Specialty> Create(SpecialtyRequest request) => Ok(_specialtyService.Create(request));

    [HttpPost("load-defaults")]
    [Authorize(Roles = Roles.Admin)]
    public ActionResult<LoadDefaultsResult> LoadDefaults() => Ok(_specialtyService.LoadDefaults());

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public ActionResult<Specialty> Update(Guid id, SpecialtyRequest request) => Ok(_specialtyService.Update(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Delete(Guid id)
    {
        _specialtyService.Delete(id);
        return NoContent();
    }
}
