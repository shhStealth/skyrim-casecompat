#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <output-directory>" >&2
    exit 2
fi

OUTPUT_DIR="$1"

SCRIPT_DIR="$(
    cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &&
    pwd
)"

REPO_ROOT="$(
    cd -- "$SCRIPT_DIR/.." &&
    pwd
)"

cd "$REPO_ROOT"

for COMMAND in git dotnet tar gzip sha256sum mktemp; do
    command -v "$COMMAND" >/dev/null 2>&1 || {
        echo "Required command not found: $COMMAND" >&2
        exit 1
    }
done

COMMIT="$(git rev-parse HEAD)"
SHORT_COMMIT="$(git rev-parse --short=12 HEAD)"
SOURCE_DATE_EPOCH="$(git show -s --format='%ct' "$COMMIT")"

ARCHIVE_NAME="casecompat-${SHORT_COMMIT}-linux-x64-framework-dependent.tar.gz"

mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(
    cd -- "$OUTPUT_DIR" &&
    pwd
)"

ARCHIVE="$OUTPUT_DIR/$ARCHIVE_NAME"
CHECKSUM="$ARCHIVE.sha256"

WORK_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/casecompat-package.XXXXXXXX")"

cleanup() {
    rm -rf -- "$WORK_ROOT"
}

trap cleanup EXIT

SOURCE="$WORK_ROOT/source"
PUBLISH="$WORK_ROOT/publish"
STAGE_PARENT="$WORK_ROOT/stage"
STAGE="$STAGE_PARENT/casecompat"
EXTRACT="$WORK_ROOT/extract"

mkdir -p \
    "$SOURCE" \
    "$PUBLISH" \
    "$STAGE" \
    "$EXTRACT"

echo "Packaging committed source:"
echo "  commit: $COMMIT"
echo "  output: $ARCHIVE"

if [ -n "$(git status --porcelain)" ]; then
    echo
    echo "NOTE: the worktree is not clean."
    echo "Only committed HEAD is packaged; worktree/index changes are ignored."
fi

echo
echo "== Export committed source =="

git archive "$COMMIT" |
    tar -x -C "$SOURCE"

echo
echo "== Restore =="

(
    cd "$SOURCE"
    dotnet restore CaseCompat.slnx
)

echo
echo "== Publish =="

(
    cd "$SOURCE"

    dotnet publish \
        src/CaseCompat.Cli/CaseCompat.Cli.csproj \
        -c Release \
        --no-restore \
        -p:PathMap="$SOURCE=/casecompat-source" \
        -o "$PUBLISH"
)

test -x "$PUBLISH/CaseCompat.Cli"
test -f "$PUBLISH/CaseCompat.Cli.dll"
test -f "$PUBLISH/CaseCompat.Cli.runtimeconfig.json"

echo
echo "== Stage user payload =="

cp -a "$PUBLISH"/. "$STAGE/"

find "$STAGE" \
    -maxdepth 1 \
    -type f \
    -name '*.pdb' \
    -delete

cp "$SOURCE/LICENSE" "$STAGE/LICENSE"
cp "$SOURCE/README.md" "$STAGE/README.md"

if [ -f "$SOURCE/docs/repair-exit-codes.md" ]; then
    mkdir -p "$STAGE/docs"
    cp \
        "$SOURCE/docs/repair-exit-codes.md" \
        "$STAGE/docs/repair-exit-codes.md"
fi

test -x "$STAGE/CaseCompat.Cli"

if find "$STAGE" -type f -name '*.pdb' -print -quit |
   grep -q .
then
    echo "PDB file unexpectedly remained in package payload." >&2
    exit 1
fi

grep -Fq '"tfm": "net10.0"' \
    "$STAGE/CaseCompat.Cli.runtimeconfig.json"

echo
echo "== Staged smoke test =="

"$STAGE/CaseCompat.Cli" --help > "$WORK_ROOT/staged-help.txt"

grep -Fq 'CaseCompat' "$WORK_ROOT/staged-help.txt"
grep -Fq 'repair-plan' "$WORK_ROOT/staged-help.txt"

echo
echo "== Create deterministic archive =="

TMP_ARCHIVE="$WORK_ROOT/$ARCHIVE_NAME"

tar \
    --sort=name \
    --mtime="@${SOURCE_DATE_EPOCH}" \
    --owner=0 \
    --group=0 \
    --numeric-owner \
    -C "$STAGE_PARENT" \
    -cf - \
    casecompat |
gzip -n -9 > "$TMP_ARCHIVE"

echo
echo "== Verify archive contents =="

tar -tzf "$TMP_ARCHIVE" > "$WORK_ROOT/archive-files.txt"

if grep -E '(^/|(^|/)\.\.(/|$))' "$WORK_ROOT/archive-files.txt"
then
    echo "Unsafe path found in generated archive." >&2
    exit 1
fi

grep -Fxq 'casecompat/CaseCompat.Cli' \
    "$WORK_ROOT/archive-files.txt"

grep -Fxq 'casecompat/CaseCompat.Cli.dll' \
    "$WORK_ROOT/archive-files.txt"

grep -Fxq 'casecompat/LICENSE' \
    "$WORK_ROOT/archive-files.txt"

grep -Fxq 'casecompat/README.md' \
    "$WORK_ROOT/archive-files.txt"

if grep -E '\.pdb$' "$WORK_ROOT/archive-files.txt"
then
    echo "PDB file leaked into generated archive." >&2
    exit 1
fi

echo
echo "== Clean extraction smoke test =="

tar -xzf "$TMP_ARCHIVE" -C "$EXTRACT"

test -x "$EXTRACT/casecompat/CaseCompat.Cli"

"$EXTRACT/casecompat/CaseCompat.Cli" --help \
    > "$WORK_ROOT/extracted-help.txt"

grep -Fq 'CaseCompat' "$WORK_ROOT/extracted-help.txt"
grep -Fq 'repair-plan' "$WORK_ROOT/extracted-help.txt"

echo
echo "== Publish artifact =="

mv -f -- "$TMP_ARCHIVE" "$ARCHIVE"

ARCHIVE_SHA256="$(
    sha256sum "$ARCHIVE" |
    awk '{print $1}'
)"

printf '%s  %s\n' \
    "$ARCHIVE_SHA256" \
    "$ARCHIVE_NAME" \
    > "$CHECKSUM"

echo
echo "Package complete:"
echo "  archive:  $ARCHIVE"
echo "  checksum: $CHECKSUM"
echo "  SHA256:   $ARCHIVE_SHA256"
echo "  runtime:  Microsoft.NETCore.App 10.x required"
