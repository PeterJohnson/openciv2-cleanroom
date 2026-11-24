using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Core.Units;

public interface INegotiatedMapEffects
{
    void Accepted(int requester, DiplomaticParty grantor);
    void Publish();
    void RedrawMaps(int localHuman);
}

/// <summary>W03 §7 accepted local AI negotiation. This is directional discovery and
/// live-feature intersection, not a symmetric union of remembered maps.</summary>
public sealed class NegotiatedMaps(GameState world, INegotiatedMapEffects effects)
{
    public Exception? Exchange(int requester, int grantor)
    {
        MapKnowledge.RequireLocal(world);
        effects.Accepted(requester, new(grantor, world.Names[grantor].Tribe, world.Names[grantor].Adjective));
        world.Civilizations[grantor].DiploRequestCount = unchecked((sbyte)(world.Civilizations[grantor].DiploRequestCount + 2));
        for (int i = 0; i < world.Map.Geometry.Area; ++i)
        {
            ref var tile = ref world.Map.Tiles[i];
            if (!tile.RevealedTo.Contains(grantor))
            {
                if (tile.RevealedTo.Contains(requester))
                {
                    tile.RevealedTo = tile.RevealedTo.With(grantor);
                    if (grantor != 0 && requester != 0)
                    { world.Map.RememberedLayer(grantor)[i] |= tile.Features & world.Map.RememberedLayer(requester)[i]; }
                }
            }
            else
            {
                tile.RevealedTo = tile.RevealedTo.With(requester);
                if (grantor != 0 && requester != 0)
                { world.Map.RememberedLayer(requester)[i] |= tile.Features & world.Map.RememberedLayer(grantor)[i]; }
            }
        }
        for (int i = 0; i < world.Entities.Units.End; ++i)
        {
            ref var unit = ref world.Entities.Units.Storage[i];
            if (!unit.Token.Occupied)
            { continue; }
            if (!unit.Visibility.Contains(grantor) && unit.Owner.Value != grantor)
            {
                if (unit.Visibility.Contains(requester) || unit.Owner.Value == requester)
                { unit.Visibility = unit.Visibility.With(grantor); }
            }
            else
            { unit.Visibility = unit.Visibility.With(requester); }
        }
        for (int i = 0; i < world.Entities.Cities.End; ++i)
        {
            ref var city = ref world.Entities.Cities.Storage[i];
            if (!city.Token.Occupied)
            { continue; }
            if (!city.KnownTo.Contains(grantor) && city.Owner.Value != grantor)
            {
                if (city.KnownTo.Contains(requester) || city.Owner.Value == requester)
                { city.KnownTo = city.KnownTo.With(grantor); }
            }
            else
            { MapKnowledge.RevealCity(world, requester, new((ushort)i)); }
        }
        effects.Publish();
        try
        { effects.RedrawMaps(world.Multiplayer.LocalHuman); return null; }
        catch (Exception error) { return error; }
    }
}
