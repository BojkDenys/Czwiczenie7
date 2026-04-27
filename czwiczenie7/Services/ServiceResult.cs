using czwiczenie7.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace czwiczenie7.Services;

public class ServiceResult<T>
{
    public ServiceResultStatus Status { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }

    public static ServiceResult<T> Ok(T data) => new()
    {
        Status = ServiceResultStatus.Success,
        Data = data
    };

    public static ServiceResult<T> BadRequest(string message) => new()
    {
        Status = ServiceResultStatus.BadRequest,
        ErrorMessage = message
    };

    public static ServiceResult<T> NotFound(string message) => new()
    {
        Status = ServiceResultStatus.NotFound,
        ErrorMessage = message
    };

    public static ServiceResult<T> Conflict(string message) => new()
    {
        Status = ServiceResultStatus.Conflict,
        ErrorMessage = message
    };
}