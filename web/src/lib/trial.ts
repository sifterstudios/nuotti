/** Public Backend origin for the marketing site. Empty falls back to same-origin relative calls. */
declare const __PUBLIC_API_BASE__: string;

export type AudienceSize = "under-50" | "50-150" | "150-400" | "400-plus";

export type TrialApplicationPayload = {
  bandName: string;
  contactName: string;
  email: string;
  city: string;
  audienceSize: AudienceSize;
  note?: string;
};

export type TrialApplicationResponse = {
  id: string;
  status: string;
  submittedAtUtc: string;
};

export type TrialSubmitResult =
  | { ok: true; data: TrialApplicationResponse }
  | { ok: false; message: string; status?: number };

function apiBase(): string {
  const fromDefine =
    typeof __PUBLIC_API_BASE__ === "string" ? __PUBLIC_API_BASE__.trim() : "";
  if (fromDefine) return fromDefine.replace(/\/$/, "");

  const fromEnv = (import.meta.env.PUBLIC_API_BASE as string | undefined)?.trim() ?? "";
  if (fromEnv) return fromEnv.replace(/\/$/, "");

  // Local default matches Backend Development bind (see AGENTS.md).
  if (import.meta.env.DEV) return "http://localhost:5240";
  return "";
}

export async function submitTrialApplication(
  payload: TrialApplicationPayload,
): Promise<TrialSubmitResult> {
  const base = apiBase();
  const url = `${base}/v1/trial/applications`;

  try {
    const response = await fetch(url, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      let message = "We could not save your trial request. Try again in a moment.";
      try {
        const problem = (await response.json()) as {
          title?: string;
          detail?: string;
          errors?: Record<string, string[]>;
        };
        const fieldError = problem.errors
          ? Object.values(problem.errors).flat()[0]
          : undefined;
        message = fieldError || problem.detail || problem.title || message;
      } catch {
        /* keep default */
      }
      return { ok: false, message, status: response.status };
    }

    const data = (await response.json()) as TrialApplicationResponse;
    return { ok: true, data };
  } catch {
    return {
      ok: false,
      message:
        "Could not reach Nuotti. Check that the Backend is running, then try again.",
    };
  }
}
