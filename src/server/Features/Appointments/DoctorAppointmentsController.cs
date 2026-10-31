using MyHospitalAI.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MyHospitalAI.Features.Appointments;

// The signed-in doctor's own appointments.
[ApiController]
[Authorize(Roles = Roles.Doctor)]
[Route("api/doctor/appointments")]
public sealed class DoctorAppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public DoctorAppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<DoctorAppointment>> GetMine() =>
        Ok(_appointmentService.GetForDoctor(User.GetUserId()));
}
