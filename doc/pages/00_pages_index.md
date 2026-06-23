# 📱 Sayfa (Ekran) Dokümanları — İndeks

> Her dosya, kodda **var olan** bir Flutter ekranını belgeler (route, dosya, state, veri kaynağı, durum).
> Kaynak: `mobile/lib/core/routing/app_router.dart` + feature klasörleri.
> **Yeni ekran eklendiğinde buraya bir satır + ekranın md'si eklenir** (bkz. kökteki `CLAUDE.md`).
> **Son güncelleme:** 2026-06-23

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
| Öğrenci listesi | `/students` | [students_list](students_list.md) | 🔴 |
| Öğrenci detayı | `/students/:studentId` | [students_detail](students_detail.md) | 🔴 |

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
| Ödeme takibi | `/payments` | [payments_list](payments_list.md) | 🔴 |
| Ödeme ekle | `/payments/new` | [payment_form](payment_form.md) | 🟢 |

## Assignments
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Ödev/takip | `/assignments/new`, `/assignments/:lessonSessionId` | [assignment_follow_up](assignment_follow_up.md) | 🟢 |

## Teacher Profile
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Öğretmen profili | `/teacher-profile` | [teacher_profile](teacher_profile.md) | 🟢 |

## More / Settings
| Ekran | Route | md | Veri |
|-------|-------|-----|------|
| Diğer/Ayarlar | `/more` | [more](more.md) | 🟡 |
| Hesap bilgileri | `/account-info` | [account_info](account_info.md) | 🔴 |

---

> **Not:** Bu indeks yalnızca **kodda var olan** ekranları belgeler (hepsi öğretmen tarafı). **Planlanan** öğrenci/veli
> ekranları ve yeni özellik ekranları (mesajlaşma, ilan/keşif, üyelik/paywall) için rol ve modül dokümanlarına bakın →
> [`../roles/ogrenci.md`](../roles/ogrenci.md), [`../roles/veli.md`](../roles/veli.md), [`../modules/00_genel_bakis.md`](../modules/00_genel_bakis.md).
> Yeni ekran kodda eklendikçe buraya bir satır + ekranın md'si eklenir (bkz. kökteki `CLAUDE.md`).
