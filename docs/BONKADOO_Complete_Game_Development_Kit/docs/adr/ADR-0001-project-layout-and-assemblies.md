# ADR-0001 Project layout and assembly boundaries
Status: accepted (M0)

## Context
ARCHITECTURE.md names Domain / Application / Infrastructure / Presentation / Tests assemblies. The save system and content loader are pure C# but need JSON; platform adapters (persistent path, ads, IAP, Yarn) need UnityEngine.

## Decision
- Unity project: `source/Bonkadoo` (sibling of `source/docs`). The kit's `content/` stays the single source of truth; `tools/sync_content.py` copies it into `Assets/Bonkadoo/Content/Resources/BonkadooContent/`, and a test fails when the copy drifts.
- Assemblies (asmdef):
  - `Bonkadoo.Domain` — `noEngineReferences`, no dependencies.
  - `Bonkadoo.Application` — `noEngineReferences`, refs Domain.
  - `Bonkadoo.Infrastructure` — `noEngineReferences`, refs Domain, Application, Newtonsoft.Json. Content loading, save files, localization tables.
  - `Bonkadoo.Infrastructure.Unity` — Unity adapters (paths, ads, IAP, Yarn, logging).
  - `Bonkadoo.Presentation` — MonoBehaviours, input, UI.
  - `Bonkadoo.Tests.EditMode`, `Bonkadoo.Tests.PlayMode`.
- The three engine-free assemblies also compile in `source/Bonkadoo/CoreTests` (plain .NET, NUnit) so domain tests run without the Unity editor, proving "Domain tests run without Unity engine references".
- Language level: C# 9 subset that Unity 6.3 compiles — no `record`, no `init`. Tests use NUnit APIs present in Unity's NUnit 3.5 build.

## Consequences
Two test runners (dotnet for speed, Unity for integration). Shared test sources must avoid UnityEngine types.
