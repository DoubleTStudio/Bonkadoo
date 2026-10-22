# Monetization specification (fixed requirement)
Full game is FREE. Small banner only on non-obstructive surfaces, not over puzzle objects, dialogue, safe-area controls or accessibility affordances. Interstitial eligibility requires: Remove Ads not owned; active gameplay >=300 seconds since last shown; natural scene/chapter break; no dialogue/cutscene/puzzle active; not first-session opening grace; frequency cap at most 1 per 5 minutes (recommend longer after playtest). Do not queue an ad to fire immediately on return from background. Failed/no-fill ad must not block transitions. Track impressions, not only requests.

One-time non-consumable Remove Ads purchase, intended price US$5 (localized store tiers subject to review). Hide all banner/interstitial once entitlement confirmed, persist and restore via platform IAP, handle offline ownership and refunds/revocations according to supported APIs. Restore Purchases button on iOS; purchase and restore errors nonblocking. No other purchase types, paid chapters, consumables or rewarded-ad-gated hints.

Consent/privacy: follow current Apple/Google/AdMob rules, ATT where applicable, GDPR/other regional consent as applicable, child-directed rating review. Do not promise personalized ads without consent. Final SDK integration and store compliance require current verification.

## AdPolicy tests
- 299s active play => no interstitial.
- 301s, mid-puzzle => no interstitial.
- 301s, natural transition => eligible only if other gates satisfied.
- Remove Ads owned => no ads ever.
- Ad SDK offline/error => transition continues.
- Resume app after 10min background => background time does not count as active play.
