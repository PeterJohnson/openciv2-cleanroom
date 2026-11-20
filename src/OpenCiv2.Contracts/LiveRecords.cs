using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

public enum LiveProvenance { OwnedCurrent }
public sealed record LiveObservation(RuntimeId Runtime, SessionId Session, ulong Revision,
    CivilizationSlot Observer, LiveProvenance Provenance = LiveProvenance.OwnedCurrent);
public sealed record OwnUnit(UnitSlot Slot, UnitToken Token, MapPoint Position, UnitType Type,
    byte MovesUsed, byte Damage, UnitOrder Orders, CitySlot? Home);
public sealed record OwnCity(CitySlot Slot, CityToken Token, MapPoint Position, string Name,
    sbyte Size, sbyte Production, short FoodBox, short ShieldBox);
public sealed record LiveRecords<T>(LiveObservation Observation, OwnedList<T> Records);
