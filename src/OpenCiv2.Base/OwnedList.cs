using System.Collections;

namespace OpenCiv2.Base;

/// <summary>An owned, structurally comparable sequence. No mutable backing storage escapes.</summary>
public sealed class OwnedList<T> : IReadOnlyList<T>, IEquatable<OwnedList<T>>
{
    private readonly T[] values;
    public OwnedList(IEnumerable<T> values) => this.values = values.ToArray();
    public static OwnedList<T> Empty { get; } = new([]);
    public int Count => values.Length;
    public T this[int index] => values[index];
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(OwnedList<T>? other) => other is not null && values.SequenceEqual(other.values);
    public override bool Equals(object? obj) => obj is OwnedList<T> other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (T value in values) hash.Add(value);
        return hash.ToHashCode();
    }
}
