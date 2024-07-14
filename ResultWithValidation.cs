using System.ComponentModel.DataAnnotations;

namespace CustomResultError;

public class ResultWithValidation<T> : Result<T, ValidationResult>
{
    protected ResultWithValidation(T value) : base(value) { }

    protected ResultWithValidation(ValidationResult validationResult) : base(validationResult) { }
}

