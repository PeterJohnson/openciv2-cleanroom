using OpenCiv2.Base;
using OpenCiv2.Core.Ai;
using OpenCiv2.Core.State;
using OpenCiv2.Core.Units;

namespace OpenCiv2.Core;

public enum NegotiatedTreatyKind { Alliance, Peace, Ceasefire }
public sealed record NegotiatedTreatyNotice(NegotiatedTreatyKind Kind, DiplomaticParty Actor, DiplomaticParty Other);

/// <summary>V04 §5 local negotiated treaty wrappers with actual A08 stance evaluation.</summary>
public sealed class NegotiatedTreaties(GameState world, DiplomaticStance stance, Action<NegotiatedTreatyNotice> notice)
{
    private DiplomaticParty Party(int c) => new(c, world.Names[c].Tribe, world.Names[c].Adjective);
    public void FormAlliance(int actor, int other, string call)
    {
        MapKnowledge.RequireLocal(world);
        DiplomaticAttitudes.Adjust(world, other, actor, -25);
        DiplomaticRelations.Set(world, actor, other, new() { Alliance = true });
        stance.Evaluate(actor, other, call + "/stance");
        world.Civilizations[other].DiploRequestCount = 0;
        world.Civilizations[other].Flags.HasAllied = true;
        world.Diplomacy.DeclareWar = false;
        world.Civilizations[actor].LastContactTurn[other] = world.Globals.Turn;
        world.Civilizations[actor].Flags.HasAllied = true;
        notice(new(NegotiatedTreatyKind.Alliance, Party(actor), Party(other)));
    }
    public void SignPeace(int actor, int other, string call)
    {
        MapKnowledge.RequireLocal(world);
        DiplomaticRelations.Set(world, actor, other, new() { Peace = true, ChangedThisTurn = true });
        ClampAttitude(actor, other);
        stance.Evaluate(actor, other, call + "/stance");
        world.Civilizations[other].DiploRequestCount = 0;
        world.Civilizations[actor].LastContactTurn[other] = world.Globals.Turn;
        notice(new(NegotiatedTreatyKind.Peace, Party(actor), Party(other)));
    }
    public void AgreeCeasefire(int actor, int other, string call)
    {
        MapKnowledge.RequireLocal(world);
        DiplomaticRelations.Set(world, actor, other, new() { Ceasefire = true, ChangedThisTurn = true });
        DiplomaticRelations.Clear(world, actor, other, new() { Tribute = true });
        ClampAttitude(actor, other);
        stance.Evaluate(actor, other, call + "/stance");
        world.Civilizations[actor].LastContactTurn[other] = world.Globals.Turn;
        notice(new(NegotiatedTreatyKind.Ceasefire, Party(actor), Party(other)));
        for (int c = 1; c < world.Civilizations.Length; ++c)
        { world.Civilizations[c].Treaties[actor].Aggressor = false; }
    }
    private void ClampAttitude(int actor, int other) => DiplomaticAttitudes.Set(world, other, actor,
        Arithmetic.ClampAB(DiplomaticAttitudes.Get(world, other, actor), 0, 50));
}
