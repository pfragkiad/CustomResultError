using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CustomResultError.FileDependencies;

public abstract class FileValidator
{
    protected readonly ILogger _logger;

    protected FileValidator(ILogger logger)
    {
        _logger = logger;
    }

    public Result<Dependencies, ErrorString> Validate(string filePath)
    {
        if (!File.Exists(filePath))
            return Fail(_logger, $"{nameof(FileValidator)}.filePathNotFound",
                   "The file '{filePath}' does not exist.", filePath);

        try
        {
            string fileContent = File.ReadAllText(filePath);

            var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

            JsonDocument jsonDocument = JsonDocument.Parse(fileContent, options);

            return Validate(filePath, jsonDocument);
        }
        catch (JsonException)
        {
            return Fail(_logger, $"{nameof(FileValidator)}.JsonParseError",
                "Failed to parse JSON file {filePath}.", filePath);
        }
        catch (Exception exception)
        {
            return Fail(_logger, $"{nameof(FileValidator)}.Exception",
                "Exception thrown when loading file '{filePath}': {message}", filePath, exception.Message);
        }
    }

    public Result<JsonElement, ErrorString> CheckJsonProperty(JsonElement parent, string property, string jsonFile)
    {
        if (!parent.TryGetProperty(property, out JsonElement jsonElement))
            return Fail(_logger, $"{nameof(FileValidator)}.Missing{property}",
                               "The JSON file '{jsonFile}' does not contain a '{property}' property.", jsonFile, property);

        return jsonElement;
    }

    //e.g. "Parent/Nested1/Nested2"
    public Result<JsonElement, ErrorString> CheckJsonNestedProperty(JsonElement parent, string nestedProperty, string jsonFile)
    {
        string[] properties = nestedProperty.Split('/');

        JsonElement current = parent;
        for (int i = 0; i < properties.Length; i++)
        {
            var propertyResult = CheckJsonProperty(current , properties[i], jsonFile);
            if (propertyResult.IsFailure) return propertyResult.Error!;

            current= propertyResult.Value!;
        }

        return current;
    }


    protected abstract Result<Dependencies, ErrorString> Validate(string filePath, JsonDocument jsonDocument);

    //e.g. "Parent": { "fileField": "file1.txt"}
    protected Result<SingleFileDependency, ErrorString> CheckFileFieldProperty(JsonElement parent, string fileField, bool isOptional, string jsonFile)
    {
        var fileResult = CheckJsonProperty(parent, fileField, jsonFile);
        if (fileResult.IsFailure) return fileResult.Error!;
        JsonElement file = fileResult.Value!;

        string? filePath = file.GetString();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (!isOptional)
                return Fail(_logger, $"{nameof(FileValidator)}.Empty{fileField}",
                    "The '{fileField}' property in the JSON file '{jsonFile}' is empty.", fileField, jsonFile);

            return SingleFileDependency.Empty(fileField);
        }

        string fullPath = Path.IsPathRooted(filePath) ? filePath :
            Path.Combine(Path.GetDirectoryName(jsonFile)!, filePath);

        if (!File.Exists(fullPath))
            return Fail(_logger, $"{nameof(FileValidator)}.{fileField}NotFound",
                "The '{fileField}' property in the JSON file '{jsonFile}' points to a non-existent file '{filePath}'.", fileField, jsonFile, filePath);

        return new SingleFileDependency
        {
            Name = fileField,
            SingleFile = new SingleFile { FileName = filePath, FullPath = fullPath },
            IsOptional = isOptional
        };
    }

    //e.g. "Parent": { "filesField": [ "file1.txt", "file2.txt"]}
    protected Result<MultipleFilesDependency, ErrorString> CheckFilesFieldProperty(JsonElement parent, string filesField, bool isOptional, string jsonFile)
    {
        var fileResult = CheckJsonProperty(parent, filesField, jsonFile);
        if (fileResult.IsFailure) return fileResult.Error!;
        JsonElement file = fileResult.Value!;

        //get the JsonArray from the JsonElement
        if (!file.ValueKind.Equals(JsonValueKind.Array))
            return Fail(_logger, $"{nameof(FileValidator)}.Invalid{filesField}",
                               "The '{filesField}' property in the JSON file '{jsonFile}' is not an array.", filesField, jsonFile);

        var jsonArray = file.EnumerateArray().ToArray();

        if (jsonArray.Length == 0)
            return Fail(_logger, $"{nameof(FileValidator)}.Empty{filesField}",
                "The '{filesField}' property in the JSON file '{jsonFile}' is empty.", filesField, jsonFile);

        //string[] filePaths = new string[jsonArray.Length];

        List<SingleFile> files = [];

        for (int i = 0; i < jsonArray.Length; i++)
        {
            string? filePath = jsonArray[i].GetString();
            if (string.IsNullOrWhiteSpace(filePath)) //we should not get empty entries typically here
            {
                if (!isOptional)
                    return Fail(_logger, $"{nameof(FileValidator)}.Empty{filesField}",
                        "The '{filesField}' property in the JSON file '{jsonFile}' contains an empty value at index {i}.", filesField, jsonFile, i);
                //we do not add empty entries to file dependencies
                continue;
            }

            string fullPath = Path.IsPathRooted(filePath) ? filePath :
                Path.Combine(Path.GetDirectoryName(jsonFile)!, filePath);

            if (!File.Exists(fullPath))
                return Fail(_logger, $"{nameof(FileValidator)}.{filesField}NotFound",
                    "The '{filesField}' property in the JSON file '{jsonFile}' points to a non-existent file '{filePath}' at index {i}.", filesField, jsonFile, filePath, i);

            //filePaths[i] = filePath;
            files.Add(new SingleFile { FileName = filePath, FullPath = fullPath });
        }

        return new MultipleFilesDependency
        {
            Files = files,
            Name = filesField,
            IsOptional = isOptional
        };
    }

    //e.g. "Parent": { "arrayField": [ "fileField":..]}
    protected Result<List<SingleFileDependency>, ErrorString> CheckPropertyFilesFieldProperty(JsonElement parent, string arrayField, string fileField, bool isOptional, string jsonFile)
    {
        var arrayResult = CheckJsonProperty(parent, arrayField, jsonFile);
        if (arrayResult.IsFailure) return arrayResult.Error!;
        JsonElement array = arrayResult.Value!;

        //get the JsonArray from the JsonElement
        if (!array.ValueKind.Equals(JsonValueKind.Array))
            return Fail(_logger, $"{nameof(FileValidator)}.Invalid{arrayField}",
                               "The '{filesField}' property in the JSON file '{jsonFile}' is not an array.", arrayField, jsonFile);

        var jsonArray = array.EnumerateArray().ToArray();

        if (jsonArray.Length == 0)
            return Fail(_logger, $"{nameof(FileValidator)}.Empty{arrayField}",
                "The '{filesField}' property in the JSON file '{jsonFile}' is empty.", arrayField, jsonFile);

        int filesCount = jsonArray.Length;
        List<SingleFileDependency> files = [];

        //the indexing is used for reporting the error
        for (int i = 0; i < filesCount; i++)
        {
            JsonElement element = jsonArray[i];

            var fileResult = CheckJsonProperty(element, fileField, jsonFile);
            if (fileResult.IsFailure) return fileResult.Error!;
            JsonElement file = fileResult.Value!;

            string? filePath = file.GetString();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                if (!isOptional)
                    return Fail(_logger, $"{nameof(FileValidator)}.Empty{fileField}",
                        "The '{filesField}' property in the JSON file '{jsonFile}' contains an empty value at index {i}.", fileField, jsonFile, i);

                //we do not add empty entries to file dependencies
                continue;
            }

            string fullPath = Path.IsPathRooted(filePath) ? filePath :
                Path.Combine(Path.GetDirectoryName(jsonFile)!, filePath);

            if (!File.Exists(fullPath))
                return Fail(_logger, $"{nameof(FileValidator)}.{fileField}NotFound",
                    "The '{filesField}' property in the JSON file '{jsonFile}' points to a non-existent file '{filePath}' at index {i}.", fileField, jsonFile, filePath, i);

            files.Add(new SingleFileDependency
            {
                Name = fileField,
                SingleFile = new SingleFile { FileName = filePath, FullPath = fullPath },
                IsOptional = isOptional
            });
            //fullPaths[i] = filePath;
        }

        return files;
    }


}
