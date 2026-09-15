# Working in this repository

ImplicitUI is a Unity UI package by Bionics: small, practical UI improvements for Unity. Scope,
architecture, rules and implementation order live in **`Docs/ui-package-guia.md`** (Portuguese, the
author's living plan) — read it before proposing work, and stop to ask wherever it marks something as
pending or to be verified. This file is the briefing for an AI assistant working
here; the README is the one for people using the package.

## What this repository is

The whole Unity development project (**6000.6.0f1**, URP), not only the package. The distributable is
the embedded package under `Packages/com.bionics.implicitui/`. Repository:
https://github.com/bionics07/Implicit-UI (public).

```
Packages/com.bionics.implicitui/   the package - this is what ships
  Runtime/                         ImplicitUI.Runtime - no UnityEditor references, ever
  Editor/                          ImplicitUI.Editor - includePlatforms: ["Editor"]
  Tests/Runtime/                   ImplicitUI.Tests.Runtime (PlayMode)
  Tests/Editor/                    ImplicitUI.Tests.Editor (EditMode)
Assets/                            sandbox and manual test scenes only; never referenced by tests
```

The package is listed in `testables` in `Packages/manifest.json`. `package.json` declares `"unity":
"2021.3"` as a placeholder — the minimum version is not decided.

## Driving the Unity Editor

The live Editor is controlled through **Unity Pipeline** (`com.unity.pipeline`, experimental) and the
Unity CLI (`unity`, on PATH; `%LOCALAPPDATA%\Unity\bin\unity.exe`). Two entry points to the same Editor:

- **MCP — the default.** `.mcp.json` registers `unity mcp --project-path .`. Its tools are the Editor's
  commands (`mcp__unity__<command>`).
- **CLI — fallback only**, when the MCP connection is down or not loaded in the conversation:
  `unity command <name> --<arg> <value>`; `unity command` alone lists everything with parameters. From
  PowerShell, C# with quotes in `eval` loses its quotes — run those through bash or use `run_script`.

Check first, always: `unity status` (GUI Editor shows state `ready`) or the `editor_status` tool.

### Rules

- **Never hand-edit `.unity`, `.prefab` or `.asset` YAML while the Editor is reachable.** Use the
  commands; the Editor keeps the real state in memory.
- **Code edit loop:** edit `.cs` on disk → `recompile` → poll `recompile_status` until `completed` /
  `up_to_date` (connection errors during the domain reload are expected) → `console` for errors and
  warnings → `run_tests --mode editor|playmode --filter <Fixture>`.
- **Bulk scene/UI construction goes in a file, not in `eval`.** Write a builder class outside `Assets/`
  (e.g. `AgentScripts/Build.cs`) and run it with `run_script --file ... --entry Class.Method`. `eval` is
  for one-liners only.
- **UI objects created from C#:** `new GameObject(name, typeof(RectTransform))`, then
  `SetParent(parent, false)` **before** setting anchors, pivot or size.
- **Build in Edit mode, then `save_scene`.** Anything created during Play mode is discarded on stop.
- **Destructive commands take `confirm` / `dry_run`.** Run `dry_run` first. Project-settings, asset and
  package writes are not undoable with Ctrl+Z.
- **Overlay UI is invisible to camera captures.** `capture_game_view` with `source=camera` misses
  Screen Space - Overlay canvases; use `source=screen` in Play mode.

### Traps

- **Authoring commands are confined to `Assets/`** (`get_authoring_root`). `create_script`,
  `write_text_file`, `create_asset` etc. cannot write into `Packages/`; write package files with the
  normal file tools, then `recompile`. Unity generates their `.meta` files on import.
- **Compile errors put the Editor in Safe Mode and the Pipeline server does not load.** Every command
  fails to connect. Confirm with `unity pipeline list`, read `error CS####` from `Logs/Editor.log`, fix
  the source, and ask the author to restart Unity.
- **A command that hangs may be a modal dialog.** `editor_status` still answers and reports
  `blocked_by_dialog`; it cannot be dismissed remotely.
- **Headless batchmode cannot open this project while the Editor has it open** ("Multiple Unity
  instances cannot open the same project"). Headless runs go on a copy.
- **PlayMode tests cannot run synchronously over the Pipeline.** Entering Play mode drops the request;
  use `run_tests --mode playmode --async_tests true` and poll `test_status`.
- **PlayMode runs need a fresh domain first.** Enter Play Mode Options is on with domain reload disabled
  (URP template default, kept on purpose). Only the first PlayMode run after a domain reload finds the
  tests; later runs report `completed` with **0 tests** — a false green. Before every PlayMode run:
  `eval` `UnityEditor.EditorUtility.RequestScriptReload();` (it may time out while the domain reloads —
  expected) → poll `editor_status` until `ready` → run. Always check that `total` is above 0.
- **Unity's analyzers run on package code.** `AppDomain.GetAssemblies()` raises UAC0005, for example.
  Treat new warnings as failures.

## Conventions

- Code, comments and documentation in English. Public API carries XML docs.
- `.meta` files are committed and never regenerated casually.
- Everything that ships has tests; a bug fix starts with the test that reproduces it.
- Semantic versioning. The version lives in the package's `package.json`.

## Working with the author

The author reviews every file before committing, and creates the branches. Do not commit, push or tag
without being asked to.
