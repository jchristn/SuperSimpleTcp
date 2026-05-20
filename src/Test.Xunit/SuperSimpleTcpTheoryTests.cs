namespace Test.Xunit;

using Test.Shared;
using Touchstone.Core;
using Touchstone.XunitAdapter;
using global::Xunit;

public sealed class SuperSimpleTcpTheoryTests : TouchstoneFactBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return SuperSimpleTcpTestSuites.All; }
    }

    [Fact]
    public Task RunTest()
    {
        return RunAllAsync(CancellationToken.None);
    }
}
