using Xunit;

/*
 * CaseCompat's integration suite exercises real Linux descriptor-backed
 * filesystem locking, durable journals, and namespace mutation.
 *
 * Test fixtures use isolated temporary trees, but default-parallel CI
 * execution has produced nondeterministic filesystem-authority failures,
 * including a nonblocking flock() EAGAIN / EWOULDBLOCK result.
 *
 * Serialize test collections so CI exercises these low-level transaction
 * semantics deterministically.
 *
 * This changes test execution policy only. Production locking remains
 * fail-closed and nonblocking.
 */
[assembly: CollectionBehavior(DisableTestParallelization = true)]
