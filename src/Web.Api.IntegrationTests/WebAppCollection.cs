using Xunit;

namespace LayeredTemplate.Web.Api.IntegrationTests;

[CollectionDefinition(nameof(WebApp))]
public class WebAppCollection : ICollectionFixture<WebApp>
{
}