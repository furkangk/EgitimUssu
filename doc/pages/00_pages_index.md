---
title: "Sayfa (Ekran) Dokümanları — İndeks"
summary: "doc/pages/ altındaki tüm sayfa dokümanlarının indeksi: route, dosya, veri durumu tablosu (Auth/Dashboard/Students/Study/Lesson Sessions/Scheduling/Payments/Assignments/Teacher Profile/Progress/Reporting/Membership/Messaging/More/Notifications/Parent)"
tags: [sayfa-index, indeks, mobil]
authority: derived
updated: 2026-09-04
---

# 📱 Sayfa (Ekran) Dokümanları — İndeks

> Her dosya, kodda **var olan** bir Flutter ekranını belgeler (route, dosya, state, veri kaynağı, durum).
> Kaynak: `mobile/lib/core/routing/app_router.dart` + feature klasörleri.
> **Yeni ekran eklendiğinde buraya bir satır + ekranın md'si eklenir** (bkz. kökteki `CLAUDE.md`).
> **Son güncelleme:** 2026-09-04 (P10 Task 4: `messaging_inbox` + `messaging_chat` sayfa md'leri eklendi; `/messages` ve `/messages/:conversationId` rotaları; giriş noktaları — öğretmen `/more` "Mesajlar", öğretmen öğrenci detayı "Mesaj Gonder", öğrenci `/student/profile` "Mesajlar" ve `/student/teacher` kartında "Mesaj gönder", veli ana sayfa başlık ikonu — D-08) · 2026-09-04 (P09 Task 6: `membership` + `paywall` sayfa md'leri eklendi; `/membership` ve `/membership/paywall` rotaları; `/more` "Uyelik" aktif — D-10/D-15) · 2026-09-04 (P08 Task 5: `progress_overview` + `teacher_reports` sayfa md'leri eklendi; `/student/reports` ve `/teacher/reports` rotaları; `/more` "Raporlar" aktif; veli çocuk detayına gelişim grafikleri) · 2026-09-03 (P05/D-14..D-17: `/settings/notifications` + `/settings/privacy` eklendi; `/more` sahte kartlardan temizlendi; veli ekranlarındaki `mock-parent-user` sentinel'i kaldırıldı) · 2026-09-03 (P04/M06-1: ders detayına "Kaynaklar" sekmesi — 🔴 → 🟡) · 2026-09-03 (P03/D-11: `/notifications` bildirim merkezine dönüştü — sayfa md'si eklendi, 🟡 → 🟢) · 2026-09-02 (P02/D-01, D-02: şifre sıfırlama + e-posta doğrulama ekranları ve `egitimussu://` derin bağlantısı) · 2026-08-20 (kod-drift düzeltmesi: payments_list/students_list rozetleri 🔴→🟡 — gerçek API + demo fallback) · 2026-08-19 (temizlik Geçiş 3 — kod-senkron: eksik `/notifications` ekranı eklendi; ekran envanteri koddan doğrulandı)

**Veri durumu:** 🟢 gerçek API'ye bağlı · 🟡 karışık (kısmen demo) · 🔴 tamamen demo/UI.

## Auth
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Karşılama | `/` | [auth_welcome](auth_welcome.md) | — |
| Hesap türü seçimi | `/role-selection` | [auth_role_selection](auth_role_selection.md) | — |
| Giriş | `/login` | [auth_login](auth_login.md) | 🟢 |
| Kayıt | `/register` | [auth_register](auth_register.md) | 🟢 |
| Şifremi unuttum (kod isteme) | `/password-reset` | [auth_password_reset](auth_password_reset.md) | 🟢 |
| Şifremi unuttum (yeni şifre) | `/password-reset/confirm` | [auth_password_reset](auth_password_reset.md) | 🟢 |
| E-posta doğrulama | `/verify-email` | [auth_email_verification](auth_email_verification.md) | 🟢 |

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
| Ders detayı | `/lesson-sessions/detail` | [lesson_detail](lesson_detail.md) | 🟡 (Kaynaklar sekmesi gerçek API — M06-1 2026-09-03; not/ödev sekmeleri demo) |
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
| Ödevler (öğretmen) | `/assignments` | — | 🟢 (liste + filtre; 2026-09-04: karta dokununca **değerlendirme alt sayfası** — puan 0–100 + geri bildirim, M06-4) |
| Ödev/takip | `/assignments/new`, `/assignments/:lessonSessionId` | [assignment_follow_up](assignment_follow_up.md) | 🟢 |
| Ödevlerim (öğrenci) | `/student/assignments` | — | 🟢 (2026-07-09: dosya yükleme + tamamlama; 2026-09-04: **puan rozeti + öğretmen geri bildirimi**, M06-4) |

## Teacher Profile
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Öğretmen profili | `/teacher-profile` | [teacher_profile](teacher_profile.md) | 🟢 |

## Progress / Reporting (M10 · M14)
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Gelişim (konu dağılımı + snapshot çizgi grafiği) | `/student/progress?studentId=` | [progress_overview](progress_overview.md) | 🟢 (P08 Task 5: `ProgressCubit` + `fl_chart`; öğretmen/veli kapsamı aynı gövdede) |
| Çalışma analizi + PDF rapor | `/student/reports?studentId=` | [teacher_reports](teacher_reports.md) §2 | 🟢 (haftalık çubuk + ders bazlı net + `402/403` premium kilidi) |
| Öğretmen raporları (aylık özet + boş zamanlar) | `/teacher/reports` | [teacher_reports](teacher_reports.md) §1 | 🟢 (M14-1/M14-5 projeksiyonları) |

## More / Settings
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Diğer/Ayarlar | `/more` | [more](more.md) | 🟢 (sahte sheet'ler kaldırıldı — D-14/D-15; **"Raporlar" P08'de**, **"Uyelik" P09 Task 6'da aktifleşti** → `/membership`; "Plus üye" rozeti artık gerçek üyelikten gelir) |
| Üyelik | `/membership` | [membership](membership.md) | 🟢 |
| Paywall | `/membership/paywall` | [paywall](paywall.md) | 🟡 (katalog + deneme gerçek; satın alma P09 Task 4'e bloke) |
| Bildirim ayarları | `/settings/notifications` | [settings_notifications](settings_notifications.md) | 🟢 |
| Gizlilik ve güvenlik | `/settings/privacy` | [settings_privacy](settings_privacy.md) | 🟢 |
| Hesap bilgileri | `/account-info` (+ `/account-info-preview` alias) | [account_info](account_info.md) | 🔴 |

## Messaging (Mesajlaşma)
> `mobile/lib/features/messaging/` · `ConversationsCubit` / `ChatCubit` · `MessagingRepository` · üç rol de aynı ekranları kullanır.

| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Mesajlar (gelen kutusu) | `/messages` | [messaging_inbox](messaging_inbox.md) | 🟡 (liste/rozet gerçek; karşı tarafın **adı** sunucudan gelmiyor, rol etiketi gösterilir) |
| Sohbet | `/messages/:conversationId` | [messaging_chat](messaging_chat.md) | 🟡 (mesaj/gönderim/okundu/engelleme gerçek; şikayet P12, gerçek zamanlı v2) |

## Notifications
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Bildirimler (bildirim merkezi) | `/notifications` | [`notifications.md`](notifications.md) | 🟢 (`NotificationsPage` + `NotificationCenterCubit`; Tümü/Okunmamış filtresi, okundu, `route` derin bağlantısı, zil rozeti — mock fallback yok) |

## Parent (Veli)
> `mobile/lib/features/parent/` · `ParentCubit` · `ParentRepository` (mock fallback) · `ParentBottomNav` · rol bazlı `/parent` yönlendirme (`app_router.dart`).

| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Veli ana sayfa (çocuk seçici + haftalık KPI + çubuk grafik + ödeme özeti) | `/parent` | — | 🟡 (oturum yoksa `SessionRequiredView` — D-17) |
| Bağlı çocuklar (durum rozetleri + "çocuk bağla" bottom-sheet) | `/parent/children` | — | 🟡 |
| Çocuk detayı (çalışma/ders/ödev/ödeme **+ gelişim grafikleri**) | `/parent/child-detail` | [teacher_reports](teacher_reports.md) §3 | 🟡 (gelişim bölümü 🟢 — P08 Task 5) |
| Bildirim tercihleri (switch + kanal) | `/parent/notifications` | — | 🟡 |
| Veli profili (+ çıkış) | `/parent/profile` | — | 🟡 |

---

> **Not:** Bu indeks yalnızca **kodda var olan** ekranları belgeler (öğretmen + **veli** tarafı). **Planlanan** öğrenci
> ekranları ve yeni özellik ekranları (mesajlaşma, ilan/keşif, üyelik/paywall) için rol ve modül dokümanlarına bakın →
> [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md), [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md).
> Yeni ekran kodda eklendikçe buraya bir satır + ekranın md'si eklenir (bkz. kökteki `CLAUDE.md`).
