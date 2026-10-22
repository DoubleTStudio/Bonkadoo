# Mobile UX, accessibility, localization
Landscape responsive scene (~75% focus), compact bottom inventory, upper-left menu, top-right hint, unobtrusive speech bubbles. Safe areas for cutouts. Tutorial integrated into Office Escape, not a modal wall. Tap and drag equivalents; reversible selections; haptic optional; dialogue log and speed controls; readable type with adjustable scale; reduced motion.

Languages: `vi` and `en`; keys in JSON, fallback to English only in development; release blocks if keys missing. Dialogue may be culturally adapted while preserving clue semantics. Keep numerals and formatting locale-aware; test Vietnamese diacritics and font coverage on iOS/Android. Pseudolocalization test and overflow screenshots mandatory.

Offline: launch, play, save, resume, endings all work in airplane mode. Ads/analytics failures must not block game. Low-memory suspend/resume tested during puzzle and cutscene.
