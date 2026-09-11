---
name: unity-verify
description: Prove a Unity change actually works in Clone Swarm without the owner pressing Play — compile-check C# while the Editor holds the lock, drive the Editor headlessly, and pick the verification layer that catches the bug class at hand. Use after editing any .cs file, before claiming a change compiles or works, when a batchmode run "succeeds" but produces nothing, or when deciding what evidence a change still needs.
---

# Verifying a Unity change (Clone Swarm)

**All builds, plays, and tests run from the Editor** — and the owner keeps it open, which blocks
batchmode. This doc is how to produce real evidence anyway, and how to be honest about what each
kind of evidence does and does not cover.

## Pick the layer that catches your bug class

Four layers, four disjoint sets of bugs. Cheapest first, but cheap does not mean sufficient.

| Layer | Tool | Catches | Blind to |
|---|---|---|---|
| Compile | Roslyn via Bee `.rsp` (below) | syntax, types, renamed APIs | everything else |
| Structural | read scene/prefab YAML directly | broken refs, `m_Script: {fileID: 0}`, duplicate panels, missing components | anything only true at runtime |
| Visual | `P3RScreenshotTool.CaptureAll` → read the PNG | spacing, colour, overflow, wrong hierarchy | anything you cannot see |
| Behavioural | `P3RSmokeTest.Run` (play mode) | dead buttons, null singletons, wrong runtime layout | anything needing 2+ machines |

This project has shipped both "structure correct but the screen is wrong" and "screen correct but
nothing is clickable". Neither layer implies the other. **Do not report a change as working on
compile-check alone** — say which layers ran and which did not.

**Nothing here covers multiplayer.** Host/client divergence, spawn ordering, and NetworkVariable
replication need two real instances (ParrelSync / Multiplayer Play Mode) and only the owner can do
that. When a change touches netcode, say so explicitly instead of letting a green smoke test imply
coverage.

## Compile-checking while the Editor is open

`Temp/UnityLockfile` makes batchmode fail most of the time. Compile with Unity's own build-graph
response file instead — same compiler, same defines, same references as the Editor uses.

```bash
# หา rsp ที่มี UNITY_EDITOR — ห้ามใช้ ls -t เฉยๆ
RSP=$(grep -l 'UNITY_EDITOR' Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp | head -1)
SCRATCH="<your scratch dir>"

sed -e "s#^-out:.*#-out:\"$SCRATCH/AC.dll\"#" \
    -e "s#^-refout:.*#-refout:\"$SCRATCH/AC.ref.dll\"#" \
    "$RSP" > "$SCRATCH/AC.rsp"

dotnet "E:/Zenity Why not/Unity/6000.7.0a2/Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" \
    "@$SCRATCH/AC.rsp"
```

Four ways this goes wrong silently:

- **There are two `.dag` folders and only one is the Editor's.** `…E.dag` carries
  `-define:UNITY_EDITOR`, `…P.dag` (Player) carries none. `ls -t | head -1` picks whichever Unity
  touched last, so it lands on the Player one about half the time — then every `#if UNITY_EDITOR`
  block is skipped and the check passes while the real build breaks. Select by content, as above.
  The hash prefix changes when Unity regenerates the graph; never hardcode it.
- **The source list is a snapshot.** It is only valid while no `.cs` file was added or deleted.
  Adding a file means appending its path to the rsp yourself — and **the rsp has no trailing
  newline**, so append `\n` first or the new path fuses onto the last entry.
- **`Assembly-CSharp-Editor.rsp` references the Editor's stale `Assembly-CSharp.ref.dll`.**
  Repoint its `-r:` at the one you just built, or it reports phantom errors about members you
  just added.
- **Redirect `-out` and `-refout`.** Writing into `Library/Bee/artifacts/` corrupts the Editor's
  own build state.

Editor and runtime assemblies are separate compilations. A change spanning both needs both runs.

## Driving the Editor headlessly

The Editor must be **closed** first. Ask the owner to close it; do not kill the process.

```bash
U="E:/Zenity Why not/Unity/6000.7.0a2/Editor/Unity.exe"
P="E:/Zenity Why not/cloneSwarm"

"$U" -quit -batchmode -nographics -projectPath "$P" \
     -executeMethod <Class>.<Method> -logFile "<ABSOLUTE>/run.log"
```

Four flags, all of which fail **quietly**:

- **`-logFile` must be an absolute path.** A bare filename resolves against the Git Bash install
  directory; Unity exits 127 with `Unable to open log file` — and the shell pipeline still reports
  success. Always read the log, never trust the exit code alone.
- **`-nographics` kills anything that renders.** Fine for builders and asset scripts, fatal for
  `P3RScreenshotTool` — it needs a graphics device, so use `-batchmode` alone.
- **`P3RScreenWirer.WireMenuScene` needs `-confirm`** or it dry-runs. It once retargeted 170
  references and saved over the scene because batchmode skipped the confirmation dialog.
- **`P3RSmokeTest.Run` must NOT get `-quit`** — it exits by itself when the test finishes.
  `-quit` kills the Editor before play mode starts and the run reports nothing.

Read the log for `error CS`, `Exception`, and the script's own summary line. A batchmode run that
prints nothing did not pass — it did not run.

## Shell traps on this machine

- **`py`, never `python`.** And set `PYTHONIOENCODING=utf-8` or Thai stdout throws.
- **Every project path contains spaces.** Quote every path; a whitespace-split pipeline returns a
  wrong number rather than an error.
- **Heredoc `\n` inside a python-written C# string literal collapses to a real newline** → CS1010
  on a file you did not read back. Use the Edit tool for C# string literals instead of generating
  them through a shell heredoc.

## What "done" means

State the evidence, not a verdict:

> Roslyn clean on both assemblies · smoke test 62/62, 0 errors · not tested on two machines

Never write "works" for something only compiled, and never present a layer that did not run as
though it did. When a run fails, quote the failing output.
