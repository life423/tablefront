# Tablefront art direction

Painted miniature military pieces on a recessed flocked campaign board, surrounded by walnut and aged brass. Grounded stylized lighting and faceted trees keep the entire scene in the same physical tabletop language.

Shared palette: ink `#172326`, walnut `#3D2A23`, flock `#829071`, route sand `#C0B491`, ivory `#F6EED8`, brass `#DDBB73`. Three deliberate faction enamel accents: blue `#54BAE4`, orange `#EEA05A`, violet `#BD92DF`. UI text colors, material colors and lighting originate in `TablePalette`.

Warm studio light arrives diagonally at 46 degrees with a soft cool fill and gradient ambient bounce. A restrained ACES grade, bloom, vignette and color adjustment preserve battlefield clarity. Units are recognisable compound models: tracked tank with long gun; wheeled recon vehicle; two armored rifle troopers. Shared enamel bases, rings and distinct headquarters make ownership visible; unit silhouettes also convey class without relying only on color.

The static terrain is combined by material. Unit models retain shared meshes/materials; combat traces and impacts use a fixed 128-slot pool. No cosmetic object blocks simulation movement. Generated audio passes through Unity's master listener gain, controlled by the game's saved volume preference; physical listening is a human validation follow-up.

Craft references read: art-direction, scene-lighting, placeholder-assets, VFX and game-audio. URP postprocessing skill read; installed URP 17.6 Bloom/Tonemapping/Vignette/ColorAdjustments and pipeline-asset source confirmed the API. Unity 6000.6 LineRenderer API checked. The hosted URP 17.6 Volumes URL was unavailable; installed package API is the version-matched fallback. Parent integrates and owns rendered verification/build proof.
