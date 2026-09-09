# Skyrim Linux Case Compatibility

CaseCompat is a Linux tool for fixing path-casing problems in heavily
modded Skyrim Special Edition installations.

A mod can contain the file Skyrim needs while still using a different
capitalization from the path requested by a plugin, mesh, or other asset.
On case-sensitive Linux paths, differences such as `meshes` versus
`Meshes` can matter, even though the same install works fine on Windows.

CaseCompat finds the exact set of files your installed plugins actually
request, checks each one against what physically exists on disk, and
renames the file to the correctly-requested case wherever the two
disagree — leaving everything else in the install untouched.

## Quickstart: guided setup

Run the tool with no arguments (or `run`) and it walks you through the
rest:

```bash
./CaseCompat.Cli
```

It will:

1. Try to auto-detect your Skyrim Special Edition install and its
   `Plugins.txt` / `loadorder.txt` via your Steam/Proton library, and ask
   you to confirm each detected path (or type one in if detection fails
   or you decline).
2. Scan your load order for case-mismatch fixes. This can take a few
   minutes for a large mod list — it has to resolve every plugin's
   asset requests against the current winning load order.
3. Show how many fixable mismatches it found and ask once before
   applying anything.
4. Apply them, showing progress, and print a summary plus the location
   of a full per-fix report and the journal used for troubleshooting or
   rollback.

Running from source instead of a package:

```bash
dotnet run --project src/CaseCompat.Cli --
```

## Safety model

- Nothing is modified until you confirm the apply step.
- A fix is a rename, not a copy: the existing file's own data is
  renamed to the correct case, so there is only ever one file on disk
  for that asset — exactly like the original Windows install. No new
  data is written and no other file is deleted or overwritten.
- Every fix goes through a durable, fresh-verified plan before any
  filesystem mutation, and every applied fix is journaled with the exact
  file identity it renamed — rollback re-verifies that identity before
  renaming anything back, and refuses if the file has since changed.
- A mismatch that can't be safely resolved (an ambiguous or conflicting
  case) is skipped and reported, not guessed at.
- There is no batch-wide atomic transaction: a batch run applies fixes
  one at a time, and each one is independently durable. If a run is
  interrupted, earlier fixes in that run remain applied; re-running is
  safe since already-applied fixes are recognized and skipped.

## Requirements

CaseCompat can be run from source or from a framework-dependent Linux
package.

To run from source, you need:

- Linux.
- A .NET 10 SDK.
- A checkout of this repository.

To run a packaged build, you need:

- Linux.
- The .NET 10 runtime (`Microsoft.NETCore.App 10.x`).
- The extracted CaseCompat package.

The current package is framework-dependent and does not bundle the
.NET runtime.

## Build a Linux package

From the repository root, choose an output directory:

```bash
mkdir -p dist
scripts/package-linux-framework-dependent.sh dist
```

The packaging script builds from committed `HEAD`. Uncommitted
worktree or index changes are not included in the package.

It produces:

- `casecompat-<commit>-linux-x64-framework-dependent.tar.gz`
- A matching `.sha256` checksum file.

The archive contains the CaseCompat executable and managed
dependencies, `README.md`, and `LICENSE`. Development PDB files are
excluded.

After extracting the archive, run it directly for the guided setup:

```bash
./casecompat/CaseCompat.Cli
```

or show the full command reference with:

```bash
./casecompat/CaseCompat.Cli --help
```

There is currently no installer and CaseCompat does not automatically
modify `~/.local/bin` or another system path. The repository also does
not yet publish these archives through a GitHub Releases workflow.

## Advanced: scripted usage

The guided setup wraps a smaller set of commands that can also be run
directly, which is useful for scripting or for re-running against a
specific candidate. Each takes Skyrim's `Data` directory plus its
`Plugins.txt`, `loadorder.txt`, and `Skyrim.ccc`.

```bash
DATA="/path/to/Skyrim Special Edition/Data"
PLUGINS="/path/to/Plugins.txt"
LOADORDER="/path/to/loadorder.txt"
CCC="/path/to/Skyrim Special Edition/Skyrim.ccc"
```

**Discover candidates** (read-only; no plan or mutation):

```bash
dotnet run --project src/CaseCompat.Cli -- \
  targeted-consumer-candidates "$DATA" "$PLUGINS" "$LOADORDER" "$CCC"
```

**Plan and apply one exact requested path**, given a plan directory and
a journal directory:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  targeted-consumer-plan "$DATA" "$PLUGINS" "$LOADORDER" "$CCC" \
  "meshes/example/file.nif" "$PLANS"

dotnet run --project src/CaseCompat.Cli -- \
  targeted-consumer-apply "$DATA" "$PLANS" targeted-consumer-plan.json "$JOURNAL"
```

**Apply every discovered candidate in one run**, writing a CSV report:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  targeted-consumer-batch-apply "$DATA" "$PLUGINS" "$LOADORDER" "$CCC" \
  "$PLANS" "$JOURNAL" "$REPORT_CSV" 100000
```

**Roll back one applied fix**, given its plan ID (the journal directory
contains `<plan-id>.apply-*.json` files that name it):

```bash
dotnet run --project src/CaseCompat.Cli -- \
  targeted-consumer-rollback "$JOURNAL" "<plan-id>"
```

Rollback re-verifies the destination file's exact identity against what
that apply published, and refuses to rename it back if it no longer
matches or if something now occupies the original name.

## Read-only diagnostics

The CLI also exposes diagnostic and inventory commands outside the main
repair workflow.

Examples include:

```text
doctor
collisions
collision-tree
compare-branches
namespace-summary
content-summary
resolve-data-path
plugin-probe
record-inventory
armor-addon-models
resolve-armor-addon-models
armor-records
load-order-probe
armor-addon-winner
effective-armor-addon-models
winning-armor-addon-inventory
effective-armor-addon-scan
archive-candidate-index
runtime-plugin-set
runtime-archive-evidence
effective-armor-addon-archive-candidates
armor-addon-snapshot-diagnostics
```

Run:

```bash
dotnet run --project src/CaseCompat.Cli -- --help
```

for the current argument syntax.

`doctor` includes Linux filesystem inspection such as directory
casefold state and physical file identity. Collision scanning reports
case-equivalent names without modifying the scanned files.

## Current limitations

- CaseCompat currently supports Linux only.
- Packaging currently produces a Linux framework-dependent archive.
  There is no installer, automatic `~/.local/bin` setup, or GitHub
  Releases publication workflow yet.
- Not every casing problem is safely repairable. Unsafe or ambiguous
  cases are rejected rather than guessed.
- Auto-detection of the Skyrim install is best-effort (via Steam/Proton
  library data) and always falls back to asking you for a path.
- A fix requires the source and destination to be on the same
  filesystem (true for a normal, single-drive Skyrim `Data` folder).
  A `Data` directory spanning multiple mounted filesystems is not
  supported.
