# Wandering near-contact band fix

Follows retained checkpoint 3ba80a7f. The outer-neighbour proximity band now activates before the target has a nonzero offset. This prevents contact-only deformation from disappearing across a triangle boundary, including the previous failure around 4.97 seconds.

Validation passed: 27-node full parameter grid interpolation; 9 control and 24 interior inspection poses; CPU and actual GPU trajectory samples from 0 through 15 seconds; target, neighbour, self-contact and root checks; Clarity/Gathering/Wandering transitions. All 18 petals retain byte-identical standard positions at 23 phases from 78% through 100%. Local scalar displacement stays within 0.10. This is targeted validation, not a full-flower all-time guarantee or performance test.

CPU/GPU minimum rendered-sheet gap: 0.000412688 at 15 seconds. CPU and GPU image comparison is in RenderComparison.json. Full formal cache rebuild follows these passing checks.

Formal full rebuild completed successfully with the normal acceptance path (no retention override). A 15-second ordinary GPU Wandering playback check reported FORMAL_WANDERING_GPU_REVIEW_READY, with no runtime, shader or stale-cache errors; the interactive Wandering scene remains running. FormalCacheHashes.json records the published data. The accepted static compensation and existing GPU base playback file remain byte-identical.
