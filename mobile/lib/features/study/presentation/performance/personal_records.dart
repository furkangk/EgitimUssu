/// Performans sayfası saf istatistik yardımcıları.
double bestNet(List<double> nets) =>
    nets.isEmpty ? 0 : nets.reduce((a, b) => a > b ? a : b);

double averageNet(List<double> nets) =>
    nets.isEmpty ? 0 : nets.reduce((a, b) => a + b) / nets.length;

/// Eşiğin (varsayılan 60) altındaki konu adları, en zayıftan güçlüye sıralı.
List<String> weakTopics(
  Map<String, double> topicScores, {
  double threshold = 60,
}) {
  final entries = topicScores.entries.where((e) => e.value < threshold).toList()
    ..sort((a, b) => a.value.compareTo(b.value));
  return entries.map((e) => e.key).toList();
}
