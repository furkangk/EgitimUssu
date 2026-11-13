#!/usr/bin/env python3
"""A-07: Kira tabanina gecen arka plan servislerine eksik using satirlarini ekler."""
import pathlib
import sys

NEEDED = [
    "using EgitimUssu.Shared.Infrastructure.Messaging;",
    "using EgitimUssu.Shared.Infrastructure.Persistence;",
]

for path in sys.argv[1:]:
    p = pathlib.Path(path)
    text = p.read_text()
    lines = text.splitlines(keepends=True)

    missing = [u for u in NEEDED if u not in text]
    if not missing:
        print("atlandi:", path)
        continue

    # Son `using` satirindan sonra ekle.
    last = max(i for i, line in enumerate(lines) if line.startswith("using "))
    lines[last + 1 : last + 1] = [u + "\n" for u in missing]
    p.write_text("".join(lines))
    print("eklendi:", path, "->", ", ".join(missing))
