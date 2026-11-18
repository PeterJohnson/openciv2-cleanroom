using OpenCiv2.FoundationCases;
using Xunit;

namespace OpenCiv2.Base.Tests;

public sealed class FoundationTests
{
    public static TheoryData<string> Cases => new(FoundationSuite.Names);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Specified_contract(string name) => FoundationSuite.Run(name);
}
