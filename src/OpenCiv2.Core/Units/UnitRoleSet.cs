namespace OpenCiv2.Core.Units;

/// <summary>U04/A05 role membership: actual signed roles share slots modulo 32.</summary>
public readonly record struct UnitRoleSet(uint Slots)
{
    public bool Contains(int role) => (Slots & (1u << (role & 31))) != 0;
    public UnitRoleSet With(int role) => new(Slots | (1u << (role & 31)));
    public UnitRoleSet Without(int role) => new(Slots & ~(1u << (role & 31)));
    public bool Intersects(UnitRoleSet other) => (Slots & other.Slots) != 0;
}
