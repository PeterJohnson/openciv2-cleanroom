using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Core.State;

namespace OpenCiv2.Session;

public sealed record WorldParameterServices(
    Func<WorldParameter, short, WorldParameterChoice> Choose,
    IPreferenceWriter Preferences)
{
    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Choose);
        ArgumentNullException.ThrowIfNull(Preferences);
    }
}

/// <summary>F05 §§2–3. Ordered setup effects, explicit gameplay choices and preference persistence.
/// These helpers do not allocate a map or start a client session; the outer wizard owns scheduling.</summary>
public static class WorldSetupOperations
{
    private static void Publish(SessionRuntime runtime)
    {
        if (runtime.Active)
        {
            runtime.Publish();
        }
    }

    private static T Complete<T>(SessionRuntime runtime, Func<T> operation)
    {
        try
        {
            T result = operation();
            Publish(runtime);
            return result;
        }
        catch
        {
            try
            {
                Publish(runtime);
            }
            catch { }

            throw;
        }
    }

    // Decimal prefix conversion into the signed cyclic 16-bit domain, before admission limits.
    private static short Dimension(string text)
    {
        int i = 0;
        while (i < text.Length && (text[i] == ' ' || text[i] is >= '\t' and <= '\r'))
        {
            ++i;
        }

        bool negative = i < text.Length && text[i] == '-';
        if (i < text.Length && text[i] is '+' or '-')
        {
            ++i;
        }

        ushort value = 0;
        for (; i < text.Length && text[i] is >= '0' and <= '9'; ++i)
        {
            value = unchecked((ushort)(value * 10 + text[i] - '0'));
        }

        return unchecked((short)(negative ? -value : value));
    }

    public static bool SelectSize(SessionRuntime runtime, IWorldSizeInteraction services)
    {
        ArgumentNullException.ThrowIfNull(services);
        using var pin = runtime.PinWorld();
        return Complete(runtime, () =>
        {
            ref var setup = ref runtime.World.WorldSetup;
            ref var flags = ref runtime.World.Globals.GameFlags;
            ref var remembered = ref runtime.Process.OrdinaryPreferences.MapSize;
            flags = 0;
            bool success = false, done = false;
            while (!done)
            {
                Publish(runtime);
                var choice = services.ChoosePreset(remembered);
                if (choice is WorldSizeChoice.Preset preset)
                {
                    if (preset.Row >= 0)
                    {
                        remembered = (WorldSize)unchecked((short)preset.Row);
                        setup.Width = (short)(preset.Row == 0 ? 40 : preset.Row == 2 ? 75 : 50);
                        setup.Height = (short)(preset.Row == 0 ? 50 : preset.Row == 2 ? 120 : 80);
                        success = true;
                    }
                    done = true;
                    continue;
                }
                if (choice is not WorldSizeChoice.Custom)
                {
                    throw new ArgumentException("Missing world-size choice");
                }

                for (; ; )
                {
                    Publish(runtime);
                    var custom = services.ChooseCustom("50", "80");
                    if (custom.Status == CustomSizeStatus.Unavailable)
                    {
                        done = true;
                        break;
                    }
                    if (custom.Status == CustomSizeStatus.Cancelled)
                    {
                        break;
                    }

                    if (custom.Status != CustomSizeStatus.Accepted)
                    {
                        throw new ArgumentException("Unknown custom-size status");
                    }

                    setup.Width = Dimension(custom.Width);
                    setup.Height = Dimension(custom.Height);
                    int area = setup.Width * setup.Height;
                    if (setup.Width is >= 20 and <= 250 && setup.Height is >= 20 and <= 250
                        && area is >= 1000 and <= 10000)
                    {
                        success = done = true;
                        break;
                    }
                }
            }
            int finalArea = setup.Width * setup.Height;
            if (finalArea > 5999)
            {
                flags |= 4;
            }

            if (finalArea < 3000)
            {
                flags |= 8;
            }

            setup.Width = unchecked((short)(2 * setup.Width));
            return success;
        });
    }

    private static void Sample(ref WorldSetup setup, WorldParameter parameter, GameplayRandom random, string call)
    {
        string site = parameter switch
        {
            WorldParameter.LandMass => "F05.world.land-mass",
            WorldParameter.LandForm => "F05.world.land-form",
            WorldParameter.Temperature => "F05.world.temperature",
            WorldParameter.Climate => "F05.world.climate",
            WorldParameter.Age => "F05.world.age",
            _ => throw new ArgumentOutOfRangeException(nameof(parameter))
        };
        int index = (int)parameter;
        if (parameter == WorldParameter.LandForm)
        {
            setup.Parameters[index] = 0;
            if (random.PrimaryModulo(new ChoiceKey(site, call), 3) == 0)
            {
                setup.Parameters[index] = setup.Parameters[0];
            }
        }
        else
        {
            setup.Parameters[index] = random.PrimaryModulo(new ChoiceKey(site, call), 3) - 1;
        }
    }

    public static void RandomizeParameters(SessionRuntime runtime, GameplayRandom random, string call)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        using var pin = runtime.PinWorld();
        Complete(runtime, () =>
        {
            foreach (var parameter in new[] {
                WorldParameter.LandMass,
                WorldParameter.LandForm,
                WorldParameter.Temperature,
                WorldParameter.Climate,
                WorldParameter.Age
                })
            {
                Sample(ref runtime.World.WorldSetup, parameter, random, call);
            }

            return true;
        });
    }

    public static bool CustomizeParameters(
        SessionRuntime runtime,
        GameplayRandom random,
        string call,
        WorldParameterServices services)
    {
        ArgumentException.ThrowIfNullOrEmpty(call);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        using var pin = runtime.PinWorld();
        return Complete(runtime, () =>
        {
            ref var setup = ref runtime.World.WorldSetup;
            ref var remembered = ref runtime.Process.OrdinaryPreferences.WorldParameters;
            foreach (var parameter in new[] {
                WorldParameter.LandMass,
                WorldParameter.LandForm,
                WorldParameter.Climate,
                WorldParameter.Temperature,
                WorldParameter.Age
                })
            {
                int index = (int)parameter;
                Publish(runtime);
                var choice = services.Choose(parameter, remembered[index]);
                if (choice is WorldParameterChoice.Selected selected)
                {
                    if (selected.Row > 2)
                    {
                        throw new ArgumentException("World parameter row is outside the specified choices");
                    }

                    setup.Parameters[index] = unchecked(selected.Row - 1);
                    if (selected.Row < 0)
                    {
                        return false;
                    }

                    remembered[index] = (short)setup.Parameters[index];
                }
                else if (choice is WorldParameterChoice.Random)
                {
                    Sample(ref setup, parameter, random, call);
                }
                else
                {
                    throw new ArgumentException("Missing world-parameter choice");
                }
            }
            _ = services.Preferences.Write(
                new(runtime.Process.OrdinaryPreferences, runtime.Process.MultiplayerPreferences),
                () => Publish(runtime));
            return true;
        });
    }

    public static bool SelectParameters(
        SessionRuntime runtime,
        GameplayRandom random,
        string call,
        bool customize,
        bool worldSelected,
        WorldParameterServices? services = null)
    {
        if (!worldSelected)
        {
            return false;
        }

        if (customize)
        {
            ArgumentNullException.ThrowIfNull(services);
            return CustomizeParameters(runtime, random, call, services);
        }
        RandomizeParameters(runtime, random, call);
        return true;
    }
}
