namespace ExcelETL.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; }

    protected Entity() => Id = Guid.NewGuid();

    // For reconstructing an existing entity under its original Id (e.g. an edited aggregate being
    // saved back over the record it started from) -- as opposed to the parameterless constructor
    // above, which always mints a fresh identity for a genuinely new entity.
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id must not be empty.", nameof(id));
        }

        Id = id;
    }

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
