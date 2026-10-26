using BookMyDoctor.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookMyDoctor.Features.Appointments;

[ApiController]
[Authorize]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<Appointment>> GetMine() =>
        Ok(_appointmentService.GetForPatient(User.GetUserId()));

    [HttpPost]
    public ActionResult<Appointment> Book(BookAppointmentRequest request)
    {
        try
        {
            return Ok(_appointmentService.Book(User.GetUserId(), request));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
