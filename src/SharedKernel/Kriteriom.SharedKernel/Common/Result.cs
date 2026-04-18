using System.Text.Json.Serialization;

namespace Kriteriom.SharedKernel.Common;

public class Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }

    [JsonConstructor]
    public Result() { }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    private Result(string error, string errorCode)
    {
        IsSuccess = false;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error, string errorCode = "UNKNOWN") => new(error, errorCode);
}
