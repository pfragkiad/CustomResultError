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
            JsonDocument jsonDocument = JsonDocument.Parse(fileContent);

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

    protected abstract Result<Dependencies, ErrorString> Validate(string filePath, JsonDocument jsonDocument);

    //e.g. "Body": { "fileField": "file1.txt"}
    protected Result<SingleFileDependency, ErrorString> CheckFileFieldProperty(JsonElement body, string fileField, bool isOptional, string jsonFile)
    {
        if (!body.TryGetProperty(fileField, out JsonElement file))
            return Fail(_logger, $"{nameof(FileValidator)}.Missing{fileField}",
                "The JSON file '{jsonFile}' does not contain a '{fileField}' property.", jsonFile, fileField);

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

    //e.g. "Body": { "filesField": [ "file1.txt", "file2.txt"]}
    protected Result<MultipleFilesDependency, ErrorString> CheckFilesFieldProperty(JsonElement body, string filesField, bool isOptional, string jsonFile)
    {
        if (!body.TryGetProperty(filesField, out JsonElement file))
            return Fail(_logger, $"{nameof(FileValidator)}.Missing{filesField}",
                "The JSON file '{jsonFile}' does not contain a '{fileField}' property.", jsonFile, filesField);

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

    //e.g. "Body": { "arrayField": [ "fileField":..]}
    protected Result<List<SingleFileDependency>, ErrorString> CheckPropertyFilesFieldProperty(JsonElement body, string arrayField, string fileField, bool isOptional, string jsonFile)
    {
        if (!body.TryGetProperty(arrayField, out JsonElement array))
            return Fail(_logger, $"{nameof(FileValidator)}.Missing{arrayField}",
                "The JSON file '{jsonFile}' does not contain a '{fileField}' property.", jsonFile, arrayField);

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
            if (!element.TryGetProperty(fileField, out JsonElement file))
                return Fail(_logger, $"{nameof(FileValidator)}.Missing{fileField}",
                                       "The JSON file '{jsonFile}' does not contain a '{fileField}' property.", jsonFile, fileField);

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
