namespace ExcelETL.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; }

    protected Entity() => Id = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
