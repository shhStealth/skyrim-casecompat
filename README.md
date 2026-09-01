# Skyrim Linux Case Compatibility

CaseCompat is a Linux command-line tool for investigating path-casing
problems in heavily modded Skyrim Special Edition installations.

A mod can contain the file Skyrim needs while still using a different
capitalization from the path requested by a plugin, mesh, or other asset.
On case-sensitive Linux paths, differences such as `meshes` versus
`Meshes` can matter.

CaseCompat provides read-only diagnostics, durable repair planning,
verified apply operations, status inspection, and rollback for repairs
that can be proven safe.

## What CaseCompat does

The repair workflow has four main stages:

1. **Plan** — inspect a requested Data-relative path and persist a repair
   plan without modifying Skyrim Data.
2. **Status** — inspect the durable state of that plan or batch.
3. **Apply** — execute a previously persisted and verified plan.
4. **Rollback** — remove or reverse CaseCompat-owned repair work when the
   required authority can still be proven.

CaseCompat supports both a single-path workflow and a batch workflow.

It also includes diagnostic commands for filesystem case behavior,
case-equivalent directory collisions, Skyrim plugin/load-order data,
asset-path resolution, and archive evidence.

## Safety model

Repair operations are intentionally fail-closed.

Important properties of the current implementation:

- `repair-plan` and `repair-plan-batch` do **not** modify Skyrim Data.
- `repair-status` and `repair-status-batch` are read-only.
- Skyrim Data, journal directories, and batch directories are supplied
  explicitly by the caller. CaseCompat does not guess or auto-discover
  those repair-authority roots.
- Apply and rollback operate from durable repair metadata rather than
  inventing a new repair from the current pathname alone.
- Batch apply and batch rollback verify the durable completed batch and
  its recorded child membership before child mutation begins.
- A repair is refused when the required filesystem or historical
  authority cannot be proven.
- Rollback is limited to work that CaseCompat can prove it owns and is
  authorized to remove or reverse.
- There is **no batch-wide atomic filesystem transaction**. If a batch
  mutation fails after execution begins, earlier children can already
  have durable progress.
- A nonzero exit from a mutation command does not necessarily mean that
  nothing changed. Inspect status before retrying.

Planning success therefore means "a repair plan was safely persisted,"
not "you should automatically apply it." Review the command output and
status before mutation.

## Requirements

CaseCompat can be run from source or from a framework-dependent Linux
package.

To run from source, you need:

- Linux.
- A .NET 10 SDK.
- A checkout of this repository.
- The path to the Skyrim Special Edition `Data` directory you want to
  inspect.

The CLI project currently targets `net10.0`.

To run a packaged build, you need:

- Linux.
- The .NET 10 runtime (`Microsoft.NETCore.App 10.x`).
- The extracted CaseCompat package.
- The path to the Skyrim Special Edition `Data` directory you want to
  inspect.

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
dependencies, `README.md`, `LICENSE`, and
`docs/repair-exit-codes.md`. Development PDB files are excluded.

After extracting the archive, show the command reference with:

```bash
./casecompat/CaseCompat.Cli --help
```

There is currently no installer and CaseCompat does not automatically
modify `~/.local/bin` or another system path. The repository also does
not yet publish these archives through a GitHub Releases workflow.

## Build and run from source

From the repository root:

```bash
dotnet build
```

Show the current command reference:

```bash
dotnet run --project src/CaseCompat.Cli -- --help
```

The remainder of this README uses that source-tree invocation.

## Single-path quickstart

Choose your Skyrim Data directory, one requested Data-relative asset
path, and a separate journal directory.

For example:

```bash
DATA="/path/to/Skyrim Special Edition/Data"
REQUESTED="meshes/example/file.nif"
JOURNAL="$HOME/casecompat-journals/example"

mkdir -p "$JOURNAL"
```

`REQUESTED` is relative to Skyrim `Data`; do not include the `Data`
directory itself in that value.

### 1. Create the plan

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-plan "$DATA" "$REQUESTED" "$JOURNAL"
```

This persists the plan metadata but does not modify Skyrim Data.

Unless you explicitly supply another direct-child manifest name,
CaseCompat uses:

```text
repair-plan.json
```

### 2. Inspect status

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status "$JOURNAL" "$DATA"
```

Read the reported lifecycle state and the plan output before applying
anything.

### 3. Apply the verified plan

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-apply "$JOURNAL" "$DATA"
```

### 4. Inspect status again

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status "$JOURNAL" "$DATA"
```

Do not infer success or failure solely from the presence of files.
The durable repair state is part of the lifecycle.

## Batch quickstart

Batch planning accepts a path-list file plus a separate batch output
directory.

Choose the inputs:

```bash
DATA="/path/to/Skyrim Special Edition/Data"
PATHS="$HOME/casecompat-paths.txt"
BATCH="$HOME/casecompat-batches/example"
```

Create the path-list file with Data-relative paths, for example:

```text
meshes/example/first.nif
meshes/example/second.nif
textures/example/example.dds
```

Use a new, empty batch directory:

```bash
mkdir -p "$BATCH"
```

### 1. Plan the batch

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-plan-batch "$DATA" "$PATHS" "$BATCH"
```

Batch planning preflights the input and persists independent safe child
plans. It does not modify Skyrim Data.

### 2. Inspect the completed batch

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status-batch "$BATCH" "$DATA"
```

### 3. Apply the verified batch

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-apply-batch "$BATCH" "$DATA"
```

### 4. Inspect the batch again

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status-batch "$BATCH" "$DATA"
```

Batch children are executed in recorded order. The batch is not a
single atomic filesystem transaction.

## Repair manifests

Two names are important and have different purposes.

### `repair-plan.json`

`repair-plan.json` is the default repair-plan manifest name.

For a single repair it is the default manifest under the supplied
journal directory. For a batch it is the default child-plan manifest
name recorded for the batch.

The manifest name can still be supplied explicitly when needed.

For example:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-plan "$DATA" "$REQUESTED" "$JOURNAL" custom-plan.json
```

Commands that consume an explicitly named single plan use the explicit
form:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status "$JOURNAL" custom-plan.json "$DATA"
```

The corresponding batch commands also retain explicit child-manifest
forms.

### `batch-manifest.json`

`batch-manifest.json` is different.

It is the fixed durable completion and membership record published for
a completed batch. It records the batch's executable child set and
associated plan identity information.

It is not the optional child-manifest argument and is not selected by
the short-form default.

If a completed batch records a different child manifest name, invoking
a short batch mutation with the default `repair-plan.json` does not
search for a likely alternative. The membership mismatch is refused
before child mutation.

## Rollback

### Single plan

To roll back CaseCompat-owned work for one plan:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-rollback "$JOURNAL" "$DATA"
```

Then inspect it:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status "$JOURNAL" "$DATA"
```

### Batch

To roll back a verified completed batch:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-rollback-batch "$BATCH" "$DATA"
```

Then inspect it:

```bash
dotnet run --project src/CaseCompat.Cli -- \
  repair-status-batch "$BATCH" "$DATA"
```

Batch rollback processes applicable children in reverse recorded batch
order.

If rollback returns a nonzero exit after execution has begun, do not
assume that no rollback progress occurred. Inspect status before
deciding what to do next.

## Exit codes

Repair exit codes are command-specific.

In particular, the same numeric nonzero value does not necessarily
mean the same thing for every repair command.

The detailed observable CLI contract is documented here:

**[Repair command exit codes](docs/repair-exit-codes.md)**

For scripts:

- check the command's exit code;
- preserve and read stderr;
- inspect durable status after mutation failures;
- do not treat every nonzero mutation result as "nothing changed."

## Read-only diagnostics

The CLI also exposes diagnostic and inventory commands outside the
repair quickstart.

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
- Repair metadata is durable, but durable history by itself does not
  grant unlimited filesystem mutation authority.
- Batch apply and rollback are ordered operations, not batch-wide
  atomic transactions.
- A failed child operation can leave durable progress that must be
  inspected before retrying.
- CaseCompat does not automatically compensate for a partially
  completed batch by running the opposite operation.

For repair failures, inspect the corresponding `repair-status` or
`repair-status-batch` result before retrying.
