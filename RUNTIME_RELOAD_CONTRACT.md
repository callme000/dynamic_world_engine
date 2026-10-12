# Dynamic World Engine — Runtime Reload Contract

## Supported development layout

For the simplest development workflow, this repository's runtime files are intended to live in the ScriptHookVDotNet scripts directory:

`C:\Program Files\Epic Games\GTAVEnhanced\scripts`

With that layout, a normal workflow is:

1. Pull the approved DWE commit into the scripts directory.
2. Start or return to GTA V Enhanced.
3. Press **Insert** to request a ScriptHookVDotNet script reload.
4. Validate the DWE startup state in-game.

The repository does **not** silently modify the GTA installation when a commit is pushed to GitHub. The pull must update the runtime files in the actual scripts directory.

## Non-negotiable runtime rule

A pull + Insert workflow is only considered working when the new `DynamicWorldEngine.dll` is loadable by the installed ScriptHookVDotNet runtime. Merely having the DLL on disk is not enough.

The current repository state contains a runtime dependency mismatch that must be resolved before this can be declared green: the existing DWE binary has reported a dependency on the legacy `ScriptHookVDotNet` assembly identity while the installation provides `ScriptHookVDotNet3.dll`.

Therefore this branch deliberately does **not** claim that Insert reload is currently validated. The next implementation step is to rebuild DWE against the exact ScriptHookVDotNet3 reference used by the target GTA V Enhanced installation, then validate the resulting DLL in-game.

## Pre-flight check

Run:

```powershell
.\tools\Verify-DWE-ReloadReady.ps1
```

The check fails on missing runtime files and warns about the known assembly-identity problem.

## Definition of done

The runtime foundation is complete only when all of these are true:

- `DynamicWorldEngine.dll` is built from repository source.
- Its ScriptHookVDotNet reference matches the installed runtime.
- Pulling the approved commit updates the DLL in the GTA `scripts` directory.
- Pressing **Insert** reloads the updated DWE without restarting the game.
- DWE reaches its normal startup state after reload.
- A small deterministic in-game operation succeeds after reload.
- Failed loads are visible and do not leave a partially registered world state.
