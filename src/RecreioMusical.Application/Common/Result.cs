namespace RecreioMusical.Application.Common;

/// <summary>Erro de aplicação com código estável para a camada de apresentação.</summary>
public sealed record ResultError(string Code, string Message);

/// <summary>Resultado de um caso de uso, com sucesso ou erro de negócio.</summary>
public class Result
{
    private protected Result(bool isSuccess, ResultError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public ResultError? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(ResultError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }

    public static Result Failure(string code, string message) =>
        Failure(new ResultError(code, message));

    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(ResultError error) => new(error);

    public static Result<T> Failure<T>(string code, string message) =>
        Failure<T>(new ResultError(code, message));
}

/// <summary>Resultado de um caso de uso que produz um valor.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value)
        : base(true, null) => _value = value;

    internal Result(ResultError error)
        : base(false, error)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Um resultado de falha não possui valor.");

    public T ValueOr(T fallback) => IsSuccess ? _value! : fallback;
}
