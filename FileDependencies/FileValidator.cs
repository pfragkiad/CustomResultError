using Microsoft.Extensions.DependencyInjection;
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

    public Result<Dependencies, ErrorString> Validate(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Fail(_logger, $"{nameof(FileValidator)}.EmptyFilePath",
                               "The file path is empty.");

        if (!File.Exists(filePath))
            return Fail(_logger, $"{nameof(FileValidator)}.FilePathNotFound",
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

    protected Result<JsonElement, ErrorString> CheckJsonProperty(JsonElement parent, string property, string jsonFile)
    {
        if (!parent.TryGetProperty(property, out JsonElement jsonElement))
            return Fail(_logger, $"{nameof(FileValidator)}.Missing{property}",
                               "The JSON file '{jsonFile}' does not contain a '{property}' property.", jsonFile, property);

        return jsonElement;
    }

    protected Result<JsonElement, ErrorString> CheckJsonProperty(JsonDocument document, string nestedProperty, string jsonFile)
    {
        return CheckJsonNestedProperty(document.RootElement,nestedProperty,jsonFile);
    }   
    
    //e.g. "Parent/Nested1/Nested2"
    protected Result<JsonElement, ErrorString> CheckJsonNestedProperty(JsonElement parent, string nestedProperty, string jsonFile)
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

    #region GetSingleFileDependency

    protected Result<SingleFileDependency, ErrorString> CheckFileFieldProperty(JsonDocument document, string nestedFileField, bool isOptional, string jsonFile)
    {
        var fileResult = CheckJsonNestedProperty(document.RootElement, nestedFileField, jsonFile);
        if (fileResult.IsFailure) return fileResult.Error!;
        JsonElement file = fileResult.Value!;

        return GetSingleFileDependency(nestedFileField, isOptional, jsonFile, file);
    }



    //e.g. "Parent": { "fileField": "file1.txt"}
    protected Result<SingleFileDependency, ErrorString> CheckFileFieldProperty(JsonElement parent, string fileField, bool isOptional, string jsonFile)
    {
        var fileResult = CheckJsonProperty(parent, fileField, jsonFile);
        if (fileResult.IsFailure) return fileResult.Error!;
        JsonElement file = fileResult.Value!;

        return GetSingleFileDependency(fileField, isOptional, jsonFile, file);
    }

    private Result<SingleFileDependency, ErrorString> GetSingleFileDependency(string fileField, bool isOptional, string jsonFile, JsonElement file)
    {
        string? filePath = file.GetString();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (!isOptional)
                return Fail(_logger, $"{nameof(FileValidator)}.Empty{fileField}",
                    "The '{fileField}' property in the JSON file '{jsonFile}' is empty.", fileField, jsonFile);

            return SingleFileDependency.Empty(fileField);
        }

        string fullPath = GetFullPath(filePath, jsonFile);

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
    protected Result<MultipleFilesDependency, ErrorString> CheckFilesFieldProperty(JsonDocument document, string nestedFilesField, bool isOptional, string jsonFile)
    {
        var fileResult = CheckJsonProperty(document, nestedFilesField, jsonFile);
        if (fileResult.IsFailure) return fileResult.Error!;
        JsonElement file = fileResult.Value!;

        return GetMultipleFilesDependency(nestedFilesField, isOptional, jsonFile, file);

    }
    #endregion

    #region MultipleFilesDependency

    protected Result<MultipleFilesDependency, ErrorString> CheckFilesFieldProperty(JsonElement parent, string filesField, bool isOptional, string jsonFile)
    {
        var fileResult = CheckJsonProperty(parent, filesField, jsonFile);
        if (fileResult.IsFailure) return fileResult.Error!;
        JsonElement file = fileResult.Value!;

        return GetMultipleFilesDependency(filesField, isOptional, jsonFile, file);
    }

    protected Result<MultipleFilesDependency, ErrorString> GetMultipleFilesDependency(string filesField, bool isOptional, string jsonFile, JsonElement file)
    {
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

            string fullPath = GetFullPath(filePath, jsonFile);

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

    #endregion

    #region List of SingleFileDependencies
    protected Result<List<SingleFileDependency>, ErrorString> CheckPropertyFilesFieldProperty(JsonDocument document, string nestedArrayField, string fileField, bool isOptional, string jsonFile)
    {
        var arrayResult = CheckJsonProperty(document, nestedArrayField, jsonFile);
        if (arrayResult.IsFailure) return arrayResult.Error!;
        JsonElement array = arrayResult.Value!;

        return GetListOfSingleFileDependency(nestedArrayField, fileField, isOptional, jsonFile, array);
    }

    //e.g. "Parent": { "arrayField": [ "fileField":..]}
    protected Result<List<SingleFileDependency>, ErrorString> CheckPropertyFilesFieldProperty(JsonElement parent, string arrayField, string fileField, bool isOptional, string jsonFile)
    {
        var arrayResult = CheckJsonProperty(parent, arrayField, jsonFile);
        if (arrayResult.IsFailure) return arrayResult.Error!;
        JsonElement array = arrayResult.Value!;

        return GetListOfSingleFileDependency(arrayField, fileField, isOptional, jsonFile, array);
    }

    protected Result<List<SingleFileDependency>, ErrorString> GetListOfSingleFileDependency(string arrayField, string fileField, bool isOptional, string jsonFile, JsonElement array)
    {
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
                        "The '{arrayField}/{filesField}' property in the JSON file '{jsonFile}' contains an empty value at index {i}.",arrayField, fileField, jsonFile, i);

                //we do not add empty entries to file dependencies
                continue;
            }

            string fullPath = GetFullPath(filePath, jsonFile);

            if (!File.Exists(fullPath))
                return Fail(_logger, $"{nameof(FileValidator)}.{fileField}NotFound",
                    "The '{arrayField}/{filesField}' property in the JSON file '{jsonFile}' points to a non-existent file '{filePath}' at index {i}.", arrayField, fileField, jsonFile, filePath, i);

            files.Add(new SingleFileDependency
            {
                Name = $"{arrayField}/{fileField}[{i}]",
                SingleFile = new SingleFile { FileName = filePath, FullPath = fullPath },
                IsOptional = isOptional
            });
            //fullPaths[i] = filePath;
        }

        return files;
    }


    protected string GetFullPath(string propertyFilePath, string jsonFile)
    {
        return Path.IsPathRooted(propertyFilePath) ? propertyFilePath :
             Path.Combine(Path.GetDirectoryName(jsonFile)!, propertyFilePath);
    }

    /// <summary>
    /// Retrieves the missing non-empty file or the empty string.
    /// </summary>
    /// <param name="json"></param>
    /// <param name="nestedProperty"></param>
    /// <param name="jsonFile"></param>
    /// <returns></returns>
    protected string? GetMissingFileOrEmpty(JsonDocument json, string nestedProperty, string jsonFile)
    {
        var fileProperty = CheckJsonProperty(json, nestedProperty, jsonFile);
        if (fileProperty.IsFailure) return null;

        JsonElement file = fileProperty.Value!;
        string? propertyFilePath = file.GetString();
        if (string.IsNullOrWhiteSpace(propertyFilePath)) return null;

        string fullPath = GetFullPath(propertyFilePath, jsonFile);
        return File.Exists(fullPath) ? null : propertyFilePath;
    }

    protected  List<string> GetMissingFilesFromArray(JsonDocument json, string nestedArrayField, string fileField, string jsonFile)
    {
        List<string> missingPaths = [];

        var arrayResult = CheckJsonProperty(json, nestedArrayField, jsonFile);
        JsonElement array = arrayResult.Value!;
        JsonElement[] jsonArray = array.EnumerateArray().ToArray();
        foreach (JsonElement jsonElement in jsonArray)
        {
            var fileProperty = CheckJsonProperty(jsonElement, fileField, jsonFile);
            if (fileProperty.IsFailure) continue;

            JsonElement file = fileProperty.Value!;
            string? propertyFilePath = file.GetString();
            if (string.IsNullOrWhiteSpace(propertyFilePath)) continue;

            string fullPath = GetFullPath(propertyFilePath, jsonFile);
            if (!File.Exists(fullPath)) missingPaths.Add(propertyFilePath);
        }

        return missingPaths.ToHashSet().ToList();
    }


    #endregion
}
