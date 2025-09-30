Nuotti.Contracts

Purpose
- This project contains the shared message and data contracts used to communicate between Nuotti applications (Backend, Audience, Projector, AudioEngine, TestClient, etc.).
- These contracts define the shape of events, commands, and DTOs exchanged over SignalR, messaging, and any other inter‑process boundaries.
- By centralizing them, we ensure all components compile against the same types and serialization rules, reducing drift and runtime incompatibilities.

What “contracts” do in this project
- Represent the public API between services/apps. Examples include:
  - Session lifecycle messages (e.g., SessionCreated)
  - Quiz flow messages (e.g., QuestionPushed, AnswerSubmitted, JoinedAudience)
  - Media control messages (e.g., PlayTrack)
- Act as the single source of truth for field names and types used in serialization (System.Text.Json by default unless otherwise stated).
- Enable compile‑time checks across projects, so breaking changes are caught early.

Versioning policy (important)
- We use versioning for our contracts. The Nuotti.Contracts package/project is versioned using SemVer (MAJOR.MINOR.PATCH):
  - MAJOR: Breaking changes to any contract (rename/remove fields, change types, behavior that breaks consumer expectations, etc.).
  - MINOR: Backward‑compatible additions (add new optional fields, add new messages) that existing consumers can safely ignore.
  - PATCH: Backward‑compatible fixes (documentation, comments, non‑breaking refactors).
- Consumers should pin a compatible version of Nuotti.Contracts. Update carefully when MAJOR versions change.
- Avoid breaking changes. Prefer additive changes with sensible defaults. If a breaking change is unavoidable, coordinate across all impacted repos and update the MAJOR version.

Guidelines for updating/adding contracts
- Be explicit and stable: choose clear names and types. Favor primitives and simple, serializable shapes.
- Prefer additive changes:
  - Add fields rather than removing or renaming existing ones.
  - When adding fields, make them optional or provide defaults to keep backward compatibility.
- Document intent in XML doc comments on each message/DTO.
- If you must deprecate a field or message:
  - Mark it as [Obsolete] with guidance on alternatives.
  - Keep it for at least one MINOR cycle before removal in the next MAJOR release.
- Validate serialization:
  - Keep attributes (e.g., JsonPropertyName) aligned across producers/consumers if introduced.
  - Avoid breaking casing or nullability expectations.

How to release a new contracts version
1) Make your changes in Nuotti.Contracts and update XML docs.
2) Bump Nuotti.Contracts version:
   - PATCH for non‑breaking fixes
   - MINOR for additive changes
   - MAJOR for breaking changes (coordinate widely)
3) Build and publish the package or propagate the project reference update.
4) Update all dependent projects to the new version and run integration tests.

FAQ
- Can I change an existing property’s type? Only in a MAJOR release, and only with coordination.
- Can I add a new message type? Yes, typically MINOR, provided it doesn’t break existing flows.
- Do I need to version each message individually? No. We version the Nuotti.Contracts package as a whole. Keep per‑type XML docs up to date for clarity.

Scope
- This repository focuses on the definition of contracts only. Business logic, storage, and UI concerns live in their respective projects. Contracts should remain lightweight, serializable, and well‑documented.
