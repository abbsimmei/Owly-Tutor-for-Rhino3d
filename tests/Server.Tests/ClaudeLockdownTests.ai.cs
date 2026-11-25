using System.Diagnostics;

using NUnit.Framework;

namespace Rhino.AI.Server.Tests;

// Plan 027: the lockdown flags are shared by the chat process and the notes request. ConfigureArguments reads
// AISettings (Rhino), so what is pinned here is the shared helper on its own.
[TestFixture]
public class ClaudeLockdownTests
{
    [Test]
    public void The_lockdown_is_exactly_these_four_flags_in_this_order()
    {
        ProcessStartInfo psi = new();

        ClaudeStreamJsonParser.AddLockdown(psi);

        Assert.That(psi.ArgumentList, Is.EqualTo(new[]
        {
            "--strict-mcp-config",
            "--setting-sources", "",
            "--tools", "",
            "--disable-slash-commands",
        }));
    }
}
