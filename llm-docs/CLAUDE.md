# LLM Documentation Index

This directory contains synthesized documentation produced during an LLM-driven refactoring session. It supplements the existing `docs/` directory with information useful for code analysis and refactoring.

## Files

- **architecture-overview.md** — High-level mod architecture, entry point flow, scene handling, and how systems interconnect
- **source-inventory.md** — Complete inventory of all source files with line counts and brief descriptions
- **framework-reference.md** — MelonLoader/Harmony/Unity framework details, dependencies, and Harmony patch inventory
- **type-index.md** — Maps game type short names to full namespace + DLL location (eliminates namespace guessing)

## Decompilation Workflow

Pre-decompiled game types are in `decompiled/` (gitignored). Read these directly instead of running ilspycmd.

**Tools** (in `tools/`):
- `decompile.ps1 "TypeName" [-Dll Core|Asm|Gre|Shared|Auto]` — Decompile a single type
- `decompile-all.ps1` — Re-decompile all types listed in type-index.md (run after game updates)
