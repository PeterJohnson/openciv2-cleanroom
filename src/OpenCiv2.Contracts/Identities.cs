namespace OpenCiv2.Contracts;

public readonly record struct CivilizationSlot(byte Value);
public readonly record struct UnitSlot(ushort Value);
public readonly record struct CitySlot(ushort Value);
public readonly record struct UnitToken(uint Value) { public bool Occupied => Value != 0; }
public readonly record struct CityToken(uint Value) { public bool Occupied => Value != 0; }
public static class EntityLimits
{
    public const int Civilizations = 8;
    public const int Units = 2048;
    public const int Cities = 256;
}

public enum GameMode { SinglePlayer, Hotseat, Email, Tcp, Ipx, Modem, Serial }
public enum MovementMode { View, MoveUnits }
public enum Government { Anarchy, Despotism, Monarchy, Communism, Fundamentalism, Republic, Democracy }
public enum Difficulty { Chieftain, Warlord, Prince, King, Emperor, Deity }
public enum Gender { Male, Female }
public enum WorldSize { Small, Normal, Large }
public enum RuleSelection { Standard, Advanced }
public enum NetworkProtocol { Tcp, Ipx, Modem, Serial }
public enum WorldParameter { LandMass, LandForm, Temperature, Climate, Age }

public readonly record struct UnitType(byte Value);
public enum UnitOrder : sbyte
{
    None = -1, Fortifying = 1, Fortified = 2, Sleeping = 3, Fortress = 4, Road = 5,
    Irrigate = 6, Mine = 7, Transform = 8, Clean = 9, Airbase = 10, GoTo = 11,
    AiIdle = 16, AiGoTo = 27
}
