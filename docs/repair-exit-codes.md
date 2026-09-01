# Repair command exit codes

This document describes the current exit-code behavior of CaseCompat's
repair commands.

These values are an observable CLI contract. They document the existing
implementation; they are not a proposal to renumber or normalize the
commands.

## General rules

- `0` means the requested command completed successfully.
- A successful verified no-op also returns `0`. Examples include applying
  or rolling back a completed zero-child batch, and rolling back a plan
  that has no started operations.
- `2` is used by repair commands for invocation-shape errors such as the
  wrong number of arguments. Some batch commands also use `2` for an
  invalid direct-child manifest file name.
- Codes `3` and above are **command-specific**. Do not assume that the same
  numeric value has the same meaning for every repair command.
- On any nonzero exit, read the command's stderr output. It contains the
  specific failure state and, where relevant, warnings about possible
  durable progress.
- `casecompat --help`, `casecompat help`, and `casecompat -h` are successful
  help paths and return `0`.
- An unknown top-level command returns `2`.

## `repair-plan`

Creates and durably verifies one repair-plan manifest. It does not modify
Skyrim Data.

| Code | Meaning |
| ---: | --- |
| `0` | Plan manifest was created and verified successfully. |
| `2` | Invalid command invocation / argument count. |
| `3` | Path resolution or repair-plan projection raised an error. |
| `4` | No safe repair plan could be projected. |
| `5` | Repair-plan manifest construction failed. |
| `6` | Journal directory could not be opened safely. |
| `7` | Manifest could not be written durably. |
| `8` | Post-write manifest verification failed. Metadata may already exist; inspect before retrying. |

## `repair-plan-batch`

Preflights the complete input, persists independent child plans, and
publishes a durable `batch-manifest.json` completion record.

| Code | Meaning |
| ---: | --- |
| `0` | Batch planning completed successfully, including a valid completed zero-child batch when every input was safely rejected. |
| `2` | Invalid command invocation / argument count. |
| `3` | Input, path, manifest-name, resolver, or projection preflight error. |
| `5` | Batch output directory could not be safely opened, inspected, or accepted as the required empty output directory. |
| `6` | A child plan directory could not be created or durably published. Partial batch metadata may exist. |
| `7` | A nested `repair-plan` child did not persist successfully. Partial batch metadata may exist. |
| `8` | A persisted child could not be safely reopened/read back and bound into the batch membership record. Partial child metadata may exist. |
| `9` | Durable batch completion-manifest construction failed after child-plan persistence. |
| `10` | Durable batch completion-manifest publication or verification failed after child-plan persistence. |

`repair-plan-batch` intentionally has no exit code `4`.

For failures after child persistence begins, inspect the batch directory
rather than assuming that no metadata was written.

## `repair-status`

Read-only inspection of one persisted repair plan.

| Code | Meaning |
| ---: | --- |
| `0` | Status inspection completed successfully. |
| `2` | Invalid command invocation / argument count. |
| `3` | Journal directory could not be opened safely. |
| `4` | Plan status inspection failed. |

A successful status command does not imply that the plan is applied or
rolled back. The reported lifecycle state is the result to inspect.

## `repair-status-batch`

Read-only inspection of a completed manifest-backed batch or a supported
legacy observational batch.

| Code | Meaning |
| ---: | --- |
| `0` | Batch status inspection completed successfully. |
| `2` | Invalid invocation or invalid direct-child manifest file name. |
| `3` | Batch directory could not be opened safely. |
| `4` | Batch completion/topology/child inspection could not be completed. |

This command is observational only. A successful status inspection does
not create apply or rollback authority.

## `repair-apply`

Executes one persisted plan through the hardened whole-plan forward
lifecycle.

| Code | Meaning |
| ---: | --- |
| `0` | Whole-plan forward execution completed durably. |
| `2` | Invalid command invocation / argument count. |
| `3` | Journal directory could not be opened safely. |
| `4` | Whole-plan execution did not reach durable success, or execution raised an error. |

After exit `4`, do not assume the plan is unchanged. Durable journals or
filesystem progress may already exist. Inspect with `repair-status` before
retrying or rolling back.

## `repair-apply-batch`

Verifies the entire durable batch before executing child plans in recorded
batch order.

| Code | Meaning |
| ---: | --- |
| `0` | Every recorded child reached durable forward success, or the verified batch contained zero recorded child plans. |
| `2` | Invalid invocation or invalid direct-child manifest file name. |
| `3` | Batch directory could not be opened safely. |
| `4` | Durable batch completion/membership verification failed before child mutation. |
| `5` | A recorded child directory could not be safely opened for mutation. |
| `6` | A child forward execution failed or raised an error after batch execution began. |

Exit `4` occurs before any child mutation by that invocation.

After exit `5` or `6`, earlier children may already have completed
durably. With exit `6`, the failing child may also have durable progress.
Later children are not attempted. Inspect the batch and failing child
before retrying.

There is no batch-wide atomic filesystem transaction.

## `repair-rollback`

Rolls back CaseCompat-owned work for one persisted plan through the
hardened whole-plan rollback lifecycle.

| Code | Meaning |
| ---: | --- |
| `0` | Rollback completed successfully, including the no-started-operations case. |
| `2` | Invalid command invocation / argument count. |
| `3` | Journal directory could not be opened safely. |
| `4` | Whole-plan rollback did not reach durable success, or execution raised an error. |

After exit `4`, do not assume rollback made no changes. Some owned
filesystem objects may already have been removed or rollback journals may
have advanced. Inspect with `repair-status` before retrying rollback or
attempting apply.

## `repair-rollback-batch`

Verifies the entire durable batch before rolling back children in
**reverse recorded batch order**.

| Code | Meaning |
| ---: | --- |
| `0` | Every applicable recorded child completed rollback successfully, including verified zero-child and never-started no-op cases. |
| `2` | Invalid invocation or invalid direct-child manifest file name. |
| `3` | Batch directory could not be opened safely. |
| `4` | Durable batch completion/membership verification failed before child rollback. |
| `5` | A recorded child directory could not be safely opened for rollback. |
| `6` | A child rollback failed or raised an error after batch rollback began. |

Exit `4` occurs before any child rollback by that invocation.

After exit `5` or `6`, children later in the original apply order may
already have completed rollback. With exit `6`, the failing child may also
have durable rollback progress. Earlier children in the original apply
order are not attempted after the failure.

No automatic forward re-apply or compensating execution is attempted.

There is no batch-wide atomic filesystem transaction.

## Scripting guidance

For automation, check the exit code first and preserve stderr.

Do not write logic such as "exit `4` always means the same thing" across
different repair commands. Instead, interpret the code in the context of
the specific command that was executed.

For mutation commands, a nonzero exit does **not** necessarily mean that
nothing changed. Follow the command's warning text and inspect durable
state with `repair-status` or `repair-status-batch` before retrying.
