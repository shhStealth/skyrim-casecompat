using CaseCompat.Core.Repair;

namespace CaseCompat.Bethesda.Plugins;

// Minimal read-only contract every per-record-type winning consumer-spelling
// projection result implements, letting the composer and candidate
// projector iterate sources generically without knowing which Bethesda
// record kind (or, for Phase 2, which loose-asset kind) produced them.
public interface ISkyrimWinningConsumerSpellingEvidenceSource
{
    string SourceName { get; }

    string DataRoot { get; }

    bool WinnerSearchComplete { get; }

    bool ConsumerPathEvidenceComplete { get; }

    IReadOnlyList<DataRelativePathAggregateConsumerSpellingEvidence> Evidence
    {
        get;
    }

    string? Error { get; }
}
