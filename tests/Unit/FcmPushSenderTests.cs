using System.Net;
using System.Text;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P03/K-02: FCM HTTP v1 gönderici. Üç sonuç ayrımı kritik: <see cref="PushDeliveryStatus.TokenInvalid"/>
/// token'ı kalıcı olarak kapatır, <see cref="PushDeliveryStatus.TransientFailure"/> ise tekrar denenir.
/// </summary>
public sealed class FcmPushSenderTests
{
    private static readonly PushMessage Message = new(
        "Ders hatırlatması",
        "Matematik dersin 30 dakika sonra.",
        new Dictionary<string, string> { ["route"] = "/lesson-sessions" });

    [Fact]
    public async Task Successful_Response_Should_Return_Delivered()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"name\":\"projects/egitimussu/messages/1\"}");
        var sender = CreateSender(handler);

        var result = await sender.SendAsync("fcm-token", Message);

        Assert.Equal(PushDeliveryStatus.Delivered, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Unregistered_Error_Should_Return_TokenInvalid()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.NotFound,
            "{\"error\":{\"status\":\"NOT_FOUND\",\"details\":[{\"errorCode\":\"UNREGISTERED\"}]}}");
        var sender = CreateSender(handler);

        var result = await sender.SendAsync("eski-token", Message);

        Assert.Equal(PushDeliveryStatus.TokenInvalid, result.Status);
    }

    [Fact]
    public async Task Invalid_Argument_Should_Return_TokenInvalid()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            "{\"error\":{\"status\":\"INVALID_ARGUMENT\"}}");
        var sender = CreateSender(handler);

        var result = await sender.SendAsync("bozuk-token", Message);

        Assert.Equal(PushDeliveryStatus.TokenInvalid, result.Status);
    }

    [Fact]
    public async Task Server_Error_Should_Return_TransientFailure()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{\"error\":{\"status\":\"UNAVAILABLE\"}}");
        var sender = CreateSender(handler);

        var result = await sender.SendAsync("fcm-token", Message);

        Assert.Equal(PushDeliveryStatus.TransientFailure, result.Status);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Request_Should_Target_Project_Endpoint_With_Bearer_Token()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var sender = CreateSender(handler);

        await sender.SendAsync("fcm-token", Message);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(
            "https://fcm.googleapis.com/v1/projects/egitimussu/messages:send",
            handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sahte-erisim-tokeni", handler.LastRequest.Headers.Authorization.Parameter);
        Assert.Contains("\"token\":\"fcm-token\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"route\":\"/lesson-sessions\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    private static FcmPushSender CreateSender(StubHttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            new StubAccessTokenProvider(),
            Options.Create(new PushOptions
            {
                Provider = PushOptions.FcmProvider,
                ProjectId = "egitimussu",
                ServiceAccountJson = "{\"type\":\"service_account\"}"
            }),
            NullLogger<FcmPushSender>.Instance);

    private sealed class StubAccessTokenProvider : IFcmAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
            => Task.FromResult("sahte-erisim-tokeni");
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
