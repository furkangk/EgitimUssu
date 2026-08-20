---
title: "Proje Vizyonu (promp)"
summary: "promp.txt vizyonunun damıtımı: özel ders sürecini dijitalleştirme fikri, PRD'nin kaynağı"
tags: [kaynak, decision, vizyon]
authority: reference
subtype: decision
source: promp.txt
updated: 2026-08-20
---

`doc/promp.txt`, kullanıcının projeyi ilk tanımladığı özgün, ham vizyon metnidir. Türkiye'de özel ders veren
öğretmen ve özel ders alan öğrencinin bugün Armut/Sahibinden benzeri ilan siteleri veya tanıdık üzerinden
buluşup iletişimi WhatsApp/telefondan, ödemeyi elden, takibi ise sözel/Excel'den yürüttüğü tespitinden yola
çıkar. Metindeki kritik tasarım kararı, "soğuk başlangıç" (cold start) problemidir: sistem ilk günden yalnız
eşleştirme uygulaması olarak kurulursa ilk kaydolan taraf karşı tarafı bulamaz. Bu yüzden öğretmen ve öğrenci
rolleri önce **bireysel olarak** (öğretmen kendi öğrenci takibini, öğrenci kendi ders programını yönetsin) değer
üretecek şekilde tasarlanmalı; eşleştirme/keşif (ilan verme, arama) kitle oluştuktan **sonra** eklenecek ikinci
aşama olarak planlanmalıdır. Bu makale, promp.txt'nin damıtılmış özetidir; PRD'nin türediği kaynağı **cite eder**.

## Kilit Noktalar

- **Cold-start stratejisi:** Önce öğretmen ve öğrenci rollerini tekil/bağımsız kullanımda değerli kıl (takvim,
  ders programı), kitle oluşunca özel ders bulma/eşleştirme özelliğini aç.
- **Öğretmen rolü:** Ders takvimi (dersler, ödevler, notlar, tatiller, ödemeler) merkezde; öğrenci ekleme iki
  yoldan olur — (1) öğrenci gerçekten hesap açıp bağlanır, (2) öğrenci uygulamayı reddederse öğretmen onu manuel
  ekler. Ders planlama tek seferlik veya tekrarlı, online (link ile) veya yüz yüze olabilir. Öğretmen eşleşmiş
  veliyi görebilir (öğrenci manuel olabilir ama veli her zaman gerçek kişi olmalı). Ödeme sistem üzerinden
  alınmaz — öğretmen manuel işaretler, bilgi veliyle paylaşılır. Öğretmen not/kaynak/ödev verir, ödev takibi,
  aylık gelir istatistik/rapor ve öğrenci gelişim takibi yapar.
- **Öğrenci rolü:** Öğretmensiz de kullanılabilir (kendi ders programı). Öğretmenle eşleşme varsa o ders program
  otomatik eklenir; çakışmada **özel ders önceliklidir**, öğrenci uyarılır. Hedef belirleme ve takip (ör. deneme
  net hedefi, doğru/yanlış girişiyle artış/azalış analizi), motivasyon için seri (streak) ve başarımlar, ders
  odağını ölçmek için kronometre, konu bazlı eksik/gelişim takibi. Öğretmenle ders alan öğrenci ödev yükler; süre
  geçerse veliye bildirim gidebilir; öğretmenin paylaştığı not/kaynağı görür.
- **Veli rolü:** Birden fazla öğrencisi olabilir; öğrencilerin ders durumu/hedef/gelişimini, ödemeleri ve
  öğretmen-öğrenci etkileşimlerini takip eder. Gelişim takibi grafik/rapor ile desteklenmeli.
- **Mesajlaşma:** Öğretmen↔öğrenci ve öğretmen↔veli arasında olmalı.
- **Premium/ücretsiz model:** Bedava ve ücretli üyelik bir arada; ücretli üyelik reklamsız + sınırsız + ekstra
  özellikli. Gelir modeli reklam + üyelik. Büyüme kampanyaları: ilk ay ücretsiz, arkadaş getirince 1 ay ücretsiz.
- **Profil/bildirim:** Üç rol de profilini ve bildirim izinlerini düzenleyebilmeli.
- **Eşleştirme/keşif (ikinci aşama):** Öğretmen kendi ilanını, öğrenci aradığı dersin ilanını verebilmeli.
- **Güven sistemi:** Yıldız + yorum sistemi, bildirme/şikayet mekanizması, kullanıcıdan bug/geri bildirim alma.
- **Sıralama sinyali:** İlan sıralamasında konum yakınlığı ve yüksek yıldız + ücretli üyelik öne çıkarılmalı.

> Not: Bu makale promp.txt'yi cite eder, kanonik gerçeği ezmez — güncel ürün kapsamı ve kesinleşmiş kararlar için
> PRD v2.1 esastır; çelişki halinde PRD (ve koddan doğrulanmış mimari dokümanlar) geçerlidir.

## İlgili

- [PRD](../ozel_ders_platformu_PRD_v2.md)
- [modüller](../modules/00_genel_bakis.md)
- [roller](../roles/00_roller_genel_bakis.md)

## Kaynak

[promp.txt](../promp.txt)

*Güncelleme: 2026-08-20*
