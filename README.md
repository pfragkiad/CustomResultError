# CustomResultError

*The modern Result Error types adjusted to modern AOT needs.*

Combine the power of Results and Errors using a "discriminated union" approach. 

## How to install

Via tha Package Manager:
```powershell
Install-Package CustomResultError
```

Via the .NET CLI
```bat
dotnet add package CustomResultError
```

## Error

Generic `Error<CodeType>` inherit from the `Error` base class. The `Error` class is an immutable object which contains the properties `Message (string)` and `Details (string[])`.
For the generic error, the `CodeType` is the type of an additional `Code` property. The errors are considered equal if their corresponding `Code` properties are equal.

```cs
Error<int> e4 = new("mpe", code: 125);
Error<int> e5 = new("mpou", code: 125);
Console.WriteLine(e5); //will print the Message, i.e. "mpe"
Console.WriteLine(e4 == e5); //will print "True" because their codes are the same.

Error<string> e6 = new("mpa", "CODE1");
Error<string> e7 = new("mpampou", "CODE1"); //will return True because their codes are the same.
```

In the AOT world you cannot use the `JsonSerializer` methods, because they use Reflection, which is not allowed. For this reason, the `Error` objects have a `ToJsonString` method which simplifies output especially in the case of Web endpoints.
The `ToString()` overriden method returns only the `Message`. Below are some examples that show how to export a full JSON string:

```cs

//the simplest case 
Error<int> e4 = new("mpe", 125);
Console.WriteLine(e4.ToJsonString());

//we can add sub-errors/details by adding more arguments in the constructor (or by passing a string array)
Error<int> e4a = new("mpe", 125,"suberror1","suberror2");
Console.WriteLine(e4a.ToJsonString());
```

The first case will print:

```json
{
    "code" : 125,
    "message" : "mpe"
}
```

The second case will print:

```json
{
    "code" : 125,
    "message" : "mpe",
    "details" : ["suberror1", "suberror2"]
}
```


### ExceptionError

`ExceptionError` is a special `Error` object that inherit from `Error<Exception>`. For convenience purposes it adds a `domain (string)` before the name of the exception, in order to generate a domain-specific exception code (the `domain` is optional). 
The `Message` property gets its value from the `Exception.Message` property and the `Details` array is populated from internal exception if they exist. The `Code` property itself contains the `Exception` object, so any stack information is preserved. For example:

```cs
Exception e = new InvalidOperationException("bamboo", new OperationCanceledException("mpeeee"));
ExceptionError error = new(e,domain:"MAIN");
Console.WriteLine(error.ToJsonString());
```

will print the following:

```json
{
    "code" : "MAIN.InvalidOperationException",
    "message" : "bamboo",
    "details" : ["mpeeee"]
}
```

## Result

The Result class is designed in a way to behave like a union. A (very) simple example below, shows the implicit conversion from the result type, or the error to a `Result` instance:

```cs
Result<int, Error<string>> result;

int a=5,b=6;
if (a < b)
    result = Result.Ok(a+b);
else
    result = Result.Fail(new Error<string>("This was a bad calc.","App.CalcError"));

//or (due to implicit conversions the code below is equivalent to the code above)
if (a < b)
    result = a+b;
else
    result = new Error<string>("This was a bad calc.","App.CalcError");
```

The `Result` instance contains the `Value` and `Error` properties. A continuation of the previous result is the following:

```cs
IResult res;
if (result.IsSuccess)
    res = Results.Ok(result.Value);
else
    res = Results.BadRequest(result.Error);
```

There are 2 more compact ways to write the same statemens above, using the `Match` function:

```cs
res = result.Match<IResult>( v => Results.Ok(v), e => Results.BadRequest(e));

//or
res = result.Match<IResult>(Results.Ok, Results.BadRequest);

//or  (the IResult return type is implied from the return type of the functions)
res = result.Match(Results.Ok, Results.BadRequest);

```

The `Match` function takes 2 functions (`Func`) are arguments. The first is a function that gets the `Value` and returns an `IResult` and the second functions gets the `Error` and returns a different `IResult`.

The `Switch` function is similar to the `Match` function but takes as arguments functions that do not return any value aka `Action`.
In the example below, the first `Action` happens on success, while the second `Action` happens on failure.

```cs
result.Switch(v => Console.WriteLine($"YES! The value is {v}"),
    e=>Console.WriteLine($"NO! The error is {e}"));
```

## Error parsing and AOT

But wait, what AOT compiling has to do with all these? The problem with AOT, is that reflection is not supported. Methods that support deserialization such as `AsJsonAsync`, will not work. This `Error` class supports parsing without the use of Reflection and therefore IS AOT compatible.

See the two examples below. The jsonString might come from the text response content of an HTTP call:

```cs
Error<int> e1 = new("messsad", 200, "sub1", "sub2");
string jsonString = e1.ToJsonString();
var e1_c = Error<int>.Parse(jsonString); //parsing does not use reflection here
Console.WriteLine(e1==e1_c);//will print True

Error<string> e2 = new(message:"messsad",code: "DSAD.asd", "sub1", "sub2");
jsonString = e2.ToJsonString();

var e2_c = Error<string>.Parse(jsonString); //parsing does not use reflection here
Console.WriteLine(e2 == e2_c); //will print True
```

# The Validator static class

The `CustomResultError.Validator` static class has some methods that combine the features of validation, logging and the `Error` type. Aren't there other libraries that do validation stuff? Of course there are, however, they are NOT AOT friendly. For example, the `FluentValidation` library uses Reflection heavily and therefore cannot be used in AOT apps.
In the following cases, it is practical to use the `Validator` static members globally using the following statement:

```cs
using static CustomResultError.Validator;

//for convenience we add an alias for Errors with code as strings
using ErrorString = CustomResultError.Error<string>;
```

The `Validate` function is a generic function that returns an Error only if the validation fails. If an `ILogger` is passed to the `Validate` function, then the `errorMessageTemplate` message is logged. The message that is passed to the `ILogger` is combined with the `messageArgs` arguments to populate any interpolated values in the `errorMessageTemplate`. The same error message, is also the `Message` property of the `ErrorString` returned instance. If the validation test is passed (based on the `validateFunction`) then `null` is returned. If the passed `logger` is `null` then no logging is done. Note that no logging happens if the value is successfully validated, to avoid verbose outputs.

```cs
static ErrorString? ValidateTaxValue(int valueInPerc, Microsoft.Extensions.Logging.ILogger? logger)
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

The same functionality can be done via the `Fail` method. The above method is same to the method below. Note that the `Fail` function returns a non-nullable `Error` and also passes the error message to the `logger` (if given):

```cs
static ErrorString? ValidateTaxValue2(int valueInPerc, Microsoft.Extensions.Logging.ILogger? logger)
{
    if(valueInPerc < 0 || valueInPerc > 100)
        return Fail(
                logger: logger,
                errorCode: "App.InvalidTax",
                errorMessageTemplate: "The tax value ({value}) is invalid.",
                logTypeIfError: ValidatorLogTypeIfError.Error,
                messageArgs: valueInPerc);
    return null; 
}
```

### MORE EXAMPLES TO FOLLOW ###
