/// M01-4 / D-03: Hesabın açık olduğu bir cihaz oturumu.
class DeviceSession {
  const DeviceSession({
    required this.sessionId,
    required this.createdOnUtc,
    required this.lastUsedOnUtc,
    required this.isCurrent,
    this.deviceName,
  });

  final String sessionId;

  /// İstemcinin bildirdiği cihaz adı; yoksa `null`.
  final String? deviceName;

  /// Cihazın ilk bağlandığı an — jeton yenilense de değişmez.
  final DateTime createdOnUtc;

  /// Jetonun en son yenilendiği an.
  final DateTime lastUsedOnUtc;

  /// Bu oturum, uygulamanın şu an kullandığı oturum mu.
  final bool isCurrent;
}

abstract interface class DeviceSessionRepository {
  /// Açık oturumları listeler (kapatılmış ve süresi geçmişler gelmez).
  Future<List<DeviceSession>> list();

  /// Tek bir oturumu kapatır.
  Future<void> revoke(String sessionId);

  /// Mevcut oturum hariç hepsini kapatır; kapatılan oturum sayısını döndürür.
  Future<int> revokeAll();
}
