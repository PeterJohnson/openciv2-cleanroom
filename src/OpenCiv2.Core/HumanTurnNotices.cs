using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

/// <summary>P09 stock council information. New explanatory prose is permitted; this is not
/// the original opinion-keyed chapter selector. Reading it performs no city or AI work.</summary>
public sealed record CouncilSummary(int Civilization, int Cities, int Population, int Units, int Disorder,
    int Gold, int TaxRate, int ScienceRate, int Research, int Wars)
{
    public static CouncilSummary Prepare(GameState world, int owner)
    {
        if (owner < 0 || owner >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(owner)); }
        int cities = 0, population = 0, units = 0, disorder = 0, wars = 0;
        foreach (ref readonly var city in world.Entities.Cities.Scanned)
        {
            if (!city.Token.Occupied || city.Owner.Value != owner)
            { continue; }
            cities++;
            population = unchecked(population + city.Size);
            if (city.Flags.Disorder)
            { disorder++; }
        }
        foreach (ref readonly var unit in world.Entities.Units.Scanned)
        { if (unit.Token.Occupied && unit.Owner.Value == owner) { units++; } }
        ref readonly var civ = ref world.Civilizations[owner];
        for (int other = 1; other < EntityLimits.Civilizations; other++)
        { if (other != owner && civ.Treaties[other].War) { wars++; } }
        return new(owner, cities, population, units, disorder, civ.Gold, civ.TaxRate, civ.ScienceRate, civ.Researching, wars);
    }
}
public enum HumanTurnHint { FirstMove, FirstHelp, SecondHelp, OneCity, Expansion }
public abstract record HumanTurnNotice
{
    public sealed record CouncilInvitation(int Civilization, CityTarget? City, string CityName, int Year) : HumanTurnNotice;
    public sealed record Council(CouncilSummary Summary) : HumanTurnNotice;
    public sealed record Tutorial(int Civilization, HumanTurnHint Kind, CityTarget? City) : HumanTurnNotice;
}

/// <summary>F04 §8 notices before human movement, including when resumed upkeep was skipped.
/// The owner is the admitted local human. Council invitation draws are F04 gameplay samples;
/// stock council rendering consumes no samples or unrelated gameplay operations.</summary>
public sealed class HumanTurnNotices(GameState world, GameplayRandom random, Action<MapPoint> center)
{
    private GameState World => world;
    public Operation Begin(int owner, string call)
    {
        if (owner < 0 || owner >= EntityLimits.Civilizations)
        { throw new ArgumentOutOfRangeException(nameof(owner)); }
        return new(this, owner, call);
    }
    public sealed class Operation : IDisposable
    {
        private readonly HumanTurnNotices parent;
        private readonly IEnumerator<HumanTurnNotice> steps;
        private readonly List<Exception> failures = [];
        private bool disposed, faulted;
        internal int Reply { get; private set; }
        public bool Completed { get; private set; }
        public HumanTurnNotice? Pending { get; private set; }
        public IReadOnlyList<Exception> PresentationFailures => failures.AsReadOnly();
        internal Operation(HumanTurnNotices parent, int owner, string call)
        {
            ArgumentException.ThrowIfNullOrEmpty(call);
            this.parent = parent;
            steps = parent.Execute(this, owner, call).GetEnumerator();
        }
        internal void Present(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        public HumanTurnNotice? Resume(int? answer = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            { throw new InvalidOperationException("Failed human turn notices cannot resume"); }
            if (Completed)
            { return null; }
            try
            {
                MapKnowledge.RequireLocal(parent.World);
                if (Pending is not null && answer is null)
                { return Pending; }
                Reply = Pending is null ? 1 : answer!.Value;
                Pending = null;
                if (steps.MoveNext())
                { return Pending = steps.Current; }
                steps.Dispose();
                Completed = true;
                return null;
            }
            catch { faulted = true; steps.Dispose(); throw; }
        }
        public void Dispose() { disposed = true; steps.Dispose(); }
    }
    private IEnumerable<HumanTurnNotice> Execute(Operation op, int owner, string call)
    {
        if (world.Globals.Turn > 1 && (world.Globals.Turn - 1) % 50 == 0 && world.Globals.GameOptions.Council)
        {
            CityTarget? selected = null;
            int best = -1;
            string name = "No city";
            MapPoint position = default;
            for (int i = 0; i < world.Entities.Cities.End; i++)
            {
                var slot = new CitySlot((ushort)i);
                if (!world.Entities.Cities.Live(slot) || world.Entities.Cities.At(slot).Owner.Value != owner)
                { continue; }
                int size = world.Entities.Cities.At(slot).Size;
                if (BuildingQueries.HasBuilding(world, slot, BuildingIds.Palace))
                { size = unchecked(size * 2); }
                int score = size <= 1 ? 0 : random.PrimaryModulo(new ChoiceKey("F04.council-city", call, i), size);
                if (score <= best)
                { continue; }
                best = score;
                selected = new(slot, world.Entities.Cities.At(slot).Token);
                name = world.Entities.Cities.At(slot).Name;
                position = world.Entities.Cities.At(slot).Position;
            }
            if (selected is not null)
            { op.Present(() => center(position)); }
            yield return new HumanTurnNotice.CouncilInvitation(owner, selected, name, world.Globals.Year);
            if (op.Reply == 0)
            { yield return new HumanTurnNotice.Council(CouncilSummary.Prepare(world, owner)); }
        }
        if (!world.Globals.GameOptions.Tutorial)
        { yield break; }
        if (world.Globals.Turn == 1)
        { yield return new HumanTurnNotice.Tutorial(owner, HumanTurnHint.FirstMove, null); }
        if (world.Globals.Turn is 20 or 60)
        { yield return new HumanTurnNotice.Tutorial(owner, HumanTurnHint.FirstHelp, null); }
        if (world.Globals.Turn is 40 or 80)
        { yield return new HumanTurnNotice.Tutorial(owner, HumanTurnHint.SecondHelp, null); }
        if (world.Globals.Turn == 35 && world.Civilizations[owner].CityCount == 1 && world.Civilizations[owner].TotalCitySize > 2
            && world.Civilizations[owner].UnitsActive[UnitTypeIds.Settlers] == 0
            && world.Civilizations[owner].UnitsInProduction[UnitTypeIds.Settlers] == 0)
        {
            var city = CityQueries.Nearest(world, new(world.Map.Geometry.Width / 2, world.Map.Geometry.Height / 2), owner).Slot;
            if (city is { } slot)
            {
                yield return new HumanTurnNotice.Tutorial(owner, HumanTurnHint.OneCity, new(slot, world.Entities.Cities.At(slot).Token));
                world.Civilizations[owner].LastCityTurn = unchecked((short)(world.Globals.Turn - 25));
            }
        }
        else if (world.Globals.Turn - world.Civilizations[owner].LastCityTurn > 24 && world.Civilizations[owner].CityCount < 6
            && world.Civilizations[owner].UnitsActive[UnitTypeIds.Settlers] == 0)
        {
            yield return new HumanTurnNotice.Tutorial(owner, HumanTurnHint.Expansion, null);
            world.Civilizations[owner].LastCityTurn = world.Globals.Turn;
        }
    }
}
