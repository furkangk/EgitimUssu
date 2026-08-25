---
title: "Roller — Genel Bakış"
summary: "Roller klasörü indeksi: rol×yetenek matrisi ve roller-arası kurallar (mesajlaşma çiftleri, veli gerçek kişi, üyelik, yetkilendirme mimarisi)"
tags: [rol, genel-bakis, roller-arasi]
authority: derived
updated: 2026-08-19
---

# 👥 Roller — Genel Bakış ve Roller-Arası Kurallar

> Bu klasör (`doc/roles/`), platformu **kullanıcı rolü** perspektifinden anlatır: her rolün yetenekleri,
> kullanıcı yolculuğu (golden path), ekranları ve rol-özel iş kuralları. **Teknik domain/API detayları**
> ise modül dokümanlarındadır (`doc/modules/mNN_*.md`). Bir rol birden çok modülü kullanır; bir modül birden çok role hizmet eder.
>
> İlgili: [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md) (ürün) · [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md) (modül indeksi) · [`../INDEX.md`](../INDEX.md)
>
> **Güncelleme:** 2026-08-19

---

## 1. Temel Strateji — "Bireysel Önce, Sonra Eşleştirme"

Platform iki parçalı bir değer sunar ve **kasıtlı bir sırayla** büyür (PRD §2, promp vizyonu):

1. **Bireysel + ortak kullanım önce:** Öğretmen kendi öğrencilerini/derslerini yönetir; öğrenci kendi çalışma programını/gelişimini takip eder; veli çocuğunu izler. Bu, her rolün **tek başına** değer bulmasını sağlar.
2. **Eşleştirme sonra:** Yeterli öğretmen + öğrenci kitlesi oluşunca **özel ders bulma/ilan (M12)** açılır. Böylece "ilk gün boş pazar yeri" sorunu yaşanmaz (ilk kaydolan kimseyi göremez/mesaj alamaz sorunu çözülür).

> **Sonuç:** Sistem baştan eşleştirmeye **uygun** tasarlanır ama eşleştirme en son devreye girer. Roller-arası ilişkiler (öğretmen↔öğrenci↔veli) bu yüzden ilk günden modellenir.

---

## 2. Roller

| Rol | Kimdir | Bireysel kullanım | Eşleşmiş kullanım |
|-----|--------|-------------------|-------------------|
| 👨‍🏫 [Öğretmen](ogretmen.md) | Özel ders veren | Kendi öğrenci/ders/ödeme yönetimi | İlan + eşleşen öğrencilere ders |
| 🎓 [Öğrenci](ogrenci.md) ([UX](ogrenci_ux.md)) | Ders alan / çalışan | Kendi program + çalışma + gelişim takibi | Öğretmenle ders + ödev |
| 👪 [Veli](veli.md) | Öğrenci yakını | — (tek başına anlamsız) | Çocuğunun gelişim/ders/ödeme takibi |
| 🛡️ [Admin](admin.md) | Platform yöneticisi | Doğrulama, moderasyon, destek | — |

> Kod tarafında roller `Identity` modülünde: `UserRole = Admin(1), Teacher(2), Student(3), Parent(4)` (bkz. [`../modules/m01_identity.md`](../modules/m01_identity.md)).

---

## 3. Rol × Yetenek Matrisi (özet)

| Yetenek | Öğretmen | Öğrenci | Veli |
|---------|:-------:|:------:|:----:|
| Profil + bildirim izinleri | ✅ | ✅ | ✅ |
| Takvim / ders programı | ✅ (yönetir) | ✅ (kendi + özel ders) | 👁️ (görür) |
| Öğrenci ekleme/takip | ✅ | — | — |
| Ders oturumu işleme/not | ✅ | 👁️ | 👁️ |
| Ödev verme | ✅ | — | — |
| Ödev yükleme/takip | — | ✅ | 👁️ |
| Ders notu + kaynak | ✅ (paylaşır) | 👁️ | 👁️ |
| Ödeme (manuel) | ✅ (takip) | — | 👁️ (paylaşılırsa) |
| Bireysel çalışma (kronometre/test/seri) | — | ✅ | 👁️ |
| Hedef + konu gelişimi | 👁️ (öğrencisinin) | ✅ | 👁️ |
| Gelir/istatistik/rapor | ✅ | — | — |
| Gelişim grafik/rapor | ✅ (öğrencisinin) | ✅ (kendi) | ✅ (çocuğunun) |
| Mesajlaşma | ✅ ↔ öğrenci/veli | ✅ ↔ öğretmen | ✅ ↔ öğretmen |
| İlan verme | ✅ (sunduğu) | ✅ (aradığı) | — |
| Puanlama/yorum | 👁️ (alır, yanıtlar) | ✅ (yapar) | — |
| Üyelik (free/premium) | ✅ | ✅ | ✅ |

✅ yapar · 👁️ görüntüler · — yok

---

## 4. Roller-Arası Kurallar (Cross-Cutting)

### 4.1 Mesajlaşma çiftleri (M16)
Mesajlaşma **yalnızca** şu çiftler arasında: **öğretmen ↔ öğrenci** ve **öğretmen ↔ veli**.
Öğrenci↔veli, öğrenci↔öğrenci, öğretmen↔öğretmen mesajlaşması **yoktur** (bkz. [`../modules/m16_messaging.md`](../modules/m16_messaging.md)).

### 4.2 Veli yalnızca gerçek kişi
Öğrenci **manuel** (öğretmenin eklediği, hesabı olmayan) olabilir; ancak **veli yalnızca gerçek, kayıtlı bir kullanıcı** olabilir.
Veli–çocuk bağı onaya dayalıdır ve bir velinin birden çok çocuğu olabilir (bkz. [`../modules/m09_parents.md`](../modules/m09_parents.md), [`../modules/m03_students.md`](../modules/m03_students.md)).

### 4.3 Öğretmen–öğrenci eşleşmesi ve çakışma önceliği
Öğrenci bir öğretmenle özel ders için eşleşirse, o ders **otomatik olarak öğrencinin programına** eklenir.
Öğrencinin kendi planı ile özel ders **çakışırsa önceliği özel ders** alır ve öğrenci uyarılır
(bkz. [`../modules/m04_scheduling.md`](../modules/m04_scheduling.md), [`../modules/m08_study.md`](../modules/m08_study.md)).

### 4.4 Bir öğrenci birden fazla öğretmene bağlanabilir (Dilim C, 2026-07-18)
Öğretmen–öğrenci ilişkisi çok-öğretmenlidir: aynı öğrenci farklı branşlar için **birden fazla öğretmene** bağlanabilir.
Her bağ ayrı bir `TeacherStudentLink`'tir (`(TeacherUserId, StudentId)` benzersiz); öğretmenin öğrenci listesi bu bağ üzerinden yürür.
Free planda öğretmen başına **en fazla 5 aktif bağ** vardır (arşivli dâhil, reddedilen hariç); öğrenci bazlı ücret ve arşivleme bağ üzerinde tutulur.
Manuel öğrenci **davet/kabul** ile gerçek öğrenci hesabına bağlanır (bkz. [`../modules/m03_students.md`](../modules/m03_students.md)).

### 4.5 Üyelik (free/premium) tüm rollerde
Her rol için ücretsiz ve ücretli üyelik vardır. Ücretsiz kullanıcılar **reklam görür** ve **limitlere** tabidir; ücretli kullanıcılar reklamsız + sınırsız + ekstra özelliklere sahiptir. Kampanyalar: ilk ay ücretsiz, arkadaşını getir → 1 ay ücretsiz (bkz. [`../modules/m17_membership.md`](../modules/m17_membership.md)).

### 4.6 Profil ve bildirim izinleri
Üç rol de kendi profilini düzenler ve bildirim izinlerini ayarlar (bkz. [`../modules/m15_settings.md`](../modules/m15_settings.md)).

### 4.7 Gizlilik / veri paylaşımı
Öğrenci, bireysel çalışma verisini veli/öğretmenle paylaşıp paylaşmayacağını kontrol eder; ödeme bilgisi veliyle ayrı bir bayrakla paylaşılır (M15 `ShareStudyDataWith*`, M07 `IsSharedWithParent`). KVKK: reşit olmayan öğrencilerde veli erişimi varsayılan.

### 4.8 Roller-arası akışta 3 mimari gerçek
Rol yetkileri kodda nasıl zorlanır (arşivlenen `is_akislari.md`'den taşındı; teknik detay → [`../modules/mimari_inceleme.md`](../modules/mimari_inceleme.md), [`../architecture/backend.md`](../architecture/backend.md)):
1. **HTTP katmanında rol kontrolü yoktur.** Tüm endpoint'ler yalnızca `RequireAuthorization("AuthenticatedUser")` der; rol/sahiplik kararı Application katmanındaki `ICommandAuthorizer`/`IQueryAuthorizer` sınıflarında verilir. Bu belgelerdeki "hangi rol ne yapar" ifadeleri bu authorizer'lardan okunur, endpoint attribute'undan değil.
2. **Varsayılan-deny zorunludur.** Authorizer'ı olmayan bir handler uygulamayı başlangıçta çökertir (`AuthorizationCoverageValidator`) — "yetki kontrolü unutuldu" durumu mimari olarak imkânsızdır (mimari_inceleme K3).
3. **Her domain event otomatik integration event olur.** `SaveChanges` anında aynı transaction içinde outbox'a yazılır; modüller birbirini **string event adıyla** dinler, proje referansıyla değil. Roller-arası veri akışı (ör. öğretmenin ders tamamlaması → veli panosu) bu gevşek olay zinciriyle yürür.

---

## 5. Giriş Yolları (Onboarding)

```
Welcome → Rol seçimi
  ├─ Öğretmen → kayıt → profil → öğrenci ekle → takvim...
  ├─ Öğrenci  → kayıt (öğretmensiz) → kendi programı + bireysel çalışma...
  └─ Veli     → kayıt (gerçek kişi) → çocuğa bağlan (onaylı) → izleme...
```

> Mevcut mobil uygulama **öğretmen odaklıdır**; öğrenci ve veli ekranları büyük ölçüde **planlanandır** (bkz. [`../pages/00_pages_index.md`](../pages/00_pages_index.md)).

---

## 6. İlişkili Dokümanlar
- Detaylı rol docs: [`ogretmen.md`](ogretmen.md) · [`ogrenci.md`](ogrenci.md) · [`veli.md`](veli.md) · [`admin.md`](admin.md)
- Modül indeksi ve teknik detay: [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md)
- Ürün gereksinimleri: [`../ozel_ders_platformu_PRD_v2.md`](../ozel_ders_platformu_PRD.md)

---

*Roller Genel Bakış | Güncelleme: 2026-08-19 (doküman temizliği: §4.8 roller-arası 3 mimari gerçek arşivlenen `is_akislari.md`'den taşındı) · 2026-07-18 (Dilim C: bir öğrenci birden fazla öğretmene bağlanabilir — `TeacherStudentLink`, free limit=5, davet/kabul — §4.4)*
