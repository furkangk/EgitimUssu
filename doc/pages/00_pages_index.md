---
title: "Sayfa (Ekran) Dokümanları — İndeks"
summary: "doc/pages/ altındaki tüm sayfa dokümanlarının indeksi: route, dosya, veri durumu tablosu (Auth/Dashboard/Students/Study/Lesson Sessions/Scheduling/Payments/Assignments/Teacher Profile/More/Notifications/Parent)"
tags: [sayfa-index, indeks, mobil]
authority: derived
updated: 2026-08-20
---

# 📱 Sayfa (Ekran) Dokümanları — İndeks

> Her dosya, kodda **var olan** bir Flutter ekranını belgeler (route, dosya, state, veri kaynağı, durum).
> Kaynak: `mobile/lib/core/routing/app_router.dart` + feature klasörleri.
> **Yeni ekran eklendiğinde buraya bir satır + ekranın md'si eklenir** (bkz. kökteki `CLAUDE.md`).
> **Son güncelleme:** 2026-08-20 (kod-drift düzeltmesi: payments_list/students_list rozetleri 🔴→🟡 — gerçek API + demo fallback) · 2026-08-19 (temizlik Geçiş 3 — kod-senkron: eksik `/notifications` ekranı eklendi; ekran envanteri koddan doğrulandı)

**Veri durumu:** 🟢 gerçek API'ye bağlı · 🟡 karışık (kısmen demo) · 🔴 tamamen demo/UI.

## Auth
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Karşılama | `/` | [auth_welcome](auth_welcome.md) | — |
| Hesap türü seçimi | `/role-selection` | [auth_role_selection](auth_role_selection.md) | — |
| Giriş | `/login` | [auth_login](auth_login.md) | 🟢 |
| Kayıt | `/register` | [auth_register](auth_register.md) | 🟢 |

## Dashboard
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Öğretmen paneli | `/dashboard` | [dashboard](dashboard.md) | 🟡 |
| Panel önizleme | `/teacher-panel-preview` | [dashboard_preview](dashboard_preview.md) | 🔴 |

## Students
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Öğrenci listesi | `/students` | [students_list](students_list.md) | 🟡 |
| Öğrenci detayı | `/students/:studentId` | [students_detail](students_detail.md) | 🔴 |

## Study (Öğrenci — Bireysel Çalışma, M08)
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Çalışma (sekme) | `/student-home` | [study_student](study_student.md) | 🟢 |
| Derslerim (sekme) | `/student/lessons` | [study_student](study_student.md) | 🟢 (eski `/student/calendar` redirect'lenir; 2026-07-08: birleşik ders programı — öğretmen dersleri + kendi programı) |
| Ders Detayı (push) | `/student/lessons/:id` | [study_student](study_student.md) | 🟢 gerçek ders bilgisi/ödev/not; 🟡 test-deneme/konu derse `subject` eşleşmesiyle (demo rozetli) bağlanır (Task 5, 2026-08-19) |
| Performans (sekme) | `/student/performance` | [study_student](study_student.md) | 🟢 (eski `/student/tests` redirect'lenir; Task 3: Çalışmalarım'ın haftalık/ders-konu analizi absorbe edildi — `/student/studies` sayfası silindi, rota `/student-home`'a redirect) |
| Profil (sekme) / İstatistik + Ayarlar | `/student/profile` | [study_student](study_student.md) | 🟢 (Task 6, 2026-07-21: Ayarlar menüsü + Çıkış eklendi, AppBar → sayfa içi başlık) |
| Hedefler (Çalış sekmesi kısayolu) | `/student/goals-overview` | [study_student](study_student.md) | 🟢 (Task 6, 2026-07-21: erişim yolu Diğer yerine Çalış kısayolları) |
| Öğretmenlerim (Derslerim → Ders araçları) | `/student/teacher` | [study_student](study_student.md) | 🟡 Yalnızca bağlı öğretmen bilgi kartı 🟢 (dersler Derslerim'de), ödev/not/mesaj yakında |
| Kronometre | `/study/timer` | [study_student](study_student.md) | 🟢 |
| Deneme gir | `/study/test` | [study_student](study_student.md) | 🟢 |
| Hedefler & paylaşım | `/study/goals` | [study_student](study_student.md) | 🟢 |
| Çalışma geçmişi | `/study/history` | [study_student](study_student.md) | 🟢 |
| Rozetler (Çalış sekmesi kısayolu) | `/study/achievements` | [study_student](study_student.md) | 🟢 (Task 6, 2026-07-21: erişim yolu Diğer yerine Çalış kısayolları) |
| Dersler & Konular (Derslerim → Ders araçları) | `/study/catalog` | [study_student](study_student.md) | 🟢 (2026-07-09: ders/konu kataloğu yönetimi) |
| Notlarım (Derslerim → Ders araçları) | `/study/notes` | [study_student](study_student.md) | 🟢 (2026-07-09: öğrenci kendi ders notları) |
| Gelişimim (Performans sekmesi kısayolu) | `/student/progress` | — | 🟡 (2026-07-09: M10 konu hâkimiyeti/eksik-güçlü; Task 6, 2026-07-21: erişim yolu Diğer yerine Performans) |

## Lesson Sessions
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Dersler | `/lesson-sessions` | [lesson_sessions_list](lesson_sessions_list.md) | 🟡 |
| Ders detayı | `/lesson-sessions/detail` | [lesson_detail](lesson_detail.md) | 🔴 |
| Ders notu formu | `/lesson-notes/new` | [lesson_note_form](lesson_note_form.md) | 🔴 |
| Ders notu görüntüleme | `/lesson-sessions/detail/note` | [lesson_note_view](lesson_note_view.md) | 🔴 |

## Scheduling
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Takvim | `/scheduling` | [scheduling](scheduling.md) | 🟡 |

## Payments
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Ödeme takibi | `/payments` | [payments_list](payments_list.md) | 🟡 |
| Ödeme ekle | `/payments/new` | [payment_form](payment_form.md) | 🟢 |

## Assignments
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Ödev/takip | `/assignments/new`, `/assignments/:lessonSessionId` | [assignment_follow_up](assignment_follow_up.md) | 🟢 |
| Ödevlerim (öğrenci) | `/student/assignments` | — | 🟢 (2026-07-09: dosya yükleme + tamamlama) |

## Teacher Profile
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Öğretmen profili | `/teacher-profile` | [teacher_profile](teacher_profile.md) | 🟢 |

## More / Settings
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Diğer/Ayarlar | `/more` | [more](more.md) | 🟡 |
| Hesap bilgileri | `/account-info` (+ `/account-info-preview` alias) | [account_info](account_info.md) | 🔴 |

## Notifications
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Bildirimler | `/notifications` | — | 🟡 (`NotificationsPage` + `NotificationsCubit`; gerçek `/api/notifications/teachers/{id}/lesson-reminders` + mock fallback) |

## Parent (Veli)
> `mobile/lib/features/parent/` · `ParentCubit` · `ParentRepository` (mock fallback) · `ParentBottomNav` · rol bazlı `/parent` yönlendirme (`app_router.dart`).

| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Veli ana sayfa (çocuk seçici + haftalık KPI + çubuk grafik + ödeme özeti) | `/parent` | — | 🟡 |
| Bağlı çocuklar (durum rozetleri + "çocuk bağla" bottom-sheet) | `/parent/children` | — | 🟡 |
| Çocuk detayı (çalışma/ders/ödev/ödeme) | `/parent/child-detail` | — | 🟡 |
| Bildirim tercihleri (switch + kanal) | `/parent/notifications` | — | 🟡 |
| Veli profili (+ çıkış) | `/parent/profile` | — | 🟡 |

---

> **Not:** Bu indeks yalnızca **kodda var olan** ekranları belgeler (öğretmen + **veli** tarafı). **Planlanan** öğrenci
> ekranları ve yeni özellik ekranları (mesajlaşma, ilan/keşif, üyelik/paywall) için rol ve modül dokümanlarına bakın →
> [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md), [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md).
> Yeni ekran kodda eklendikçe buraya bir satır + ekranın md'si eklenir (bkz. kökteki `CLAUDE.md`).
