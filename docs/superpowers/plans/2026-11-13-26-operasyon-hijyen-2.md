# P26 — Operasyon ve Mühendislik Hijyeni — Hafif Plan 🅻

> **Hafif plan:** `/devam` önce **Task 0**'ı yürütür. T1–T8 istenen an (paralel) yürüyebilir; **T9 en son** (K-29) ve yalnız açık özellik planı yokken.

**Goal:** Üretimin ayakta, yedekli, izlenebilir olması; kod tabanının bir ekip tarafından güvenle geliştirilebilmesi.

**Spec:** master K-27, K-29. **Kaynak:** rapor P-02, P-03 (backend), P-07, P-08, P-09, P-13, P-14, §2.4-5.
**Dal:** görev başına kısa dallar (`chore/p26-tN-...`). **Dış bağımlılık:** Q4, Q9, Q12, Q13.

### Task 0: Planı ayrıntılandır
- [ ] **Step 1:** Kodu oku, görevleri 2026-09-02 plan biçiminde (`Files/Interfaces/Steps`) yaz, `docs(plan): P26 ayrintilandirildi` commit'i at, ILERLEME'yi güncelle. **Kod yazma.**

### Task 1: Barındırma ve yedek (P-02)
- Render ücretli plan (uyumayan servis) + **Frankfurt** bölgesi + managed Postgres (PITR) — Q12; `render.yaml` güncel; günlük `backup-db.sh` → R2 (Q4) cron işi + 30 gün saklama; aylık geri yükleme tatbikatı takvimi `backup_restore.md`'ye.
- **Kabul:** servis 15 dk boşta uyumuyor; dünün yedeği R2'de; tatbikat kaydı.

### Task 2: Hata takibi, gözlemlenebilirlik, alarm (P-03)
- Sentry .NET (Q13); OTLP toplayıcı (Q9) ile `Observability__Enabled=true`; uptime ping (`/health/ready`); alarmlar: 5xx oranı, outbox bekleyen > 100, dead-letter > 0, yedek başarısız.
- **Kabul:** bilerek tetiklenen hata Sentry'de; alarm test bildirimi ulaştı.

### Task 3: CI güvenliği (P-07)
- Dependabot (NuGet, pub, GitHub Actions, Docker); CodeQL (C#); secret scanning + push protection; Docker imajı build + Trivy taraması; `flutter pub outdated`/audit adımı.

### Task 4: Dockerfile (P-09)
- csproj'ları önce kopyala → `restore` → kaynak; `USER $APP_UID`; `HEALTHCHECK`; `LangVersion` sabit (preview değil); `tzdata` (P16 T1 ile tutarlı).

### Task 5: Deploy hattı + PR akışı (P-07)
- `main` korumalı (PR + yeşil CI zorunlu); PR şablonu; `CODEOWNERS`; staging'e otomatik, üretime onaylı deploy; migration ayrı ve gözetimli adım (E-05 kuralı).

### Task 6: Repo hijyeni ve sürümleme (P-14)
- Kök `README.md` (kurulum, mimari özet, komutlar), `LICENSE` kararı, `SECURITY.md`, `CHANGELOG.md`; semver etiketleri (`v0.x`); kök `dotnet-tools.json` silinir (`.config/` kalır); platforma özgü izlenen dosyalar `.gitignore`'a + şablon dosyalar (`*.example`); `doc/.obsidian` izlenmez; `.doc`/`.html` kopyaları üretilir, commit edilmez; `codex/` ve `doc/promp.txt` → `doc/_arsiv/`; Flutter sürümü sabitlenir (`.fvmrc` veya `.tool-versions`) — `pubspec.lock` oynaması biter.

### Task 7: Test kapsamı ölçümü (P-08)
- Backend kapsam (coverlet) CI'da raporlanır; mobil kapsam eşiği uyarıdan **hataya** (başlangıç: mevcut değer, her plan +); senaryo testleri (master §1) için `tests/Integration/Scenarios/` klasörü.

### Task 8: Ürün analitiği + huni (rapor 2.4-5)
- Rızaya bağlı, AB'de barındırılan analitik (ör. PostHog EU) veya Firebase Analytics (P27 envanteri); olaylar: kayıt, rol, ilk öğrenci/ilk ders/ilk çalışma, deneme başlat, premium; admin huni görünümü (P14 ile).

### Task 9: Migration konsolidasyonu (P-13, K-29) — **EN SON**
- `job_leases`, `outgoing_emails`, `module_states` → tek `infra` şeması/`InfraDbContext`; outbox/inbox modül şemasında kalır; her modülde squash + yeni baseline; mevcut üretim DB'si için "baseline'ı uygulanmış say" prosedürü + tatbikat.
- **Kabul:** `src/**/Migrations` satır sayısı önce/sonra ILERLEME'de; temiz DB'de ve mevcut DB'de açılış yeşil.
