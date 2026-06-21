namespace EgitimUssu.Shared.Infrastructure.Persistence;

public sealed record ModuleDbContextDescriptor(string ModuleName, string Schema, Type DbContextType);
