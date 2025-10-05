# Nuotti Contracts V1 — Versioning and Deprecation

This document describes the forward‑compatibility and versioning approach for the public contracts exposed by the Nuotti application.

Scope:
- Namespace: `Nuotti.Contracts.V1`
- Serialization: System.Text.Json with stable field names
- Cross‑language mirroring: TypeScript definitions in web/shared/contracts.ts

Related files:
- C# contracts: Nuotti.Contracts/V1/
- TS mirrors: Nuotti.Contracts/web/shared/contracts.ts
- Drift check helper: tools/check-contracts.ps1

## Namespace: Nuotti.Contracts.V1

All public C# contracts intended for external consumption live under the root namespace:

- Nuotti.Contracts.V1

This includes:
- Models (e.g., GameStateSnapshot, SongRef)
- Messages/Commands (under V1.Message.*)
- Events (under V1.Event.*)
- Enums (under V1.Enum)

V1 constitutes a stable, versioned surface. Consumers should reference only V1 types from this namespace.

## Non‑breaking change rules (additive‑only)

Within V1, only additive, backward‑compatible changes are allowed. Examples of allowed changes:
- Adding new optional properties/fields with sensible defaults in deserialization
- Adding new enum values while preserving existing numeric values
- Adding new message/event types that do not change existing semantics
- Adding new overloads, constructors, or extension helpers that do not remove existing ones

Disallowed (breaking) changes in V1 include:
- Removing public types, members, or constructors
- Renaming public types or members (including JSON property names)
- Changing member types or enum numeric values
- Changing serialization defaults in a way that alters wire format

If a change is not strictly additive, it must not be made in V1.

## Deprecation policy

When a V1 member is planned for removal or replacement, mark it as obsolete using the standard .NET attribute, with guidance for consumers in the message:

- Apply [Obsolete] with a clear message that explains the replacement or the reason.
- Do not set `error: true` in V1 — keep it as a warning to preserve compatibility.

Example:

```csharp
using System;

namespace Nuotti.Contracts.V1.Model
{
    public sealed record Example
    {
        [Obsolete("Use NewProperty instead; will be removed in a future major version.")]
        public string? OldProperty { get; init; }

        public string? NewProperty { get; init; }
    }
}
```

Deprecated members remain available within V1 until a subsequent major version (e.g., V2) provides a replacement surface.

## Coexistence with V2

When introducing breaking changes, create a new side‑by‑side versioned namespace:

- New contracts live under `Nuotti.Contracts.V2`.
- V1 and V2 coexist in the same assembly and can be referenced simultaneously by different components.
- Both versions should be serialized/deserialized independently without interfering with each other.

Migration strategy:
- Keep V1 intact and maintained (bug fixes, additive improvements, and deprecations) while consumers migrate.
- Provide adapters or translation helpers when feasible to ease migration between V1 and V2 models.
- Plan a deprecation window; remove V1 only after all first‑party components have migrated and after sufficient notice to external consumers.

## Validation and drift checks

A lightweight drift check exists to reduce C# ⇄ TypeScript divergence for top‑level type names:
- Run: `pwsh -File tools/check-contracts.ps1`
- It compares public C# types under `Nuotti.Contracts.V1.*` with exported TS types/interfaces in `web/shared/contracts.ts`.

Additionally, this repository includes a simple Markdown link checker (see tools/check-docs.ps1) executed by the test suite to ensure documentation links remain valid.
