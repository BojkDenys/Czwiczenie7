using System.Data;
using czwiczenie7.DTOs;
using Microsoft.Data.SqlClient;

namespace czwiczenie7.Services;

public class AppointmentService
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new Exception("ConnectionString not found");
    }

    public async Task<List<AppointmentListDto>> GetAppointments(string? status, string? patientLastName)
    {
        var appointments = new List<AppointmentListDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
                                                 SELECT
                                                     a.IdAppointment,
                                                     a.AppointmentDate,
                                                     a.Status,
                                                     a.Reason,
                                                     p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                     p.Email AS PatientEmail
                                                 FROM dbo.Appointments a
                                                 JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                                 WHERE (@Status IS NULL OR a.Status = @Status)
                                                   AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                                                 ORDER BY a.AppointmentDate;
                                                 """,
            connection
        );
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
            string.IsNullOrWhiteSpace(status) ? DBNull.Value : status;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 30).Value =
            string.IsNullOrWhiteSpace(patientLastName) ? DBNull.Value : patientLastName;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }

        return appointments;
    }

    public async Task<AppointmentDetailsDto> GetAppointmentById(int appointmentId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
                                                 SELECT
                                                     a.IdAppointment,
                                                     a.AppointmentDate,
                                                     a.Status,
                                                     a.Reason,
                                                     a.InternalNotes,
                                                     a.CreatedAt,
                                                     p.IdPatient,
                                                     p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                     p.Email AS PatientEmail,
                                                     p.PhoneNumber AS PatientPhoneNumber,
                                                     d.IdDoctor,
                                                     d.FirstName + N' ' + d.LastName AS DoctorFullName,
                                                     d.LicenseNumber,
                                                     s.Name AS SpecializationName
                                                 FROM dbo.Appointments a
                                                 JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                                 JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                                                 JOIN dbo.Specializations s ON s.IdSpecialization = d.IdSpecialization
                                                 WHERE a.IdAppointment = @IdAppointment
                                                 """,
            connection
        );
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = appointmentId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes"))
                ? null
                : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            IdPatient = reader.GetInt32(reader.GetOrdinal("IdPatient")),
            PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
            PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            PatientPhoneNumber = reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),
            IdDoctor = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
            DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
            LicenseNumber = reader.GetString(reader.GetOrdinal("LicenseNumber")),
            SpecializationName = reader.GetString(reader.GetOrdinal("SpecializationName"))
        };
    }

    public async Task<ServiceResult<AppointmentDetailsDto>> CreateAppointment(CreateAppointmentRequestDto dto)
    {
        if (dto.AppointmentDate <= DateTime.Now)
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Appointment can not be in past");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Reason must be present");
        }

        if (dto.Reason.Length > 250)
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Reason can not be longer than 250");
        }
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        if (!await PatientExists(connection,dto.IdPatient))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Invalid patient");
        }
        if (!await DoctorExists(connection,dto.IdDoctor))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Invalid doctor");
        }

        if (await DoctorNotFreeAtThisTime(connection,dto.IdDoctor,dto.AppointmentDate,null))
        {
            return ServiceResult<AppointmentDetailsDto>.Conflict("Doctor don't free at this time");
        }

        await using var command = new SqlCommand("""
                                                 INSERT INTO  dbo.Appointments
                                                 (IdPatient, IdDoctor, AppointmentDate, Status, Reason, InternalNotes, CreatedAt)
                                                 OUTPUT INSERTED.IdAppointment
                                                 VALUES(@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason, NULL, SYSUTCDATETIME());
                                                 """, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
        var id = (int)await command.ExecuteScalarAsync();
        var appointment = await GetAppointmentById(id);
        return ServiceResult<AppointmentDetailsDto>.Ok(appointment);

    }
    public async Task<ServiceResult<AppointmentDetailsDto>> UpdateAppointment(int idAppointment,
        UpdateAppointmentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Reason must be present");
        }

        if (dto.Reason.Length > 250)
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Reason can not be longer than 250");
        }

        var allowedStatuses = new[] { "Scheduled", "Completed", "Cancelled" };
        if (!allowedStatuses.Contains(dto.Status))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Invalid Status");
        }
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var currentAppointment = await GetCurrentAppointment(connection, idAppointment);
        if (currentAppointment == null)
        {
            return ServiceResult<AppointmentDetailsDto>.NotFound("Appointment not found");
        }
        if (!await PatientExists(connection,dto.IdPatient))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Invalid patient");
        }
        if (!await DoctorExists(connection,dto.IdDoctor))
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest("Invalid doctor");
        }
        if (currentAppointment.Status == "Completed" &&
            currentAppointment.AppointmentDate != dto.AppointmentDate)
        {
            return ServiceResult<AppointmentDetailsDto>.Conflict(
                "appointment already completed");
        }

        if (dto.AppointmentDate <= DateTime.Now && dto.Status == "Scheduled")
        {
            return ServiceResult<AppointmentDetailsDto>.BadRequest(
                "appointment  cannot be in the past.");
        }


        if (await DoctorNotFreeAtThisTime(connection,dto.IdDoctor,dto.AppointmentDate,idAppointment))
        {
            return ServiceResult<AppointmentDetailsDto>.Conflict("Doctor don't free at this time");
        }

        await using var command = new SqlCommand("""
                                                 UPDATE dbo.Appointments
                                                 SET
                                                 IdPatient = @IdPatient, 
                                                 IdDoctor = @IdDoctor, 
                                                 AppointmentDate = @AppointmentDate, 
                                                 Status = @Status, 
                                                 Reason = @Reason, 
                                                 InternalNotes = @InternalNotes 
                                                 WHERE IdAppointment = @IdAppointment;
                                                 """, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = dto.Status;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
        command.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value =
            string.IsNullOrWhiteSpace(dto.InternalNotes) ? DBNull.Value : dto.InternalNotes;
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        await command.ExecuteNonQueryAsync();
        var appointment = await GetAppointmentById(idAppointment);
        return ServiceResult<AppointmentDetailsDto>.Ok(appointment);

    }

    private async Task<bool> PatientExists(SqlConnection connection, int patientId)
    {
        await using var command = new SqlCommand("""
                                                 SELECT COUNT(1)
                                                 FROM dbo.Patients
                                                 WHERE IdPatient = @IdPatient
                                                 AND IsActive = 1;

                                                 """, connection);
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = patientId;
        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;
    }
    private async Task<bool> DoctorExists(SqlConnection connection, int doctorId)
    {
        await using var command = new SqlCommand("""
                                                 SELECT COUNT(1)
                                                 FROM dbo.Doctors
                                                 WHERE IdDoctor = @IdDoctor
                                                 AND IsActive = 1;

                                                 """, connection);
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = doctorId;
        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;
    }

    private async Task<bool> DoctorNotFreeAtThisTime(
        SqlConnection connection,
        int idDoctor,
        DateTime appointmentDate,
        int? currentAppointmentId)
    {
        await using var command = new SqlCommand("""
                                                 SELECT COUNT(1)
                                                 FROM dbo.Appointments
                                                 WHERE IdDoctor = @IdDoctor
                                                 AND AppointmentDate = @AppointmentDate
                                                 AND Status = N'Scheduled'
                                                 AND (@CurrentAppointmentId IS NULL OR IdAppointment <> @CurrentAppointmentId)
                                                 """, connection);
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointmentDate;
        command.Parameters.Add("@CurrentAppointmentId", SqlDbType.Int).Value =
            currentAppointmentId.HasValue ? currentAppointmentId.Value : DBNull.Value;
        var result = (int)await command.ExecuteScalarAsync();
        return result > 0;

    }

    private async Task<CurrentAppointment?> GetCurrentAppointment(SqlConnection connection, int idAppointment)
    {
        await using var command = new SqlCommand("""
                                                 SELECT IdAppointment, AppointmentDate,Status
                                                 FROM dbo.Appointments
                                                 WHERE IdAppointment = @IdAppointment;
                                                 """, connection);
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new CurrentAppointment
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status"))
        };
    }
    
}