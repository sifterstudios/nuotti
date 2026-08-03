<script lang="ts">
  import {
    submitTrialApplication,
    type AudienceSize,
    type TrialApplicationPayload,
  } from "$lib/trial";

  type Status = "idle" | "submitting" | "success" | "error";

  let bandName = $state("");
  let contactName = $state("");
  let email = $state("");
  let city = $state("");
  let audienceSize = $state<AudienceSize | "">("");
  let note = $state("");
  let status = $state<Status>("idle");
  let errorMessage = $state("");
  let confirmationId = $state("");

  let canSubmit = $derived(
    status !== "submitting" &&
      bandName.trim().length > 0 &&
      contactName.trim().length > 0 &&
      email.trim().length > 0 &&
      city.trim().length > 0 &&
      audienceSize !== "",
  );

  async function onSubmit(event: Event) {
    event.preventDefault();
    if (!canSubmit || audienceSize === "") return;

    status = "submitting";
    errorMessage = "";

    const payload: TrialApplicationPayload = {
      bandName: bandName.trim(),
      contactName: contactName.trim(),
      email: email.trim(),
      city: city.trim(),
      audienceSize,
    };
    if (note.trim()) payload.note = note.trim();

    const result = await submitTrialApplication(payload);
    if (result.ok) {
      status = "success";
      confirmationId = result.data.id;
      return;
    }

    status = "error";
    errorMessage = result.message;
  }
</script>

{#if status === "success"}
  <div class="success" role="status">
    <p class="eyebrow">Trial request received</p>
    <h3>You're on the list</h3>
    <p class="lede">
      We'll email <strong>{email}</strong> with exclusive trial access for
      <strong>{bandName}</strong>. Keep an eye on your inbox.
    </p>
    <p class="ref">
      Reference <code>{confirmationId.slice(0, 8)}</code>
    </p>
  </div>
{:else}
  <form class="form" onsubmit={onSubmit} novalidate>
    <div class="grid">
      <label>
        <span>Band / act name</span>
        <input
          name="bandName"
          autocomplete="organization"
          maxlength="120"
          required
          bind:value={bandName}
          placeholder="e.g. Midnight Brass"
        />
      </label>
      <label>
        <span>Your name</span>
        <input
          name="contactName"
          autocomplete="name"
          maxlength="120"
          required
          bind:value={contactName}
          placeholder="e.g. Alex Rivera"
        />
      </label>
      <label>
        <span>Email</span>
        <input
          type="email"
          name="email"
          autocomplete="email"
          maxlength="320"
          required
          bind:value={email}
          placeholder="you@band.example"
        />
      </label>
      <label>
        <span>City / region</span>
        <input
          name="city"
          autocomplete="address-level2"
          maxlength="120"
          required
          bind:value={city}
          placeholder="e.g. Helsinki"
        />
      </label>
      <label class="full">
        <span>Typical audience size</span>
        <select name="audienceSize" required bind:value={audienceSize}>
          <option value="" disabled>Select a range</option>
          <option value="under-50">Under 50</option>
          <option value="50-150">50 – 150</option>
          <option value="150-400">150 – 400</option>
          <option value="400-plus">400+</option>
        </select>
      </label>
      <label class="full">
        <span>Anything we should know <em>(optional)</em></span>
        <textarea
          name="note"
          maxlength="1000"
          rows="3"
          bind:value={note}
          placeholder="Venue types, show format, how often you play…"
        ></textarea>
      </label>
    </div>

    {#if status === "error"}
      <p class="error" role="alert">{errorMessage}</p>
    {/if}

    <button type="submit" disabled={!canSubmit}>
      {status === "submitting" ? "Sending…" : "Request exclusive trial access"}
    </button>
    <p class="fine">
      Spots are limited. No password yet — we'll send a magic link when your trial
      opens.
    </p>
  </form>
{/if}

<style>
  .form,
  .success {
    display: grid;
    gap: 1rem;
  }

  .eyebrow {
    margin: 0;
    color: var(--cyan);
    text-transform: uppercase;
    letter-spacing: 0.14em;
    font-weight: 800;
    font-size: 0.72rem;
  }

  .success h3 {
    margin: 0;
    font-size: clamp(1.6rem, 4vw, 2.2rem);
    letter-spacing: -0.03em;
    line-height: 1.05;
  }

  .lede {
    margin: 0;
    color: var(--muted);
    max-width: 34rem;
    line-height: 1.5;
  }

  .lede strong {
    color: var(--ink);
    font-weight: 700;
  }

  .ref {
    margin: 0.25rem 0 0;
    color: var(--muted);
    font-size: 0.9rem;
  }

  .ref code {
    font-family: var(--mono);
    color: var(--cyan);
    letter-spacing: 0.04em;
  }

  .grid {
    display: grid;
    gap: 0.9rem;
    grid-template-columns: 1fr;
  }

  @media (min-width: 640px) {
    .grid {
      grid-template-columns: 1fr 1fr;
    }

    .full {
      grid-column: 1 / -1;
    }
  }

  label {
    display: grid;
    gap: 0.35rem;
  }

  label span {
    color: var(--muted);
    font-size: 0.78rem;
    font-weight: 700;
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  label em {
    font-style: normal;
    font-weight: 500;
    text-transform: none;
    letter-spacing: 0;
    color: color-mix(in srgb, var(--muted) 80%, transparent);
  }

  input,
  select,
  textarea {
    width: 100%;
    border: 1px solid var(--line);
    border-radius: var(--radius);
    background: var(--option);
    color: var(--ink);
    padding: 0.85rem 0.9rem;
    appearance: none;
  }

  input::placeholder,
  textarea::placeholder {
    color: color-mix(in srgb, var(--muted) 70%, transparent);
  }

  input:hover,
  select:hover,
  textarea:hover {
    border-color: color-mix(in srgb, var(--cyan) 35%, var(--line));
  }

  input:focus,
  select:focus,
  textarea:focus {
    border-color: var(--cyan);
    outline: none;
    box-shadow: inset 0 0 0 1px var(--cyan);
  }

  select {
    background-image: linear-gradient(45deg, transparent 50%, var(--cyan) 50%),
      linear-gradient(135deg, var(--cyan) 50%, transparent 50%);
    background-position:
      calc(100% - 18px) 50%,
      calc(100% - 12px) 50%;
    background-size: 6px 6px;
    background-repeat: no-repeat;
    padding-right: 2.2rem;
  }

  textarea {
    resize: vertical;
    min-height: 5.5rem;
  }

  button {
    justify-self: start;
    border: 0;
    border-radius: var(--radius);
    background: var(--cyan);
    color: var(--on-cyan);
    font-weight: 800;
    letter-spacing: 0.01em;
    padding: 0.95rem 1.35rem;
    cursor: pointer;
    transition:
      transform 0.18s ease,
      filter 0.18s ease;
  }

  button:hover:not(:disabled) {
    transform: translateY(-1px);
    filter: brightness(1.05);
  }

  button:active:not(:disabled) {
    transform: translateY(0);
  }

  button:disabled {
    opacity: 0.45;
    cursor: not-allowed;
  }

  .error {
    margin: 0;
    color: var(--error);
    font-size: 0.95rem;
  }

  .fine {
    margin: 0;
    color: var(--muted);
    font-size: 0.85rem;
    max-width: 36rem;
    line-height: 1.45;
  }
</style>
