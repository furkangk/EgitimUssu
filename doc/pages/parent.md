---
title: "Veli Ana Sayfası ve Bildirim Özetleri"
summary: "Onaylı çocuk seçicili veli paneli, mesaj/bildirim girişleri ve ödev/ödeme/ders özet rotaları"
tags: [sayfa, veli, parents]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/parent/presentation/pages/parent_home_page.dart
  - mobile/lib/features/parent/presentation/cubit/parent_cubit.dart
  - mobile/lib/core/routing/route_access.dart
  - mobile/lib/core/push/push_route_mapping.dart
updated: 2026-11-15
---

# Veli ana sayfası ve özetleri

`ParentHomePage` mevcut `ParentRepository` ve `ParentCubit` ile veli profilini, çocuk bağlarını ve seçili çocuğun gerçek dashboard'unu yükler. Oturum yoksa giriş gerekliliği; onaylı çocuk yoksa çocuk bağlama girişi gösterilir. Çocuk seçici yalnız onaylı bağları sunar. Yenileme seçili çocuğu korur.

| Rota | Görünüm |
|------|---------|
| `/parent` | Haftalık çalışma, seri, ders/ödev sayıları, ödeme özeti ve gelişim detayına giriş |
| `/parent/assignments` | Seçili çocuğun toplam/tamamlanan/açık ödev sayıları |
| `/parent/payments` | Seçili çocuğun beklenen/tahsil edilen/kalan tutarı ve sunucunun para birimi |
| `/parent/lessons` | Seçili çocuğun tamamlanan/planlanan ders sayıları |

Özet rotaları `ParentHomeSection` ile aynı panelin bölümünü gösterir; tekil ödev/ödeme kaydı listesi veya ödeme beyanı ekranı değildir. Mevcut bildirim hedefleri çocuk kimliği taşımadığından ilk onaylı çocuk seçilir; veli seçiciden diğer çocuğa geçebilir. Yeni liste ve işlem akışları P21 kapsamındadır.

Başlıktaki “Mesajlar” `/messages`, zil `/notifications` rotasına gider. Alt menüdeki `/parent/notifications` eski veli bildirim **tercihleri** ekranıdır. Ortak ayarlar/hesap/üyelik rotaları veliye açıktır; ek profil girişleri P21'de kalır. Özetlerde Ana Sayfa sekmesi seçili değildir; dokunulduğunda `/parent` açılır. Ana ekranın sabit metinleri tr/en ARB'dedir.

Router rol sınırı sağlar; çocuğun sahipliği, onaylı bağ ve gizlilik sunucu tarafından denetlenir. Ders, ödev veya ödeme yazma yetkisi eklenmez. Gerçek FCM cihaz denemesi Q8/P25'e bağlıdır; burada platform bağımsız bildirim dokunma akışı test edilir.

İlgili: [M09](../modules/m09_parents.md), [veli rolü](../roles/veli.md), [rota matrisi](../architecture/mobile_flutter.md#6-yönlendirme--go_router), [bildirim merkezi](notifications.md).

Güncelleme: 2026-11-15 — P20 Task 2.
