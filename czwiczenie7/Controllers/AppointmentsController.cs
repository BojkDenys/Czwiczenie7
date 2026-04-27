using czwiczenie7.DTOs;
using czwiczenie7.Services;
using Microsoft.AspNetCore.Mvc;

namespace czwiczenie7.Controllers;
[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _appointmentService;

    public AppointmentsController(AppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AppointmentListDto>>> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        var appointment = await _appointmentService.GetAppointments(status, patientLastName);
        return Ok(appointment);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<ActionResult<AppointmentDetailsDto>> GetAppointmentById(int idAppointment)
    {
        var appointment = await _appointmentService.GetAppointmentById(idAppointment);
        if (appointment == null)
        {
            return NotFound(new ErrorResponseDto());
        }

        return Ok(appointment);
    }
}