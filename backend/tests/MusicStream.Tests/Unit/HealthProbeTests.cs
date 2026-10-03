using System.Net;
using MusicStream.Api.Infrastructure;

namespace MusicStream.Tests.Unit;

public class HealthProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;

        public StubHandler(Func<HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond());
    }

    [Fact]
    public async Task Healthy_ReturnsZero()
        => Assert.Equal(0, await HealthProbe.RunAsync(new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK))));

    [Fact]
    public async Task Unhealthy_ReturnsOne()
        => Assert.Equal(1, await HealthProbe.RunAsync(new StubHandler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

    [Fact]
    public async Task Unreachable_ReturnsOne()
        => Assert.Equal(1, await HealthProbe.RunAsync(new StubHandler(() => throw new HttpRequestException("down"))));
}
