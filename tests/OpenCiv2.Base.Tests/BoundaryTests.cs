using System.Reflection;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using Xunit;

namespace OpenCiv2.Base.Tests;

public sealed class BoundaryTests
{
    [Fact]
    public void Public_contract_values_only_expose_owned_or_immutable_types()
    {
        Assembly contracts = typeof(CommandRequest).Assembly;
        foreach (Type type in contracts.GetExportedTypes())
        {
            // Provider methods have explicit lifetimes; inspect observation storage here.
            if (type.IsInterface || type.IsEnum || typeof(Exception).IsAssignableFrom(type)) continue;
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                Assert.True(Immutable(property.PropertyType), $"Mutable API property: {type.Name}.{property.Name}");
            Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }
        foreach (AssemblyName reference in contracts.GetReferencedAssemblies())
            Assert.True(reference.Name == "OpenCiv2.Base" || reference.Name!.StartsWith("System.", StringComparison.Ordinal),
                $"Unexpected Contracts dependency: {reference.Name}");
    }

    private static bool Immutable(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return true;
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            return (definition == typeof(OwnedList<>) || definition == typeof(LiveRecords<>) || definition == typeof(Nullable<>)) &&
                type.GetGenericArguments().All(Immutable);
        }
        return type.Assembly == typeof(CommandRequest).Assembly ||
            (type.IsValueType && type.Assembly == typeof(RuntimeId).Assembly);
    }

    [Fact]
    public void Player_port_exposes_only_owned_contract_values()
    {
        foreach (var method in typeof(IPlayerClient).GetMethods())
        {
            Assert.True(Immutable(method.ReturnType), $"Mutable player return: {method.Name}");
            foreach (var parameter in method.GetParameters())
                Assert.True(Immutable(parameter.ParameterType), $"Mutable player argument: {method.Name}.{parameter.Name}");
        }
    }
}
