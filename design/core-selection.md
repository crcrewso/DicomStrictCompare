# Which cores compute threads use

**Status: a stand-in, for future review.** Code: `DSClibrary/Comparing/CpuTopology.cs`.

## The intent

Never pick substandard cores automatically. crcrewso, 2026-11-15:
- *"SMT threads should always be ignored"*
- *"limit yourself to not use low power e cores"*
- *"The intent is to prevent automatic selection of substandard cores, I'm not sure the best way to do that ... Only
  cores with L3 seems like a good stand-in for now but this should be documented for future review."*

## The rule as built

- **One thread per physical core.** SMT siblings are never counted.
- **Only cores with an L3 cache,** when any core has one. A machine where no core reports an L3 uses every core.
- **Threads aren't pinned**: the OS schedules them on every platform (crcrewso, 2026-11-15: *"let windows figure out
  the scheduling"*, then *"defaulting to linux be the scheduling agent should be the goal, so let's try it that way for
  now"*). `CpuTopology.PinByDefault` is false; pinning stays for measuring, in DSCgpu `cores`.
- **The Threads list** (the Settings tab, Avalonia and Eto) runs from 1 to every logical processor, SMT included, in
  steps of 1 to 4, then 2 to 12, then 4, and defaults to the recommended count, marked "(recommended)". The machine
  settings file keeps the recommended choice as no number, so it follows the machine. A user's chosen count always
  wins.

## Why L3 is only a stand-in

L3 is a proxy for "full-speed core", not a measure of speed.

| Machine | What the rule does | Checked? |
|---|---|---|
| Intel Meteor, Arrow (H), Panther Lake | leaves out the SoC-tile LP E-cores | measured on jackdaw (Panther Lake): an LP E-core does about a third of a P- or E-core's work |
| Intel Lunar Lake | its only E-cores have no L3, so 4 of 8 cores are left out | **not measured**; Intel's layout, inferred |
| AMD, one L3 per CCD | uses every core | crane's 5900XT on Windows |
| Intel without hybrid cores | uses every core | not measured |
| ARM, or anything with no cache data | uses every core | not measured |
| macOS | counts `hw.physicalcpu`, which includes Apple's E-cores, and doesn't pin | **can't be tested here** |

## Open questions

Raised 2026-11-15; 1 and 2 are now mostly handled, 3 and 4 are open:
1. **Windows detection ignores the process's own CPU limit; Linux honours it.** The count is now capped at
   `Environment.ProcessorCount`, which honours it, and Windows no longer pins, so only the core list itself is off.
2. **A Linux container's CPU quota:** handled by the same cap, 2026-11-15.
3. **Pinning, decided for now: the OS schedules** (crcrewso, 2026-11-15). What it costs, measured on jackdaw (Core
   Ultra X7 358H, Linux), DSCgpu `cores --nobox`, best of 5, identical counts in every row:

   | Pair (grid spacing) | 12 pinned | 12 unpinned | 16 unpinned |
   |---|---|---|---|
   | E11V11 vs E15V11 AiXflatXaaa 06x10x10 (2 mm against 2 mm) | 0.075 s | 0.085 s | 0.077 s |
   | E15V11 vs E15V15 STBflatXaaa 06f10x10 (2 mm against 1 mm) | 0.061 s | 0.088 s | 0.077 s |

   Unpinned, 12 threads are 13–44 % slower than pinned, and slower than 16. *Inferred:* the scheduler puts some
   of the 12 on the LP-E cores, which work at a third of the speed, and the others then wait. crane's W2 measures the
   same on Windows. Two DSC instances at once aren't supported (crcrewso, 2026-11-15).
4. **Fewer threads than cores take the lowest-numbered cores** when pinned. Only matters for measuring now.

A better test of "substandard" than L3 might be measured: a short timed loop per core type at start-up. Not built.
