using BookMyDoctor.Features.Auth;
using BookMyDoctor.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Hospital;

[ApiController]
[Route("api/hospital")]
public sealed class HospitalController : ControllerBase
{
    private readonly IHospitalRepo _hospitalRepo;
    private readonly IInsurancePlansRepo _insurancePlansRepo;

    public HospitalController(IHospitalRepo hospitalRepo, IInsurancePlansRepo insurancePlansRepo)
    {
        _hospitalRepo = hospitalRepo;
        _insurancePlansRepo = insurancePlansRepo;
    }

    [HttpGet]
    public ActionResult<HospitalDetails> Get() => Ok(Load());

    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    public ActionResult<HospitalDetails> Update(HospitalDetails details)
    {
        var info = details.Info;
        if (string.IsNullOrWhiteSpace(info.Name) || info.Name.Length > 100)
        {
            return BadRequest(new { message = "The hospital name is required and can be up to 100 characters." });
        }
        if (details.Hours.GroupBy(hours => hours.Day).Any(day => day.Count() > 1) || details.Hours.Any(hours => hours.Closes <= hours.Opens))
        {
            return BadRequest(new { message = "Each day can appear once, and must close after it opens." });
        }
        if (details.ServicePrices.Any(price => string.IsNullOrWhiteSpace(price.Name) || price.Price < 0) ||
            details.InsurancePlans.Any(plan => string.IsNullOrWhiteSpace(plan.Name)))
        {
            return BadRequest(new { message = "Every test and insurance plan needs a name, and prices cannot be negative." });
        }

        _hospitalRepo.Save(details with
        {
            Info = info with { Facilities = [.. info.Facilities.Select(facility => facility.Trim()).Where(facility => facility.Length > 0)] }
        });
        return Ok(Load());
    }

    private HospitalDetails Load() =>
        new(_hospitalRepo.GetInfo(), _hospitalRepo.GetHours(), _hospitalRepo.GetServicePrices(), _insurancePlansRepo.GetAll());
}
