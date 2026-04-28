using czwiczenie7.DTOs;
using czwiczenie7.Enums;
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
            return NotFound(new ErrorResponseDto("Appointment not found"));
        }

        return Ok(appointment);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDetailsDto>> CreateAppointment([FromBody] CreateAppointmentRequestDto dto)
    {
        var result = await _appointmentService.CreateAppointment(dto);
        if (result.Status == ServiceResultStatus.BadRequest)
        {
            return BadRequest(new ErrorResponseDto(result.ErrorMessage));
        }

        if (result.Status == ServiceResultStatus.Conflict)
        {
            return Conflict(new ErrorResponseDto(result.ErrorMessage));
        }

        return CreatedAtAction(
            nameof(GetAppointmentById),
            new { idAppointment = result.Data!.IdAppointment },
            result.Data);
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<ActionResult<AppointmentDetailsDto>> UpdateAppointment(
        int idAppointment,
        [FromBody] UpdateAppointmentRequestDto dto)
    {
        var result = await _appointmentService.UpdateAppointment(idAppointment, dto);
        if (result.Status == ServiceResultStatus.BadRequest)
        {
            return BadRequest(new ErrorResponseDto(result.ErrorMessage));
        }

        if (result.Status == ServiceResultStatus.Conflict)
        {
            return Conflict(new ErrorResponseDto(result.ErrorMessage));
        }

        if (result.Status == ServiceResultStatus.NotFound)
        {
            return NotFound(new ErrorResponseDto(result.ErrorMessage));
        }

        return Ok(result.Data);
    }
}