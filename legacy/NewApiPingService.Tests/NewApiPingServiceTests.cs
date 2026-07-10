using System;
using FluentAssertions;
using Legacy.NewApiPingService;
using Xunit;

namespace Legacy.NewApiPingService.Tests
{
    public class NewApiPingServiceTests
    {
        private const string BaseUrl = "https://excel-etl.internal/";
        private const string ApiKey = "legacy-app-api-key";

        [Fact]
        public void Constructor_ConfiguresBaseAddress()
        {
            using (var service = new NewApiPingService(BaseUrl, ApiKey))
            {
                service.Client.BaseAddress.Should().Be(new Uri(BaseUrl));
            }
        }

        [Fact]
        public void Constructor_ConfiguresTwoMinuteTimeout()
        {
            using (var service = new NewApiPingService(BaseUrl, ApiKey))
            {
                service.Client.Timeout.Should().Be(TimeSpan.FromMinutes(2));
            }
        }

        [Fact]
        public void Constructor_SetsApiKeyHeader()
        {
            using (var service = new NewApiPingService(BaseUrl, ApiKey))
            {
                service.Client.DefaultRequestHeaders.GetValues("X-Api-Key").Should().ContainSingle(ApiKey);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WithInvalidBaseUrl_ThrowsArgumentException(string invalidBaseUrl)
        {
            Action act = () => new NewApiPingService(invalidBaseUrl, ApiKey);

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("baseUrl");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WithInvalidApiKey_ThrowsArgumentException(string invalidApiKey)
        {
            Action act = () => new NewApiPingService(BaseUrl, invalidApiKey);

            act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("apiKey");
        }
    }
}
