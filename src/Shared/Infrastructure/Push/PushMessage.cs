namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>
/// Tek bir push bildirimi. <see cref="Data"/> alanı mobilde derin bağlantı için kullanılır
/// (ör. <c>{"route":"/lesson-sessions"}</c>) — bildirime dokunma ilgili ekranı açar.
/// </summary>
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);
