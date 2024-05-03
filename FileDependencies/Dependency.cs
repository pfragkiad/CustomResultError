namespace CustomResultError.FileDependencies;

public abstract class Dependency
{
    public required string Name { get; init; }

    public bool IsOptional { get; init; } = true;

    public override string ToString() => Name;

    public abstract bool IsEmpty { get; }
}
