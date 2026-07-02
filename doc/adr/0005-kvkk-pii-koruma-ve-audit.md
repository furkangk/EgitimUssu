# ADR-0005: KVKK/PII koruma & denetim (audit) stratejisi

- **Durum:** Önerildi (Proposed)
- **Tarih:** 2026-06-30
- **Karar vericiler:** Ürün + Backend + Hukuk/uyum
- **İlgili:** Güvenlik denetimi (sırlar, PII, audit eksikliği). Platform reşit olmayan (öğrenci) verisi işliyor.

## Bağlam ve Problem

EğitimÜssü; öğrenci (çoğu reşit değil), veli, öğretmen kişisel verisi + ödeme verisi işleyen bir platform. Mevcut durumda:

- **PII şifrelemesi yok.** Ad/soyad, e-posta, telefon, ödeme tutarları düz saklanıyor (encryption-at-rest yalnız DB sağlayıcı seviyesinde, alan bazında değil).
- **Audit log yok.** Admin işlemleri, rol değişimi, ödeme güncellemesi, hassas veri erişimi izlenmiyor → kim ne yaptı sorusu yanıtsız.
- **Veri saklama / silme politikası yok.** "Unutulma hakkı" (right-to-erasure), hesap kapanışında veri yaşam döngüsü tanımsız.
- **Açık rıza (consent) takibi yok.**
- **Sır yönetimi env-only.** JWT key vb. yalnız ortam değişkeni; vault yok, rotasyon yok (Y3).
- Log hijyeni iyi (parola/token loglanmıyor) ama bu PII stratejisi yerine geçmez.

KVKK (ve AB için GDPR) açısından bunlar yalnız "iyi olur" değil, **yasal yükümlülük**; bir eğitim platformu için aynı zamanda güven meselesi.

## Karar Etkenleri

- **Yasal uyum:** KVKK/GDPR — veri minimizasyonu, amaç sınırlaması, silme hakkı, ihlal bildirimi.
- **Güven:** reşit olmayan veri için yüksek hassasiyet beklentisi.
- **Güvenlik:** veri ihlalinde etki azaltımı (şifreli alanlar).
- **Denetlenebilirlik:** hassas işlemlerin değişmez kaydı.

## Değerlendirilen Seçenekler

### Seçenek A — Mevcut durum + nokta düzeltmeler
- ➕ Hızlı.
- ➖ Yasal risk sürer; ihlalde etki yüksek; denetim yok.

### Seçenek B — Bütünsel KVKK/PII çerçevesi (fazlı)
1. **Alan bazında PII şifreleme** (hassas alanlar için encryption-at-rest; anahtar vault'ta).
2. **Değişmez audit log** — admin/hassas işlemler (rol değişimi, ödeme, veri erişimi) için ayrı, salt-ekle (append-only) kayıt.
3. **Veri saklama + silme** — retention politikası + hesap kapanışında "unutulma hakkı" akışı (anonimleştirme/silme).
4. **Açık rıza takibi** — kayıt/onam sürümleme.
5. **Sır yönetimi** — vault + anahtar rotasyon (Y3 ile).
- ➕ Yasal uyum + ihlal etkisi azalır + denetlenebilirlik + güven.
- ➖ Önemli mühendislik eforu; şifreli alanlarda sorgulama kısıtı (arama/indeks tasarımı); fazlı planlama gerektirir.

## Karar

**Seçenek B, fazlı.** Öncelik: (1) sır yönetimi + JWT rotasyon (Y3 — zaten görev), (2) hassas işlemler için audit log, (3) alan bazında PII şifreleme, (4) saklama/silme akışı, (5) rıza takibi. MVP'de en azından **audit log + silme akışı + sır yönetimi** hedeflenir; tam şifreleme bir sonraki faza yayılabilir.

## Sonuçlar

- ✅ **Olumlu:** KVKK/GDPR uyumuna giden net yol; ihlalde veri etkisi azalır; admin işlemleri denetlenebilir; kullanıcı güveni.
- ⚠️ **Olumsuz / maliyet:** Şifreli alanlarda arama/indeks tasarımı zorlaşır (deterministik şifreleme vs. arama dengesi); audit log depolama/erişim politikası; eforun fazlara yayılması.
- 🔭 **Riskler / izlenecekler:** Anahtar yönetimi (kayıp = veri kaybı); audit log'un kendisinin PII içermesi; silme akışının yedeklerle tutarlılığı.

## Uygulama Notları

- Audit log'u ayrı şema/tablo + salt-ekle; her kayıtta aktör, eylem, kaynak, zaman, correlation id (mevcut `RequestContextLoggingMiddleware` ile bağ).
- PII şifreleme için hassas alan envanteri çıkar; deterministik (aranabilir) vs. rastgele şifreleme kararını alan bazında ver.
- Silme/anonimleştirme akışını modüller-arası event ile yay (hesap kapanışı → tüm modüller temizlik) — ADR-0001 mesajlaşmasıyla uyumlu.
- Sırları vault'a taşı; JWT key rotasyonunu Y3 ile birlikte planla.
