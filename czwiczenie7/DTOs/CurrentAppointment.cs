namespace czwiczenie7.DTOs;

public class CurrentAppointment
{
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    
}