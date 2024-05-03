namespace CustomResultError.FileDependencies;

public class Dependencies
{
    public required string Name { get; init; }

    public override string ToString() => Name;

    public List<MultipleFilesDependency> MultipleFiles { get; init; } = [];

    public List<SingleFileDependency> SingleFiles { get; init; } = [];

    public List<Dependencies> Subdependencies { get; init; } = [];

    public MultipleFilesDependency? GetMultipleFilesDependency(string name) => MultipleFiles.FirstOrDefault(f => f.Name == name);
    public SingleFileDependency? GetSingleFileDependency(string name) => SingleFiles.FirstOrDefault(f => f.Name == name);
    public Dependencies? GetSubdependency(string name) => Subdependencies.FirstOrDefault(f => f.Name == name);


}
