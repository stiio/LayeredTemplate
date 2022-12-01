using Xunit;

namespace LayeredTemplate.Web.IntegrationTests;

[CollectionDefinition(nameof(WebApp))]
public class WebAppCollection : ICollectionFixture<WebApp>
{
}