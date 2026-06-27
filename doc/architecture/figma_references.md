# 🎨 Figma Referansları — EğitimÜssü

> **Kapsam:** Figma tasarım dosyalarına referanslar, ekran–component eşlemeleri ve tasarım–kod uyum kuralları.
>
> **Güncelleme:** 2026-06-27

---

## 1. Figma Dosyası

> ⚠️ Henüz Figma dosyası oluşturulmamış. Tasarım kararları `design_system.md` token'larına dayanır.
> Figma bağlandığında bu dosyaya URL ve bileşen eşlemesi eklenecek.

| Alan | Değer |
|------|-------|
| **Ana dosya URL** | — (henüz yok) |
| **Design System sayfası** | — |
| **Mobil ekranlar sayfası** | — |
| **İkon seti** | Material Icons (`Icons.*`) — Figma'da `material-symbols` |

---

## 2. İkon Kullanım Kuralları

Figma'da tasarlanmış ikonlar koda şu şekilde yansıtılır:

| Figma ikon | Flutter karşılığı |
|-----------|------------------|
| `home` | `Icons.home_rounded` |
| `person` | `Icons.person_rounded` |
| `calendar` | `Icons.calendar_today_rounded` |
| `assignment` | `Icons.assignment_rounded` |
| `payments` | `Icons.payments_rounded` |
| `notifications` | `Icons.notifications_rounded` |
| `settings` | `Icons.settings_rounded` |
| `chevron_right` | `Icons.chevron_right_rounded` |
| `add` | `Icons.add_rounded` |
| `edit` | `Icons.edit_rounded` |
| `delete` | `Icons.delete_rounded` |
| `check` | `Icons.check_circle_rounded` |
| `warning` | `Icons.warning_rounded` |
| `close` | `Icons.close_rounded` |
| `search` | `Icons.search_rounded` |

> **Kural:** Daima `_rounded` varyantını tercih et — köşeli ikonlar yerine yumuşak formlar projenin tonuyla uyumludur.

---

## 3. Ekran–Bileşen Eşlemesi (Planlanan)

Figma dosyası oluşturulduğunda her ekran için şu bilgiler buraya eklenir:

```
| Figma Ekranı        | Flutter Sayfası                     | Durum   |
|---------------------|-------------------------------------|---------|
| Teacher Dashboard   | TeacherDashboardPage                | 🟡      |
| Student List        | StudentListPage                     | 🔴      |
| Lesson Detail       | LessonDetailPage                    | 🔴      |
| ...                 | ...                                 | ...     |
```

---

## 4. Tasarım → Kod Aktarım Kuralları

Figma tasarımı geldiğinde şu adımlar izlenir:

1. **Token kontrolü:** Figma'daki renk/spacing değerleri `design_system.md` token'larıyla eşleşmeli.
   Farklıysa Figma güncellenir, token değiştirilmez (token kanonik kaynaktır).

2. **Bileşen kontrolü:** Figma'daki bileşen `widgets.md`'de var mı?
   - 🟢 Varsa: Import et, yeniden yazma.
   - 🔴 Yoksa: `shared/widgets/` altına yaz, kataloğu güncelle.

3. **Responsive:** Flutter'da `LayoutBuilder` veya `MediaQuery` ile 360-430px arası optimize et.
   Tablet desteği şu an kapsam dışı.

4. **Figma Auto Layout → Flutter:**
   - Horizontal Auto Layout → `Row`
   - Vertical Auto Layout → `Column`
   - Gap → `SizedBox(width/height: AppSpacing.X)` veya `gap` parametresi

5. **Figma Fill Container → Flutter:** `Expanded` veya `double.infinity` genişlik.

---

## 5. Tasarım Kararı Geçmişi

Figma olmadan alınan önemli kararlar — Figma bağlandığında bu kararlar dosyaya yansıtılır:

| Karar | Gerekçe | Tarih |
|-------|---------|-------|
| Ana renk `#082B4F` (lacivert) | Güven, profesyonellik, eğitim sektörü tonu | 2026-05 |
| Font: Inter | Nötr, okunabilir, tüm platformlarda desteklenen | 2026-05 |
| Kart radius: 16px | Modern ama abartısız yuvarlama | 2026-05 |
| Bottom nav max 5 sekme | Bilişsel yük sınırı | 2026-06 |
| Scaffold bg: `#F7F9FC` | Tam beyazdan daha az yorucu | 2026-05 |

---

*Figma Referansları | Güncelleme: 2026-06-27*
