// Nuotti TypeScript contract mirrors (V1)
// - Enums are represented as string unions to match JSON string enums from .NET
// - Properties use camelCase to align with REST/web usage (ContractsJson.RestOptions)
// - DateTime values are represented as ISO 8601 strings
//
// Compile type-check only (no emit):
//   npx tsc -p ../tsconfig.json

// ===== Enums (string unions) =====
export type Role = "Performer" | "Projector" | "Audience" | "Engine";
export const Roles: readonly Role[] = ["Performer", "Projector", "Audience", "Engine"] as const;
export const isRole = (v: unknown): v is Role => typeof v === "string" && (Roles as readonly string[]).includes(v);

export type Phase =
    | "Idle"
    | "Lobby"
    | "Start"
    | "Hint"
    | "Guessing"
    | "Lock"
    | "Reveal"
    | "Play"
    | "Intermission"
    | "Finished";
export const Phases: readonly Phase[] = [
    "Idle",
    "Lobby",
    "Start",
    "Hint",
    "Guessing",
    "Lock",
    "Reveal",
    "Play",
    "Intermission",
    "Finished",
] as const;
export const isPhase = (v: unknown): v is Phase => typeof v === "string" && (Phases as readonly string[]).includes(v);

export type ReasonCode = "None" | "InvalidStateTransition" | "UnauthorizedRole" | "DuplicateCommand";
export const ReasonCodes: readonly ReasonCode[] = [
    "None",
    "InvalidStateTransition",
    "UnauthorizedRole",
    "DuplicateCommand",
] as const;
export const isReasonCode = (v: unknown): v is ReasonCode =>
    typeof v === "string" && (ReasonCodes as readonly string[]).includes(v);

// ===== Value Objects / Models =====
export interface SongId {
    value: string; // C#: public readonly record struct SongId(string Value)
}

export interface SongRef {
    id: SongId; // C#: SongRef(SongId Id, string Title, string Artist)
    title: string;
    artist: string;
}

export interface GameStateSnapshot {
    sessionCode: string;
    phase: Phase;
    songIndex: number;
    currentSong?: SongRef | null; // nullable in C#
    choices: string[]; // never null in C# (defaults to [])
    hintIndex: number;
    tallies: number[]; // never null in C# (defaults to [])
    scores: Record<string, number>; // never null in C# (defaults to {})
    songStartedAtUtc?: string | null; // C# DateTime? as ISO string
}

// ===== Minimal runtime validators (MVP) =====
const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === "object" && v !== null;

export const isSongId = (v: unknown): v is SongId => isRecord(v) && typeof v.value === "string";

export const isSongRef = (v: unknown): v is SongRef =>
    isRecord(v) && isSongId((v as any).id) && typeof (v as any).title === "string" && typeof (v as any).artist === "string";

export const isGameStateSnapshot = (v: unknown): v is GameStateSnapshot => {
    if (!isRecord(v)) return false;
    const x = v as any;
    if (typeof x.sessionCode !== "string") return false;
    if (!isPhase(x.phase)) return false;
    if (typeof x.songIndex !== "number") return false;
    if (x.currentSong != null && !isSongRef(x.currentSong)) return false;
    if (!Array.isArray(x.choices) || !x.choices.every((c: unknown) => typeof c === "string")) return false;
    if (typeof x.hintIndex !== "number") return false;
    if (!Array.isArray(x.tallies) || !x.tallies.every((n: unknown) => typeof n === "number")) return false;
    if (!isRecord(x.scores) || !Object.values(x.scores).every((n) => typeof n === "number")) return false;
    if (x.songStartedAtUtc != null && typeof x.songStartedAtUtc !== "string") return false;
    return true;
};
