namespace CustomResultError.FileDependencies;

public class SingleFile
{
    public string? FileName { get; init; }

    public string? FullPath { get; init; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(FileName);

    public override string ToString() => FileName ?? string.Empty;

}
