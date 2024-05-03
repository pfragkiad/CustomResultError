namespace CustomResultError;


public class Result<T> : Result<T, Error>
{
    protected Result(T value) : base(value) { }

    protected Result(Error error) : base(error) { }
}


public class Result<T, E>
{
    public T? Value { get; }
    public E? Error { get; }

    protected Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    protected Result(E error)
    {
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; protected set; }
    public bool IsFailure => !IsSuccess;

    public static Result<T, E> Ok(T value)
    {
        return new Result<T, E>(value);
    }

    public static Result<T, E> Fail(E error)
    {
        return new Result<T, E>(error);
    }

    public static implicit operator Result<T, E>(T value)
    {
        return new(value);
    }
    public static implicit operator Result<T, E>(E error)
    {
        return new(error);
    }


    public static implicit operator T?(Result<T, E> r)
    {
        return r.Value!;
    }

    public static implicit operator E?(Result<T, E> r)
    {
        return r.Error!;
    }

    public TResult Match<TResult>(Func<T, TResult> successFunc, Func<E, TResult> failFunc) =>
        IsSuccess ? successFunc(Value!) : failFunc(Error!);

    public void Switch(Action<T> successFunc, Action<E> failFunc)
    {
        if (IsSuccess) successFunc(Value!); else failFunc(Error!);
    }
}

