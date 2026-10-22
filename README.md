# BONKADOO! / NGƯỢC ĐỜI

Five-chapter offline comedy puzzle-adventure for Android and iOS (Unity 6.3 LTS, URP 2D, Yarn Spinner, VI/EN).
Specification and authored content live in the kit; the Unity project consumes generated content.

```
docs/BONKADOO_Complete_Game_Development_Kit/   spec, ADRs, decisions, backlog, content, tools
  tools/author/chapters/chNN.py                chapter specs (source of truth for playable content)
  tools/author/build.py                        compiles specs -> content/*.json, dialogue/*.yarn, localization
  tools/author/simulate.py                     plays every route of every puzzle (Python mirror of the engine)
  tools/validate_content.py                    content validator (CI gate)
  docs/10-implementation/                      gap report, progress, traceability, authoring guide
Bonkadoo/                                      Unity project
  Assets/Bonkadoo/Scripts/{Domain,Application,Narrative,Infrastructure}   engine-free C# (also built by CoreTests)
  Assets/Bonkadoo/Scripts/{Infrastructure.Unity,Presentation,Editor}      Unity adapters, UI, build/setup
  CoreTests/                                   plain .NET test project over the engine-free code
  Tools/sync_content.py                        copies kit content into Unity Resources
ci.sh                                          every gate, in order
```

## Everyday commands
```bash
python3 docs/BONKADOO_Complete_Game_Development_Kit/tools/author/build.py      # after editing a chapter spec
python3 Bonkadoo/Tools/sync_content.py                                         # then copy into Unity
./ci.sh                  # all gates incl. Unity EditMode/PlayMode (needs the unity CLI + licence)
./ci.sh --skip-unity     # content + dotnet gates only
```

## Builds
`unity run Bonkadoo -- -buildTarget Android -executeMethod Bonkadoo.Editor.BuildScript.BuildAndroid` (add
`-bonkadooDev` for a development build). iOS: `BuildIOS` produces an Xcode project. No signing material is stored
in the repo; Android falls back to Unity's debug keystore.

Ads: Google Mobile Ads 11.5.0 (OpenUPM). Real IDs come from CI secrets: `BONKADOO_ADMOB_ANDROID_APP_ID`,
`BONKADOO_ADMOB_IOS_APP_ID`, `BONKADOO_ADMOB_{ANDROID,IOS}_{BANNER,INTERSTITIAL}`. Without them Google's sample IDs
are used and **release builds show no ads**. IAP: Unity IAP product `com.omnicasa.bonkadoo.removeads`.

## Human-gated before release
Brand clearance, production art/audio, editorial review of all VI/EN copy, playtests, physical-device QA,
AdMob/App Store/Play accounts and IDs, signing, privacy policy, age rating, store submission.
See `docs/BONKADOO_Complete_Game_Development_Kit/docs/10-implementation/PROGRESS.md`.
