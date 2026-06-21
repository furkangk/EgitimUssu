namespace EgitimUssu.Shared.Infrastructure.Configuration;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool DispatchEnabled { get; set; }

    public int BatchSize { get; set; } = 20;

    public int PollIntervalSeconds { get; set; } = 15;
}
