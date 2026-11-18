using OpenCiv2.AutomationCases;
using Xunit;

namespace OpenCiv2.Automation.Tests;

public sealed class AutomationTests
{
    public static TheoryData<string> Cases => new(AutomationSuite.Names);
    [Theory]
    [MemberData(nameof(Cases))]
    public void Production_script_contract(string name) => AutomationSuite.Run(name);
}
