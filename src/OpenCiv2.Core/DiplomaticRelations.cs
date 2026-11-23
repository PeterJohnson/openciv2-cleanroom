using OpenCiv2.Core.State;

namespace OpenCiv2.Core;

/// <summary>V04 §3 bilateral flag primitives. Cascades operate on the requested flags in order;
/// they do not declare a full war, establish contact, change attitude or send messages.</summary>
public static class DiplomaticRelations
{
    /// <summary>V04 caller-supplied protected relation set, without file-bit representation.</summary>
    public static bool Intersects(in Relationship relation, in Relationship flags) =>
        relation.Contact && flags.Contact || relation.Ceasefire && flags.Ceasefire || relation.Peace && flags.Peace
        || relation.Alliance && flags.Alliance || relation.Vendetta && flags.Vendetta || relation.HostileIntent && flags.HostileIntent
        || relation.BrokenTreaty && flags.BrokenTreaty || relation.Embassy && flags.Embassy || relation.NuclearThreat && flags.NuclearThreat
        || relation.Fighting && flags.Fighting || relation.RecentContact && flags.RecentContact || relation.Aggressor && flags.Aggressor
        || relation.SneakAttack && flags.SneakAttack || relation.War && flags.War || relation.ChangedThisTurn && flags.ChangedThisTurn
        || relation.Incident && flags.Incident || relation.Nuked && flags.Nuked || relation.Tribute && flags.Tribute
        || relation.ExtendedAggressor && flags.ExtendedAggressor || relation.Mercenary && flags.Mercenary
        || relation.WarThisTurn && flags.WarThisTurn || relation.CityCaptured && flags.CityCaptured
        || relation.MetThisTurn && flags.MetThisTurn || relation.SpyChat && flags.SpyChat;

    public static void Clear(GameState world, int from, int to, Relationship flags)
    {
        if (flags.Peace)
        {
            Clear(world, from, to, new() { Alliance = true });
        }

        if (flags.War)
        {
            Clear(world, from, to, new() { Aggressor = true, SneakAttack = true });
        }

        if (flags.Contact)
        {
            Clear(world, from, to, new() { War = true });
        }

        Apply(ref world.Civilizations[from].Treaties[to], flags, false);
        Apply(ref world.Civilizations[to].Treaties[from], flags, false);
    }

    public static void Set(GameState world, int from, int to, Relationship flags)
    {
        if (flags.Alliance)
        {
            Set(world, from, to, new() { Peace = true });
        }

        if (flags.Ceasefire || flags.Peace || flags.Alliance)
        {
            Clear(world, from, to, new() { War = true, Aggressor = true, Fighting = true, BrokenTreaty = true, HostileIntent = true });
        }

        if (flags.War)
        {
            Clear(world, from, to, new() { Ceasefire = true, Peace = true, Alliance = true });
            flags.WarThisTurn = true;
        }
        Apply(ref world.Civilizations[from].Treaties[to], flags, true);
        Apply(ref world.Civilizations[to].Treaties[from], flags, true);
    }

    private static void Apply(ref Relationship relation, in Relationship flags, bool value)
    {
        if (flags.Contact)
        {
            relation.Contact = value;
        }

        if (flags.Ceasefire)
        {
            relation.Ceasefire = value;
        }

        if (flags.Peace)
        {
            relation.Peace = value;
        }

        if (flags.Alliance)
        {
            relation.Alliance = value;
        }

        if (flags.Vendetta)
        {
            relation.Vendetta = value;
        }

        if (flags.HostileIntent)
        {
            relation.HostileIntent = value;
        }

        if (flags.BrokenTreaty)
        {
            relation.BrokenTreaty = value;
        }

        if (flags.Embassy)
        {
            relation.Embassy = value;
        }

        if (flags.NuclearThreat)
        {
            relation.NuclearThreat = value;
        }

        if (flags.Fighting)
        {
            relation.Fighting = value;
        }

        if (flags.RecentContact)
        {
            relation.RecentContact = value;
        }

        if (flags.Aggressor)
        {
            relation.Aggressor = value;
        }

        if (flags.SneakAttack)
        {
            relation.SneakAttack = value;
        }

        if (flags.War)
        {
            relation.War = value;
        }

        if (flags.ChangedThisTurn)
        {
            relation.ChangedThisTurn = value;
        }

        if (flags.Incident)
        {
            relation.Incident = value;
        }

        if (flags.Nuked)
        {
            relation.Nuked = value;
        }

        if (flags.Tribute)
        {
            relation.Tribute = value;
        }

        if (flags.ExtendedAggressor)
        {
            relation.ExtendedAggressor = value;
        }

        if (flags.Mercenary)
        {
            relation.Mercenary = value;
        }

        if (flags.WarThisTurn)
        {
            relation.WarThisTurn = value;
        }

        if (flags.CityCaptured)
        {
            relation.CityCaptured = value;
        }

        if (flags.MetThisTurn)
        {
            relation.MetThisTurn = value;
        }

        if (flags.SpyChat)
        {
            relation.SpyChat = value;
        }
    }
}
