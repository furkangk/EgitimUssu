#!/usr/bin/env python3
"""
md2doc — doc/ altındaki bir markdown dokümanını Word uyumlu .doc dosyasına çevirir.

Neden bu var: repo'da pandoc/libreoffice yok; macOS'un yerleşik `textutil` aracı
HTML → .doc/.docx dönüşümü yapabiliyor. Bu betik markdown'ı `textutil`in anladığı
sade, stil gömülü HTML'e çevirip dönüşümü tetikler.

Kullanım:
    python3 doc/_tools/md2doc.py doc/denetim/2026-09-05_rakip_paritesi_analizi.md
    python3 doc/_tools/md2doc.py <girdi.md> [--format doc|docx|rtf] [--out <cikti>]

Varsayılan çıktı: girdiyle aynı klasör, aynı ad, .doc uzantısı.

Kapsam (bu repodaki dokümanların kullandığı markdown alt kümesi):
    frontmatter · # başlıklar · > alıntı · | tablo | · - liste · 1. liste
    --- yatay çizgi · ``` kod bloğu · **kalın** *eğik* `kod` ~~üstü çizili~~ [bağlantı](url)
"""

from __future__ import annotations

import argparse
import html
import re
import shutil
import subprocess
import sys
from pathlib import Path

# --- Word'de düzgün görünen, gömülü stil ------------------------------------
CSS_WORD = """
body { font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; font-size: 10.5pt;
       line-height: 1.5; color: #16202B; }
h1 { font-size: 21pt; color: #082B4F; border-bottom: 2px solid #082B4F;
     padding-bottom: 6pt; margin: 0 0 14pt; }
h2 { font-size: 15pt; color: #082B4F; margin: 20pt 0 8pt;
     border-bottom: 1px solid #C7D2DE; padding-bottom: 4pt; }
h3 { font-size: 12pt; color: #0F3A63; margin: 14pt 0 5pt; }
h4 { font-size: 11pt; color: #3C4E60; margin: 12pt 0 4pt; }
p  { margin: 0 0 8pt; }
ul, ol { margin: 0 0 8pt; padding-left: 20pt; }
li { margin-bottom: 3pt; }
blockquote { margin: 8pt 0; padding: 7pt 11pt; background: #F1F5F9;
             border-left: 3px solid #0F7D8C; color: #33475B; }
blockquote p:last-child { margin-bottom: 0; }
table { border-collapse: collapse; width: 100%; margin: 8pt 0 12pt; font-size: 9pt; }
th { background: #082B4F; color: #FFFFFF; text-align: left; padding: 5pt 7pt;
     border: 1px solid #082B4F; font-weight: bold; }
td { border: 1px solid #C7D2DE; padding: 5pt 7pt; vertical-align: top; }
tr:nth-child(even) td { background: #F6F8FA; }
code { font-family: 'Menlo', 'Courier New', monospace; font-size: 9pt;
       background: #EEF1F5; padding: 1pt 3pt; }
pre { font-family: 'Menlo', 'Courier New', monospace; font-size: 9pt;
      background: #F1F5F9; border: 1px solid #C7D2DE; padding: 8pt;
      white-space: pre-wrap; margin: 8pt 0; }
hr { border: 0; border-top: 1px solid #C7D2DE; margin: 14pt 0; }
a { color: #0F7D8C; }
"""

# --- Tarayıcıda okunacak bağımsız HTML için stil ----------------------------
# Word şablonundan ayrıdır: duyarlı, açık/koyu tema, kaydırılabilir tablo, baskı kuralları.
CSS_WEB = """
:root {
  --ground:#EEF1F5; --surface:#FFFFFF; --surface-2:#F6F8FA;
  --line:#D3DBE4; --line-soft:#E4EAF0;
  --ink:#0E1A26; --ink-2:#3C4E60; --ink-3:#6C7E90;
  --navy:#082B4F; --teal:#0F7D8C; --quote:#EDF3F4;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --ground:#0B131C; --surface:#121C27; --surface-2:#17222E;
    --line:#2A3949; --line-soft:#1F2C3A;
    --ink:#E6EDF4; --ink-2:#AFC0D0; --ink-3:#7C8FA2;
    --navy:#7FB2E4; --teal:#4FBECB; --quote:#152631;
  }
}
:root[data-theme="dark"] {
  --ground:#0B131C; --surface:#121C27; --surface-2:#17222E;
  --line:#2A3949; --line-soft:#1F2C3A;
  --ink:#E6EDF4; --ink-2:#AFC0D0; --ink-3:#7C8FA2;
  --navy:#7FB2E4; --teal:#4FBECB; --quote:#152631;
}
* { box-sizing: border-box; }
body {
  background: var(--ground); color: var(--ink);
  font-family: "IBM Plex Sans", -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  font-size: 16.5px; line-height: 1.62;
  margin: 0; padding: 40px 20px 96px;
  -webkit-font-smoothing: antialiased;
}
main { max-width: 1000px; margin: 0 auto; }
h1, h2, h3, h4 {
  font-family: Newsreader, Georgia, "Times New Roman", serif;
  font-weight: 500; letter-spacing: -.015em; line-height: 1.15; text-wrap: balance;
}
h1 { font-size: clamp(2rem, 5vw, 2.9rem); margin: 0 0 24px;
     border-bottom: 2px solid var(--ink); padding-bottom: 18px; }
h2 { font-size: clamp(1.45rem, 3vw, 1.95rem); margin: 48px 0 14px;
     border-top: 1px solid var(--line); padding-top: 16px; }
h3 { font-size: 1.2rem; margin: 28px 0 8px; color: var(--ink); }
h4 { font-size: 1.02rem; margin: 20px 0 6px; color: var(--ink-2);
     font-family: "IBM Plex Sans", sans-serif; font-weight: 600; }
p { margin: 0 0 15px; max-width: 78ch; }
ul, ol { margin: 0 0 15px; padding-left: 24px; max-width: 78ch; }
li { margin-bottom: 6px; }
li > ul, li > ol { margin-top: 6px; }
strong { font-weight: 600; }
a { color: var(--teal); text-underline-offset: 2px; }
a:focus-visible { outline: 2px solid var(--teal); outline-offset: 3px; }
blockquote {
  margin: 18px 0; padding: 14px 18px;
  background: var(--quote); border-left: 3px solid var(--teal);
  color: var(--ink-2); max-width: 82ch;
}
blockquote p { margin-bottom: 10px; }
blockquote p:last-child { margin-bottom: 0; }
.tw { overflow-x: auto; border: 1px solid var(--line); background: var(--surface); margin: 0 0 20px; }
table { border-collapse: collapse; width: 100%; font-size: 14px; }
th, td { text-align: left; padding: 10px 13px; border-bottom: 1px solid var(--line-soft); vertical-align: top; }
th { font-size: 11.5px; font-weight: 600; letter-spacing: .08em; text-transform: uppercase;
     color: var(--ink-3); background: var(--surface-2);
     border-bottom: 1px solid var(--line); white-space: nowrap; }
tr:last-child td { border-bottom: 0; }
code {
  font-family: "IBM Plex Mono", ui-monospace, Menlo, "Courier New", monospace;
  font-size: .87em; background: var(--surface-2); color: var(--ink-2);
  padding: 1px 5px; border: 1px solid var(--line-soft);
}
pre {
  font-family: "IBM Plex Mono", ui-monospace, Menlo, "Courier New", monospace;
  font-size: 13px; line-height: 1.55; background: var(--surface);
  border: 1px solid var(--line); padding: 14px 16px;
  overflow-x: auto; margin: 0 0 20px;
}
pre code { background: none; border: 0; padding: 0; font-size: inherit; }
hr { border: 0; border-top: 1px solid var(--line); margin: 32px 0; }
@media (max-width: 620px) { body { padding: 28px 16px 72px; font-size: 16px; } }
@media print {
  body { background: #FFF; color: #000; padding: 0; font-size: 10.5pt; }
  .tw { overflow: visible; border: 0; }
  h2 { break-after: avoid; } table, pre, blockquote { break-inside: avoid; }
}
"""

_CODE_TOKEN = "\x00CODE%d\x00"


def _inline(text: str) -> str:
    """Satır içi markdown → HTML. Kod parçaları önce korunur ki içleri işlenmesin."""
    spans: list[str] = []

    def stash(m: re.Match[str]) -> str:
        spans.append(html.escape(m.group(1)))
        return _CODE_TOKEN % (len(spans) - 1)

    text = re.sub(r"`([^`]+)`", stash, text)
    text = html.escape(text)
    text = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r'<a href="\2">\1</a>', text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"(?<![\w*])\*([^*\n]+)\*(?![\w*])", r"<em>\1</em>", text)
    text = re.sub(r"~~([^~]+)~~", r"<s>\1</s>", text)
    for i, span in enumerate(spans):
        text = text.replace(_CODE_TOKEN % i, f"<code>{span}</code>")
    return text


def _strip_frontmatter(lines: list[str]) -> tuple[list[str], dict[str, str]]:
    meta: dict[str, str] = {}
    if not lines or lines[0].strip() != "---":
        return lines, meta
    for i in range(1, len(lines)):
        if lines[i].strip() == "---":
            for raw in lines[1:i]:
                if ":" in raw:
                    k, _, v = raw.partition(":")
                    meta[k.strip()] = v.strip().strip('"')
            return lines[i + 1:], meta
    return lines, meta


def _is_table_divider(line: str) -> bool:
    return bool(re.match(r"^\s*\|[\s:|-]+\|\s*$", line)) and "-" in line


def _cells(line: str) -> list[str]:
    return [c.strip() for c in line.strip().strip("|").split("|")]


def convert(md: str, *, web: bool = False) -> str:
    """Markdown gövdesini tek parça HTML'e çevirir.

    ``web=True`` tarayıcı için bağımsız belge üretir (duyarlı, açık/koyu tema);
    aksi hâlde ``textutil``in .doc/.docx'e çevireceği sade Word şablonu kullanılır.
    """
    lines, meta = _strip_frontmatter(md.splitlines())
    out: list[str] = []
    i, n = 0, len(lines)
    list_stack: list[str] = []

    def close_lists() -> None:
        while list_stack:
            out.append(f"</{list_stack.pop()}>")

    while i < n:
        line = lines[i]
        stripped = line.strip()

        # kod bloğu
        if stripped.startswith("```"):
            close_lists()
            i += 1
            buf: list[str] = []
            while i < n and not lines[i].strip().startswith("```"):
                buf.append(lines[i])
                i += 1
            i += 1
            out.append("<pre>" + html.escape("\n".join(buf)) + "</pre>")
            continue

        # boş satır
        if not stripped:
            close_lists()
            i += 1
            continue

        # yatay çizgi
        if re.match(r"^\s*(-{3,}|\*{3,})\s*$", line):
            close_lists()
            out.append("<hr>")
            i += 1
            continue

        # başlık
        m = re.match(r"^(#{1,6})\s+(.*)$", stripped)
        if m:
            close_lists()
            lvl = min(len(m.group(1)), 4)
            out.append(f"<h{lvl}>{_inline(m.group(2))}</h{lvl}>")
            i += 1
            continue

        # tablo
        if stripped.startswith("|") and i + 1 < n and _is_table_divider(lines[i + 1]):
            close_lists()
            head = _cells(stripped)
            i += 2
            rows: list[list[str]] = []
            while i < n and lines[i].strip().startswith("|"):
                rows.append(_cells(lines[i].strip()))
                i += 1
            out.append("<table><tr>" + "".join(f"<th>{_inline(c)}</th>" for c in head) + "</tr>")
            for row in rows:
                row = (row + [""] * len(head))[: len(head)]
                out.append("<tr>" + "".join(f"<td>{_inline(c)}</td>" for c in row) + "</tr>")
            out.append("</table>")
            continue

        # alıntı bloğu (ardışık > satırları tek blok)
        if stripped.startswith(">"):
            close_lists()
            buf = []
            while i < n and lines[i].strip().startswith(">"):
                buf.append(re.sub(r"^\s*>\s?", "", lines[i]))
                i += 1
            body = "\n".join(buf)
            paras = [p.strip() for p in re.split(r"\n\s*\n", body) if p.strip()]
            inner = "".join(f"<p>{_inline(p.replace(chr(10), ' '))}</p>" for p in paras)
            out.append(f"<blockquote>{inner}</blockquote>")
            continue

        # listeler
        m = re.match(r"^(\s*)([-*+]|\d+\.)\s+(.*)$", line)
        if m:
            indent, marker, body = len(m.group(1)), m.group(2), m.group(3)
            tag = "ul" if marker in "-*+" else "ol"
            depth = indent // 2 + 1
            while len(list_stack) > depth:
                out.append(f"</{list_stack.pop()}>")
            if len(list_stack) < depth:
                out.append(f"<{tag}>")
                list_stack.append(tag)
            # satır devamı (girintili sonraki satırlar)
            i += 1
            while i < n and lines[i].strip() and not re.match(r"^(\s*)([-*+]|\d+\.)\s+", lines[i]) \
                    and not lines[i].strip().startswith(("#", ">", "|", "```")) \
                    and lines[i].startswith(" "):
                body += " " + lines[i].strip()
                i += 1
            out.append(f"<li>{_inline(body)}</li>")
            continue

        # normal paragraf (yumuşak satır sonlarını birleştir)
        close_lists()
        buf = [stripped]
        i += 1
        while i < n and lines[i].strip() and not re.match(
                r"^\s*(#{1,6}\s|>|\||```|[-*+]\s|\d+\.\s|-{3,}\s*$)", lines[i]):
            buf.append(lines[i].strip())
            i += 1
        out.append(f"<p>{_inline(' '.join(buf))}</p>")

    close_lists()
    title = meta.get("title") or "EğitimÜssü"
    body = "\n".join(out)

    if not web:
        return (
            '<html><head><meta charset="utf-8">'
            f"<title>{html.escape(title)}</title><style>{CSS_WORD}</style></head>"
            "<body>" + body + "</body></html>"
        )

    # Geniş içerik (tablo) sayfayı yana kaydırmasın diye kendi kabına alınır.
    body = body.replace("<table>", '<div class="tw"><table>').replace("</table>", "</table></div>")
    desc = meta.get("summary", "")
    return (
        "<!doctype html>\n"
        '<html lang="tr"><head><meta charset="utf-8">'
        '<meta name="viewport" content="width=device-width, initial-scale=1">'
        f"<title>{html.escape(title)}</title>"
        + (f'<meta name="description" content="{html.escape(desc)}">' if desc else "")
        + '<link rel="preconnect" href="https://fonts.googleapis.com">'
        '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>'
        '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
        "family=Newsreader:opsz,wght@6..72,400;6..72,500&"
        "family=IBM+Plex+Sans:wght@400;500;600;700&"
        'family=IBM+Plex+Mono:wght@400;500&display=swap">'
        f"<style>{CSS_WEB}</style></head>"
        "<body><main>" + body + "</main></body></html>"
    )


def main() -> int:
    ap = argparse.ArgumentParser(description="Markdown → Word uyumlu .doc (textutil ile)")
    ap.add_argument("source", help="girdi .md dosyası")
    ap.add_argument("--format", default="doc", choices=["doc", "docx", "rtf", "html"],
                    help="çıktı biçimi (varsayılan: doc)")
    ap.add_argument("--out", help="çıktı yolu (varsayılan: girdiyle aynı ad + biçim uzantısı)")
    args = ap.parse_args()

    src = Path(args.source)
    if not src.is_file():
        print(f"HATA: dosya yok: {src}", file=sys.stderr)
        return 1

    out = Path(args.out) if args.out else src.with_suffix("." + args.format)
    page = convert(src.read_text(encoding="utf-8"), web=args.format == "html")

    if args.format == "html":
        out.write_text(page, encoding="utf-8")
        print(f"✓ {out}")
        return 0

    if not shutil.which("textutil"):
        print("HATA: `textutil` bulunamadı (yalnız macOS). --format html kullanın.", file=sys.stderr)
        return 2

    tmp = out.with_suffix(".tmp.html")
    tmp.write_text(page, encoding="utf-8")
    try:
        subprocess.run(
            ["textutil", "-convert", args.format, "-format", "html",
             str(tmp), "-output", str(out)],
            check=True, capture_output=True,
        )
    except subprocess.CalledProcessError as exc:
        print(f"HATA: textutil başarısız: {exc.stderr.decode(errors='replace')}", file=sys.stderr)
        return 3
    finally:
        tmp.unlink(missing_ok=True)

    print(f"✓ {out}  ({out.stat().st_size // 1024} KB)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
