namespace EgitimUssu.Shared.Infrastructure.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string Configuration { get; set; } = "localhost:6379";
}
