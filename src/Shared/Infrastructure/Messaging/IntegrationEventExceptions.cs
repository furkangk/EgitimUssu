namespace EgitimUssu.Shared.Infrastructure.Messaging;

/// <summary>Yeniden teslimle düzelemeyen geçersiz olay yükü.</summary>
public sealed class PoisonMessageException(string message) : Exception(message);

/// <summary>Başarısız tüketiciyi, olay yükünü hata metnine taşımadan tanımlar.</summary>
public sealed class IntegrationEventHandlerException(string handlerName, Exception innerException)
    : Exception($"{handlerName}: {innerException.GetType().Name}", innerException)
{
    public string HandlerName { get; } = handlerName;
}
