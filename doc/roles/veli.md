# 👪 Veli Rolü — Detaylı Tasarım Dokümanı

> **Öncelik: 3️⃣** · **Faz 2-3 — Veli Paneli** · **Durum: 🟡 Kısmi (Faz 2 uygulandı)**
>
> **Amaç:** Veli, çocuğunun gelişimini **şeffaf** ve **grafik/rapor ağırlıklı** biçimde takip etsin; özel ders alıyorsa
> ödemeleri ve öğretmen etkileşimlerini izlesin.
>
> İlgili: [`00_roller_genel_bakis.md`](00_roller_genel_bakis.md) · [`ogrenci.md`](ogrenci.md) · [`ogretmen.md`](ogretmen.md) · [`../modules/m09_parents.md`](../modules/m09_parents.md)
> **Güncelleme:** 2026-07-19

---

## 1. Tasarım İlkesi
Veli, **kendi verisi üretmeyen**, çoğunlukla diğer modüllerin verisini veli perspektifinden **okuyan/birleştiren** bir roldür (read-model). Velinin önceliği çocuğun **gelişim takibidir** ve bu güçlü **grafik ve raporlarla** desteklenmelidir.

> **Önemli kural:** Veli **yalnızca gerçek, kayıtlı bir kullanıcı** olabilir (öğrenci manuel olabilir; veli olamaz). Veli–çocuk bağı **onaya** dayalıdır ve bir velinin **birden çok çocuğu** olabilir.

---

## 2. Kullanıcı Tipi ve Giriş
- Kayıt: `Parent` rolüyle (gerçek kişi) — [`../modules/m01_identity.md`](../modules/m01_identity.md) (`UserRole.Parent = 4`).
- Çocuğa bağlanma: davet kodu / öğrenci e-postası ile **onaylı** bağ (`ParentChildLink`). Bağ noktası kodda hazır: `StudentProfile.ParentUserId`.

---

## 3. İki Veri Kaynağı (PRD M09)

| Kaynak | İçerik | Önkoşul | Modül |
|--------|--------|---------|-------|
| **Bireysel çalışma** | Haftalık çalışma süresi, konu dağılımı, test performansı, seri | Öğretmen gerekmez | [`m08_study`](../modules/m08_study.md) |
| **Öğretmen bağlıysa** | Son ders özeti, ödevler, öğretmen notları, **ödeme özeti** | Öğrenci bir öğretmene bağlı | [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md), [`m06_assignments`](../modules/m06_assignments.md), [`m07_payments`](../modules/m07_payments.md) |

---

## 4. Yetenek Haritası

| Yetenek | Modül | Durum |
|---------|-------|-------|
| Veli profili + çocuğa onaylı bağ (çoklu çocuk) | [`m09_parents`](../modules/m09_parents.md) | 🟢 |
| Bireysel çalışma görünümü (veli paneli/dashboard) | [`m09_parents`](../modules/m09_parents.md) | 🟢 (Veli V-F: canlı digest — çalışma "0" bug fix) |
| Zengin panel: çalışma dağılımı + yaklaşan/son ders + öğretmen notları + ödeme detay | [`m09_parents`](../modules/m09_parents.md) | 🟢 (Veli V-F) |
| Bildirim tercihleri (ödev kaçırma/haftalık özet/ders/test/ödeme + kanal) | [`m09_parents`](../modules/m09_parents.md) | 🟢 |
| Çocuğun ders durumu/programı | [`m04_scheduling`](../modules/m04_scheduling.md) / [`m05_lesson_sessions`](../modules/m05_lesson_sessions.md) | 👁️ |
| Çocuğun **hedef + gelişim** (grafik/rapor) | [`m10_progress_tracking`](../modules/m10_progress_tracking.md) / [`m14_reporting`](../modules/m14_reporting.md) | 🔴 |
| Bireysel çalışma verisi (süre/test/seri) | [`m08_study`](../modules/m08_study.md) | 🔴 |
| **Özel ders ödemeleri** (paylaşılırsa) | [`m07_payments`](../modules/m07_payments.md) | 🟢 veri / ⚠️ paylaşım bayrağı |
| **"Ödedim" beyanı** (öğretmen teyitli mutabakat) | [`m07_payments`](../modules/m07_payments.md) | 🟢 (Veli V-G) |
| Öğretmen-öğrenci **etkileşimleri** | [`m05`](../modules/m05_lesson_sessions.md)/[`m06`](../modules/m06_assignments.md) | 👁️ |
| Öğretmenle **mesajlaşma** | [`m16_messaging`](../modules/m16_messaging.md) | 🔴 |
| Bildirimler (ödev kaçırma vb.) | [`m11_notifications`](../modules/m11_notifications.md) | 🟡 |
| **Veli bildirim motoru** (olay + haftalık özet, **Premium**) | [`m11_notifications`](../modules/m11_notifications.md) | 🟢 (Veli V-E) |
| Profil/bildirim/üyelik | [`m15_settings`](../modules/m15_settings.md) / [`m17_membership`](../modules/m17_membership.md) | 🟡/🔴 |

---

## 5. Altın Akış (Golden Path)

```
Kayıt (Parent, gerçek kişi) → çocuğa bağlan (davet/e-posta → onay)
  → Veli paneli: bu hafta kaç saat çalıştı, hangi derslere ne kadar
    → Test performansı + gelişim grafikleri
      → (öğretmen bağlıysa) son ders, ödevler, öğretmen notları, ödeme özeti
        → Öğretmenle mesajlaş → ödev kaçırma bildirimleri
```

---

## 6. Rol-Özel İş Kuralları

1. **Gerçek kişi zorunlu** (promp): veli manuel olamaz.
2. **Onaylı bağ:** Veli–çocuk bağı onay gerektirir (özellikle büyük yaş grubu için); KVKK gereği reşit öğrencide öğrenci onayı esas, reşit olmayanda veli erişimi varsayılan.
3. **Çoklu çocuk:** Bir veli birden çok öğrenciye bağlanabilir; panelde çocuk seçici bulunur.
4. **Yalnız görüntüleme:** Veli ders/ödev/ödeme verisini **düzenleyemez**, yalnızca görüntüler.
5. **Gizlilik:** Öğrenci, hangi verilerin veliye yansıyacağını kontrol edebilir (M15); ödeme yalnızca `IsSharedWithParent` ise görünür (M07). **(Veli V-B, 2026-07-19 — uygulandı)** Öğrenci `PUT /api/settings/users/{userId}/study-sharing` ile çalışma verisi paylaşımını kapatabilir; kapalıysa veli panelinde çalışma alanları (haftalık dakika, streak) **"paylaşılmıyor"** işaretiyle (`IsShared=false`, değer 0) döner — değer sızmaz. **Değişmez kural:** çocuğun kişisel seans notu veliye hiçbir koşulda açılmaz.
6. **Ödev kaçırma bildirimi:** Öğrenci ödevini son tarihten önce yüklemezse veliye bildirim gider (M06 + M11).
7. **Sessizce bağlanma yok + birincil veli (Veli V-C, 2026-07-19 — uygulandı):** Bir bağ onaylandığında şeffaflık olayı (`ParentLinkConnectionNoticeDomainEvent`) yayılır — çocuk ve varsa mevcut birincil veli "X hesabı veli olarak bağlandı" bilgilendirilir (teslim V-E). Bir çocuğun tek **birincil velisi** olabilir; ikinci veli birincil olmak isterse mevcut birincil veli (veya admin) onaylamadıkça olamaz (`parents.primary_exists`, 409). Yani veli, çocuk/mevcut veli **haberdar edilmeden bağlanamaz**.
8. **Öğretmen→veli davet kodu (Veli V-D, 2026-07-19 — uygulandı):** Öğretmen bir öğrenci için veli davet kodu üretir (`POST /api/students/profiles/{studentId}/parent-invite`); veli kaydolup kodu `POST /api/parents/children/claim-invite` ile girerek çocuğuna bağlanır. Kod girmek onay eylemidir → bağ doğrudan **Approved** (öğretmen kodu = öğretmen onayı, veli kod = veli onayı). İlk veli birincil olur. Telefon eşleştirme yok; kod modeli.
9. **"Ödedim" beyanı (Veli V-G, 2026-07-19 — uygulandı):** Veli bir ödeme kaydı için "ödedim" beyan eder (`POST /api/payments/records/{id}/declare-paid`) → öğretmene bildirim gider (teslim V-E) → öğretmen **teyit** edince (`.../confirm`) kayıt tam tahsil edilmiş (`Paid`) işaretlenir; reddederse kayıt değişmez. **Para transferi değildir**, mutabakat kaydıdır (PRD "platform para tahsil etmez" korunur). Yetki: yalnız onaylı veli beyan eder, yalnız ilgili öğretmen teyit eder.
10. **Bildirimler Premium (Veli V-E, 2026-07-19 — uygulandı):** Veli bildirimleri (yeni ödev, ders tamamlandı, ödeme güncellemesi, bağlantı bildirimi + haftalık özet) yalnız **Premium** veliye gider (`ParentProfile.MembershipTier`; PRD 9.3) ve velinin tercih anahtarlarına saygılıdır. Bağlantı bildirimi güvenlik gereği koşulsuz (yine de Premium). Satın alma altyapısı olmadığından başlangıçta tüm veliler Free — Premium yalnız Admin `PUT /membership-tier` ile verilir. Liste: `GET /api/notifications/parents/{parentUserId}/notifications`.
11. **Zenginleştirilmiş panel (Veli V-F, 2026-07-19 — uygulandı):** Veli paneli artık canlı digest'lerle beslenir: çalışma süresi + **ders bazlı dağılım** (panelde "hep 0" bug'ı düzeltildi), yaklaşan dersler, son ders özeti (konu), **veli-görünür öğretmen notları** (yalnız Student/StudentAndParent; Private asla), ödeme kalem listesi. Çalışma verisi gizlilik kapalıysa maskeli döner (V-B); kişisel seans notu hiçbir koşulda görünmez.

---

## 7. Mobil Ekranlar

**Mevcut ✅** (`mobile/lib/features/parent/`, rota grubu `/parent`, `ParentBottomNav`):
`parent_home_page` (çocuk seçici + haftalık KPI kartları + haftalık çalışma çubuk grafiği + ödeme özeti),
`parent_children_page` (bağlı çocuklar + durum rozetleri + "çocuk bağla" bottom-sheet), `parent_child_detail_page`
(çalışma/ders/ödev/ödeme detayı), `parent_notifications_page` (bildirim tercihleri + kanal seçimi), `parent_profile_page` (profil + çıkış).
Rol bazlı yönlendirme uygulandı: `session.roles` 'Parent' içeriyorsa `/parent`.

**Planlanan ⚠️:** Öğretmenle mesajlaşma; M08 verisi gelince gerçek çalışma süresi/streak; gelişim grafikleri (donut/line, M10/M14).

---

## 8. Bireysel vs Eşleşmiş Kullanım
- **Öğretmensiz (Faz 2):** Çocuğun bireysel çalışma verisini izler.
- **Öğretmen bağlıysa (Faz 3):** Ders/ödev/öğretmen notu + ödeme özeti + öğretmenle mesajlaşma eklenir.

## 9. Üyelik Etkisi (Free/Premium)
Premium veli: reklamsız, detaylı gelişim grafikleri, haftalık rapor, çalışma süresi geçmişi, gelişmiş bildirimler. Free: temel özet + reklam (PRD §9.3, [`../modules/m17_membership.md`](../modules/m17_membership.md)).
> **Kod gerçeği (Veli V-E, 2026-07-19):** `ParentProfile.MembershipTier` (Free/Premium) eklendi ve **veli bildirim motoru (M11) Premium kapılı** çalışır. Satın alma/aile paketi altyapısı henüz yok — tier yalnız Admin `PUT /api/parents/{id}/membership-tier` ile set edilir (sonraki faz: V-Premium).

## 10. Kabul Kriterleri
**Faz 2 (öğretmensiz):**
- [x] Veli profili + çocuğa onaylı bağ (çoklu çocuk).
- [~] Çocuğun bireysel çalışma verisi (süre, konu, test, seri) — panel + read-model hazır, **M08 verisi bekliyor**.
- [x] İzin bazlı görünürlük (yalnız `Approved` bağ, salt-okunur) + bildirim tercihleri.

**Faz 3 (öğretmen verisi):**
- [ ] Son ders, ödevler, öğretmen notları, ödeme özeti.
- [ ] Öğretmenle mesajlaşma + ödev kaçırma bildirimi.

## 11. İlişkili Dokümanlar
- Çocuğun verisi → [`ogrenci.md`](ogrenci.md) · Öğretmen verisi → [`ogretmen.md`](ogretmen.md)
- Teknik → [`../modules/m09_parents.md`](../modules/m09_parents.md), [`m07_payments`](../modules/m07_payments.md), [`m10_progress_tracking`](../modules/m10_progress_tracking.md), [`m11_notifications`](../modules/m11_notifications.md)

---

*Veli Rolü — Detaylı Tasarım | Güncelleme: 2026-07-19 (Veli V-F: zenginleştirilmiş panel — canlı digest'ler + çalışma verisi bug fix + öğretmen notları görünürlük filtreli; Veli V-E: Premium veli bildirim motoru + `MembershipTier`; Veli V-G: "ödedim" beyanı öğretmen teyitli; Veli V-D: öğretmen→veli davet kodu claim; Veli V-C: bağlantı şeffaflığı + birincil veli; Veli V-B: gizlilik filtresi — çalışma verisi paylaşım kontrolü)*
