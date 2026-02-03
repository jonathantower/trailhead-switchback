using Switchback.Core;

namespace Switchback.Core.Tests;

public class PlaceholderTests
{
    [Test]
    public async Task Core_placeholder_exists()
    {
        await Assert.That(Placeholder.Exists).IsTrue();
    }
}
