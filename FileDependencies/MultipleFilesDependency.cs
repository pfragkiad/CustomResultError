using System.Collections;

namespace CustomResultError.FileDependencies;

public class MultipleFilesDependency : Dependency, IEnumerable<SingleFile>
{
    public List<SingleFile> Files { get; init; } = [];

    public override bool IsEmpty => (Files?.Count ?? 0) == 0;

    public IEnumerator<SingleFile> GetEnumerator()
    {
        return ((IEnumerable<SingleFile>)Files).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Files).GetEnumerator();
    }

    public static MultipleFilesDependency Empty(string name) => new() { Name = name, IsOptional = true };
}
