namespace OpenCiv2.Core.Rules;

/// <summary>Fixed F03 advance identities used by identity-specific mechanics. Names and prerequisites remain data.</summary>
public static class AdvanceIds
{
    public const int AdvancedFlight = 0, Explosives = 28, Laser = 41, Robotics = 72;
    public const int Alphabet = 1, Automobile = 5, Banking = 6, BronzeWorking = 8, CeremonialBurial = 9, Chemistry = 10, CodeOfLaws = 12;
    public const int Combustion = 14, Economics = 22, Electricity = 23, MassProduction = 48, Pottery = 65, Refrigeration = 70;
    public const int Communism = 15, Computers = 16, Conscription = 17, Democracy = 21, FusionPower = 32;
    public const int GuerrillaWarfare = 34, Gunpowder = 35, HorsebackRiding = 36, Industrialization = 37;
    public const int Invention = 38, IronWorking = 39, Leadership = 42, MapMaking = 46, Masonry = 47;
    public const int Miniaturization = 52, Monotheism = 55, Mysticism = 56, Navigation = 57, NuclearFission = 58, NuclearPower = 59;
    public const int Fundamentalism = 31, Philosophy = 60, Republic = 71;
    public const int Polytheism = 64, Rocketry = 73, SpaceFlight = 76, Steel = 79, Tactics = 81, Theology = 82;
    public const int University = 85, WarriorCode = 86;
    public const int BridgeBuilding = 7, Construction = 18, Currency = 20, Monarchy = 54, TheWheel = 87;
    public const int Radio = 66, Railroad = 67;
    public const int MobileWarfare = 53;
    public const int Seafaring = 75, Trade = 84, Writing = 88;
    public const int Electronics = 24, Environmentalism = 26, Plastics = 62, Sanitation = 74;
}

/// <summary>C08 ordinary facility/production identities in the declared catalog order.</summary>
public static class BuildingIds
{
    public const int First = 1, Last = 34;
    public const int Palace = 1, Barracks = 2, Temple = 4, Marketplace = 5, Library = 6, Aqueduct = 9, Bank = 10, Cathedral = 11;
    public const int MassTransit = 13, Factory = 15, SdiDefense = 17, RecyclingCenter = 18, PowerPlant = 19, HydroPlant = 20;
    public const int NuclearPlant = 21, SewerSystem = 23, Superhighways = 25, SolarPlant = 29, PortFacility = 34;
    public const int Structural = 35, Component = 36, Module = 37;
    public const int Granary = 3, Colosseum = 14;
    public const int Supermarket = 24, Harbor = 30, OffshorePlatform = 31, Airport = 32;
    public const int CoastalFortress = 28;
    public const int Courthouse = 7, University = 12, ManufacturingPlant = 16, StockExchange = 22, ResearchLab = 26, PoliceStation = 33, Capitalization = 38;
}

/// <summary>C08 fixed wonder identities; recorded locations and active effects are distinct.</summary>
public static class WonderIds
{
    public const int Lighthouse = 3, Michelangelo = 10, Magellan = 12, EiffelTower = 20, Manhattan = 23, Apollo = 25;
    public const int Pyramids = 0, Colossus = 2, KingRichard = 8;
    public const int HangingGardens = 1, Oracle = 5, Copernicus = 11, Shakespeare = 13, Bach = 15, Newton = 16;
    public const int WomensSuffrage = 21, HooverDam = 22, Seti = 26, CureForCancer = 27;
    public const int MarcoPolo = 9, StatueOfLiberty = 19, UnitedNations = 24;
    public const int SunTzu = 7;
    public const int Leonardo = 14, AdamSmith = 17;
    public const int GreatLibrary = 4, GreatWall = 6, Darwin = 18;
}

/// <summary>C07 commodity identities. Display names and rule labels do not redirect their formulas.</summary>
public enum Commodity
{
    Hides, Wool, Beads, Cloth, Salt, Coal, Copper, Dye, Wine, Silk, Silver, Spice, Gems, Gold, Oil, Uranium
}

/// <summary>A08 fixed unit identities used to read customizable rule prerequisites.</summary>
public static class UnitTypeIds
{
    public const int AlpineTroops = 10, Marines = 12, Crusaders = 18, Dragoons = 20, Armor = 22;
    public const int Fighter = 27, Helicopter = 29, StealthFighter = 30, StealthBomber = 31;
    public const int Ironclad = 36, Destroyer = 37, Cruiser = 38, AegisCruiser = 39, Battleship = 40, Submarine = 41, Carrier = 42, CruiseMissile = 44;
    public const int Settlers = 0, Warriors = 2, Phalanx = 3, Archers = 4, Legion = 5, Pikemen = 6, Musketeers = 7, Fanatics = 8, Partisans = 9;
    public const int Horsemen = 15, Chariot = 16, Elephant = 17, Knights = 19;
    public const int Bomber = 28, Trireme = 32, Caravel = 33, Frigate = 35, Transport = 43;
    public const int NuclearMissile = 45, Diplomat = 46, Spy = 47;
    public const int Riflemen = 11, Engineers = 1, Paratroopers = 13;
}
