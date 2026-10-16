namespace TaskFlow.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; private set; }
    public string? Error { get; private set; }
    public int? StatusCode { get; private set; }

    public static Result Success() => new() { Succeeded = true };
    public static Result Failure(string error, int statusCode = 400) => new() { Succeeded = false, Error = error, StatusCode = statusCode };
}

public class Result<T>
{
    public bool Succeeded { get; private set; }
    public T? Value { get; private set; }
    public string? Error { get; private set; }
    public int? StatusCode { get; private set; }

    public static Result<T> Success(T value) => new() { Succeeded = true, Value = value };
    public static Result<T> Failure(string error, int statusCode = 400) => new() { Succeeded = false, Error = error, StatusCode = statusCode };
    public static Result<T> NotFound(string error = "Resource not found") => new() { Succeeded = false, Error = error, StatusCode = 404 };
}
