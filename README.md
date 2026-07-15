# CustomResultError

*The modern Result/Error types adjusted to modern AOT needs.*

Combine the power of Results and Errors using a "discriminated union" approach. 

## How to install

Via the Package Manager:
```powershell
Install-Package CustomResultError
```

Via the .NET CLI:
```bat
dotnet add package CustomResultError
```

## Error

Generic `Error<CodeType>` inherits from the `Error` base class. The `Error` class is an immutable object which contains the properties `Message (string)` and `Details (string[])`.
For the generic error, `CodeType` is the type of an additional `Code` property. Errors are considered equal if their corresponding `Code` properties are equal.

```cs
Error<int> e4 = new("mpe", code: 125);
Error<int> e5 = new("mpou", code: 125);
Console.WriteLine(e5);       // prints "mpou"
Console.WriteLine(e4 == e5); // prints "True" because their codes are the same

Error<string> e6 = new("mpa", "CODE1");
Error<string> e7 = new("mpampou", "CODE1");
Console.WriteLine(e6 == e7); // prints "True" because their codes are the same
```

In the AOT world you cannot use `JsonSerializer` methods because they rely on Reflection. For this reason, `Error` objects expose a `ToJsonString()` method which simplifies output especially in the case of Web endpoints.
The `ToString()` overridden method returns only the `Message`. Below are examples of how to produce a full JSON string:

```cs
// simplest case
Error<int> e4 = new("mpe", 125);
Console.WriteLine(e4.ToJsonString());

// with sub-errors/details (pass extra strings or a string array)
Error<int> e4a = new("mpe", 125, "suberror1", "suberror2");
Console.WriteLine(e4a.ToJsonString());
```

The first case prints:

```json
{
    "code": 125,
    "message": "mpe"
}
```

The second case prints:

```json
{
    "code": 125,
    "message": "mpe",
    "details": ["suberror1", "suberror2"]
}
```


### ExceptionError

`ExceptionError` is a special `Error` that inherits from `Error<Exception>`. For convenience it prepends an optional `domain (string)` to the exception type name to form a domain-specific error code.
The `Message` property is taken from `Exception.Message`, and the `Details` array is populated from inner exceptions if they exist. The `Code` property holds the full `Exception` object, so stack information is preserved.

```cs
Exception e = new InvalidOperationException("bamboo", new OperationCanceledException("mpeeee"));
ExceptionError error = new(e, domain: "MAIN");
Console.WriteLine(error.ToJsonString());
```

Output:

```json
{
    "code": "MAIN.InvalidOperationException",
    "message": "bamboo",
    "details": ["mpeeee"]
}
```

## Result

The `Result` class is designed to behave like a discriminated union. A simple example below shows implicit conversion from a value or an error to a `Result` instance:

```cs
Result<int, Error<string>> result;

int a = 5, b = 6;
if (a < b)
    result = Result.Ok(a + b);
else
    result = Result.Fail(new Error<string>("This was a bad calc.", "App.CalcError"));

// equivalent, using implicit conversions
if (a < b)
    result = a + b;
else
    result = new Error<string>("This was a bad calc.", "App.CalcError");
```

The `Result` instance exposes `Value` and `Error` properties:

```cs
IResult res;
if (result.IsSuccess)
    res = Results.Ok(result.Value);
else
    res = Results.BadRequest(result.Error);
```

There are more compact ways to express the same logic using the `Match` function:

```cs
res = result.Match<IResult>(v => Results.Ok(v), e => Results.BadRequest(e));

// or using method groups
res = result.Match<IResult>(Results.Ok, Results.BadRequest);

// or with inferred return type
res = result.Match(Results.Ok, Results.BadRequest);
```

`Match` takes two `Func` arguments: the first maps the `Value` to a result, the second maps the `Error` to a result.

The `Switch` function is similar to `Match` but takes `Action` arguments (no return value).
The first `Action` is called on success, the second on failure:

```cs
result.Switch(
    v => Console.WriteLine($"YES! The value is {v}"),
    e => Console.WriteLine($"NO! The error is {e}"));
```

## Error parsing and AOT

The problem with AOT is that Reflection is not supported. Methods such as `AsJsonAsync` will not work. The `Error` class supports parsing without Reflection and is therefore AOT-compatible.

The examples below show round-trip serialization and parsing. The `jsonString` might come from the text response content of an HTTP call:

```cs
Error<int> e1 = new("messsad", 200, "sub1", "sub2");
string jsonString = e1.ToJsonString();
var e1_c = Error<int>.Parse(jsonString); // no Reflection used
Console.WriteLine(e1 == e1_c); // prints "True"

Error<string> e2 = new(message: "messsad", code: "DSAD.asd", "sub1", "sub2");
jsonString = e2.ToJsonString();
var e2_c = Error<string>.Parse(jsonString); // no Reflection used
Console.WriteLine(e2 == e2_c); // prints "True"
```

## The Validator static class

The `CustomResultError.Validator` static class combines validation, logging and the `Error` type in one place. Other validation libraries (e.g. `FluentValidation`) rely heavily on Reflection and cannot be used in AOT apps — `Validator` can.

It is practical to import the static members globally:

```cs
using static CustomResultError.Validator;

// convenient alias for string-coded errors
using ErrorString = CustomResultError.Error<string>;
```

### Validate

`Validate` is a generic function that returns an `Error` only when validation fails. If an `ILogger` is supplied, the error message template is logged using structured logging. The same message becomes the `Message` property of the returned error. `null` is returned on success; no logging is done on success to avoid verbose output.

```cs
static ErrorString? ValidateTaxValue(int valueInPerc, ILogger? logger)
{
    return Validate(
        value: valueInPerc,
        validateFunction: p => p >= 0 && p <= 100,
        logger: logger,
        errorCode: "App.InvalidTax",
        errorMessageTemplate: "The tax value ({value}) is invalid.",
        logTypeIfError: ValidatorLogTypeIfError.Error,
        messageArgs: valueInPerc);
}
```

### Fail

`Fail` returns a non-nullable `Error` and logs the message to the `ILogger` (if given). The method below is equivalent to the `Validate` example above:

```cs
static ErrorString? ValidateTaxValue(int valueInPerc, ILogger? logger)
{
    if (valueInPerc < 0 || valueInPerc > 100)
        return Fail(
            logger: logger,
            errorCode: "App.InvalidTax",
            errorMessageTemplate: "The tax value ({value}) is invalid.",
            logTypeIfError: ValidatorLogTypeIfError.Error,
            messageArgs: valueInPerc);

    return null;
}
```

### Chaining multiple validations

A common pattern is to chain validations and return on the first failure:

```cs
static ErrorString? ValidateOrder(int quantity, decimal price, ILogger? logger)
{
    return
        Validate(quantity, q => q > 0, logger, "App.InvalidQuantity",
            "The quantity ({value}) must be positive.", ValidatorLogTypeIfError.Warning, quantity) ??
        Validate(price, p => p >= 0, logger, "App.InvalidPrice",
            "The price ({value}) cannot be negative.", ValidatorLogTypeIfError.Warning, price);
}
```

Because `Validate` returns `null` on success, the `??` operator chains validations and short-circuits on the first error.

---

## The FileDependencies namespace

`CustomResultError.FileDependencies` provides an AOT-friendly system for validating JSON configuration files that reference external files on disk. It integrates directly with `Result` and `Error` so all validation failures are surfaced as typed errors.

### Core types

| Type | Description |
|---|---|
| `SingleFile` | Holds a `FileName` (relative) and `FullPath` (absolute) for a single file on disk. |
| `Dependency` | Abstract base with `Name`, `IsOptional` and `IsEmpty`. |
| `SingleFileDependency` | A named `Dependency` wrapping a `SingleFile`. |
| `MultipleFilesDependency` | A named `Dependency` wrapping a `List<SingleFile>`, implements `IEnumerable<SingleFile>`. |
| `Dependencies` | Composite container holding lists of `SingleFileDependency`, `MultipleFilesDependency`, and nested `Dependencies`. |
| `FileValidator` | Abstract base class — inherit this to build a validator for your own JSON config format. |

### FileValidator

Inherit `FileValidator` and implement the single abstract method `Validate(string filePath, JsonDocument jsonDocument)`. The public entry point `Validate(string? filePath)` handles reading, JSON-parsing and common error cases for you, then delegates to your implementation.

```cs
using CustomResultError.FileDependencies;

public class AppConfigValidator : FileValidator
{
    public AppConfigValidator(ILogger logger) : base(logger) { }

    protected override Result<Dependencies, ErrorString> Validate(string filePath, JsonDocument json)
    {
        // resolve a required single-file field: { "model": "weights/model.bin" }
        var modelResult = CheckFileFieldProperty(json.RootElement, "model", isOptional: false, filePath);
        if (modelResult.IsFailure) return modelResult.Error!;

        // resolve a required array of files: { "inputs": ["a.csv", "b.csv"] }
        var inputsResult = CheckFilesFieldProperty(json.RootElement, "inputs", isOptional: false, filePath);
        if (inputsResult.IsFailure) return inputsResult.Error!;

        return new Dependencies
        {
            Name = filePath,
            SingleFiles = [modelResult.Value!],
            MultipleFiles = [inputsResult.Value!]
        };
    }
}
```

Using the validator:

```cs
var validator = new AppConfigValidator(logger);
var result = validator.Validate("config.json");

result.Switch(
    deps =>
    {
        string modelPath = deps.GetSingleFileDependency("model")!.FullPath!;
        foreach (SingleFile input in deps.GetMultipleFilesDependency("inputs")!)
            Console.WriteLine(input.FullPath);
    },
    error => Console.WriteLine($"Config error: {error.ToJsonString()}"));
```

### Navigating nested JSON

Use `/`-separated paths to reach nested properties without manual traversal:

```cs
// JSON: { "training": { "data": { "labels": "labels.csv" } } }
var labelsResult = CheckFileFieldProperty(json, "training/data/labels", isOptional: false, filePath);
```

### Resolving an array of objects with a file field

When the config contains an array of objects each with a file field:

```cs
// JSON: { "stages": [ { "script": "step1.py" }, { "script": "step2.py" } ] }
var stagesResult = CheckPropertyFilesFieldProperty(json.RootElement, "stages", "script", isOptional: false, filePath);
if (stagesResult.IsFailure) return stagesResult.Error!;

List<SingleFileDependency> scripts = stagesResult.Value!;
foreach (var dep in scripts)
    Console.WriteLine($"{dep.Name} -> {dep.FullPath}");
```

### Probing for missing files without errors

Use the helper methods when you want to report missing files without failing the whole validation:

```cs
// returns the path string if the file is missing, null if it exists or the property is absent
string? missing = GetMissingFileOrEmpty(json, "optional/asset", filePath);
if (missing is not null)
    Console.WriteLine($"Warning: '{missing}' not found.");

// returns all missing paths from an array of objects
List<string> missingInputs = GetMissingFilesFromArray(json, "inputs", "path", filePath);
foreach (var m in missingInputs)
    Console.WriteLine($"Missing input: {m}");
```

### Relative and absolute paths

All file resolution is handled automatically by `GetFullPath`: relative paths in the JSON are resolved relative to the JSON file's own directory, while absolute paths are used as-is. This means configs are portable regardless of the working directory.
