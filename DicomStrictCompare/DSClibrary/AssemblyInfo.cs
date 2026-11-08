using System.Runtime.CompilerServices;

// The vendor-string mapping helpers (modality, filter, curve type, measurement date) are internal:
// they are implementation detail of a mapper, not library API. Their translation tables are worth
// testing directly though, because a wrong mapping is silent - a mis-read curve type produces a
// plausible profile in the wrong place - so the test assembly is given access rather than the
// helpers being made public.
[assembly: InternalsVisibleTo("DSCLibrary.Tests")]
