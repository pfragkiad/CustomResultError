namespace CustomResultError.FileDependencies;

public class SingleFileDependency : Dependency
{
    public SingleFile? SingleFile { get; init; }

    public string? FileName => SingleFile?.FileName;

    public string? FullPath => SingleFile?.FullPath;


    public override bool IsEmpty => string.IsNullOrWhiteSpace(FileName);

    public static SingleFileDependency Empty(string name) => new() { Name = name, IsOptional = true };
}
