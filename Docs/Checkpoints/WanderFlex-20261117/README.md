# Retained Wandering flex candidate

User requested preservation before further fixes on 2026-11-17.

This is an unapproved CPU inspection candidate: 2.86–5 seconds, cosine forward/reverse replay. L1-P3 follows L0-P3 tilt (independent tilt fraction zero). All other petals use their original motion at the inspection time. Controls use the 20-pass smoothed normal basis and the accepted static geometry.

ClipControls.csv contains the exact sorted control poses retained by the interactive preview. Interpolate linearly in time with the existing cubic B-spline field; roots remain pinned.

Known failure: interpolation contact with L0-P2 (global 2) near time 4.972 seconds, against target L1-P3 (global 10). Neither continuous clearance nor full Wandering playback has passed. Formal caches were subsequently fully regenerated for this checkpoint; see FormalCacheHashes.json.

## Explicit retention authorization

The user subsequently requested full Wandering playback data, formal cache generation and a commit before further changes, explicitly allowing known contacts to remain. `Tools/Bake.ps1 -RetainFlexCandidate` is the explicit checkpoint path: it fills the full motion-parameter grid with the same flexible field and conservative target-following motion, reports failed samples/interpolations, and permits their retention without labelling them clearance-approved. Ordinary baking continues to require the normal acceptance checks.

The accepted static geometry is rebuilt with the existing full geometry path, not PlaybackOnly. The exact short inspection clip is preserved above independently of this full-range extension. StreamingAssets remain local generated artifacts under the repository's existing ignore policy; cache hashes and diagnostic results are recorded with the checkpoint.

Full bake completed successfully: 729 grid nodes. All unique point solves passed; maximum-depth interpolation checking still failed and is explicitly retained as a known candidate limitation. No clearance-approval claim is made. Standard BottomOverlap.bytes is byte-identical to the accepted baseline.

GPU smoke check: ordinary Play loaded the fresh cache, entered Wandering, ran beyond the transition with no runtime/shader/cache-staleness errors in RetainedGpuPlayback.log. This is an execution check, not clearance approval. The new ~1 MB WanderingFlex.bin is committed; the existing ~1.17 GB generated FlowerPlayback.bin remains local under the established ignore policy and is covered by FormalCacheHashes.json.
