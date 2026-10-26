using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Appointments;

[ApiController]
[Route("api/doctors/{doctorId:guid}/slots")]
public sealed class DoctorSlotsController : ControllerBase
{
    private readonly IAvailabilityService _availabilityService;

    public DoctorSlotsController(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<TimeSlot>> GetAvailable(Guid doctorId, [FromQuery] DateOnly? from, [FromQuery] int days = 7)
    {
        try
        {
            return Ok(_availabilityService.GetAvailableSlots(doctorId, from, days));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
