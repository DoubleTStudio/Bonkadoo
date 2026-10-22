# Art bible and production pipeline
## Visual identity
Expressive colorful 2D cartoon; simplified silhouettes, clear facial acting, Vietnamese-inspired absurd city without copying real brand identities. Tí: messy hair, loose office shirt, crooked red tie; Bốp: hovering rounded cube with minimal LED face. Scenes feel familiar then reveal bureaucratic surrealism. World uses exaggerated signage, forms, queues, stamps and office furniture as readable gameplay affordances.

## Style rules
Strong readable silhouettes at phone scale; character outline consistent; 3-value lighting; restrained background detail behind interactive hotspots; interactable objects get hover/tap affordance and consistent highlight. UI labels not baked into images. No AI-generated visual asset can be considered final until art-direction, licensing and consistency review.

## Pipeline
Concept sheet -> character turnaround -> expression sheet -> layered source (PSD/Krita or equivalent) -> cutout rig/Unity 2D Animation -> export sprites/atlas -> prefab -> animation tests -> visual QA. Backgrounds: blockout -> interactable pass -> atmosphere -> occlusion QA -> mobile compression.

## Asset IDs and target budget
`chr.ti`, `chr.bop`, `chr.vuong`, `chr.coc`, `chr.mai`, `chr.lan`, `chr.logic`; scene backgrounds per `sceneId`; hotspot props per puzzle; animation states idle/talk/react/use/walk/surprised/sad/celebrate. Initial target 7 main character sets, ~15 scenes, 45 puzzle prop clusters, 150–250 reaction/SFX cues; counts subject to scope review. Use `content/assets/manifest.csv` as authoritative tracker.

## QA
No unreadable interactables, visual clue hidden behind ads/safe area, clipping at 16:9 and 19.5:9, animation glitches at 30fps, asset ownership/license unknown. Deliver source art and exports; document prompts/licenses if AI-assisted.
