using Xunit;

namespace LayeredTemplate.Tests.App.Integration;

[CollectionDefinition(nameof(WebApp))]
public class WebAppCollection : ICollectionFixture<WebApp>
{
}