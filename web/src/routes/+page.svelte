<script lang="ts">
  import TrialForm from "$lib/TrialForm.svelte";

  const features = [
    {
      title: "Run the room",
      body: "Lobby to reveal in one Performer flow — hints, guessing windows, and scoring stay in sync.",
    },
    {
      title: "Venue stays local",
      body: "Audio plays on your machine. The cloud never streams your tracks — only the show state.",
    },
    {
      title: "Audience on every phone",
      body: "Guests join with a short session code. No app install. Live tallies on the big screen.",
    },
  ];

  const steps = [
    { code: "01", label: "Request access", detail: "Tell us about your band and typical rooms." },
    { code: "02", label: "Get a magic link", detail: "We open your trial Workspace when a spot frees." },
    { code: "03", label: "Run a dry show", detail: "Prep a setlist, pair venue gear, put phones in the room." },
  ];

  function scrollToTrial() {
    document.getElementById("trial")?.scrollIntoView({ behavior: "smooth", block: "start" });
  }
</script>

<svelte:head>
  <title>Nuotti — Exclusive trial for event bands</title>
  <meta
    name="description"
    content="Nuotti is the live music-guessing show for event bands. Request exclusive trial access to run interactive nights with your audience."
  />
</svelte:head>

<div class="page">
  <header class="top">
    <a class="brand" href="/">Nuotti</a>
    <nav>
      <a href="#features">Features</a>
      <a href="#how">How it works</a>
      <a class="nav-cta" href="#trial">Request trial</a>
    </nav>
  </header>

  <section class="hero">
    <div class="hero-copy">
      <p class="eyebrow">Exclusive trial · Event bands</p>
      <h1>Nuotti</h1>
      <p class="tagline">Live music quizzes your audience actually plays.</p>
      <p class="lede">
        Progressive hints, timed guesses, and a venue display — built for bands that
        already own the room. Limited trial spots for acts ready to run a real show.
      </p>
      <div class="cta-row">
        <button type="button" class="primary" onclick={scrollToTrial}>
          Request exclusive trial access
        </button>
        <a class="ghost" href="#how">See the flow</a>
      </div>
    </div>

    <aside class="stage" aria-hidden="true">
      <div class="stage-frame">
        <div class="stage-meta">
          <span class="pulse"></span>
          <span>LIVE SESSION</span>
          <code>N7K-482</code>
        </div>
        <p class="stage-prompt">Guess the song</p>
        <div class="hint-rail">
          <span class="hint on">Hint 1 · Brass intro</span>
          <span class="hint on">Hint 2 · Key of F</span>
          <span class="hint">Hint 3 · Locked</span>
        </div>
        <div class="bars">
          {#each [48, 72, 36, 90, 58, 80, 44, 66] as h, i (i)}
            <span style:--h="{h}%"></span>
          {/each}
        </div>
        <div class="score-strip">
          <span>Audience locked in</span>
          <strong>214</strong>
        </div>
      </div>
    </aside>
  </section>

  <section id="features" class="features">
    <p class="eyebrow">What you test</p>
    <h2>Everything you need for one night</h2>
    <ul>
      {#each features as feature (feature.title)}
        <li>
          <h3>{feature.title}</h3>
          <p>{feature.body}</p>
        </li>
      {/each}
    </ul>
  </section>

  <section id="how" class="how">
    <p class="eyebrow">Trial path</p>
    <h2>From request to first show</h2>
    <ol>
      {#each steps as step (step.code)}
        <li>
          <span class="code">{step.code}</span>
          <div>
            <h3>{step.label}</h3>
            <p>{step.detail}</p>
          </div>
        </li>
      {/each}
    </ol>
  </section>

  <section id="trial" class="trial">
    <div class="trial-copy">
      <p class="eyebrow">Apply</p>
      <h2>Request exclusive trial access</h2>
      <p class="lede">
        Spots are invite-only while we harden the product with real event bands.
        Tell us who you are — we'll follow up with a magic-link when your trial opens.
      </p>
    </div>
    <div class="trial-panel">
      <TrialForm />
    </div>
  </section>

  <footer>
    <span class="brand">Nuotti</span>
    <span class="muted">Live music quiz for event bands</span>
  </footer>
</div>

<style>
  .page {
    min-height: 100vh;
    border-left: 4px solid var(--cyan);
    background:
      radial-gradient(1200px 600px at 85% -10%, rgba(0, 255, 245, 0.09), transparent 55%),
      linear-gradient(180deg, rgba(0, 255, 245, 0.05), transparent 28%),
      var(--bg);
  }

  .top {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    padding: 1.25rem clamp(1.25rem, 4vw, 3.5rem);
  }

  .brand {
    color: var(--cyan);
    text-decoration: none;
    font-weight: 800;
    letter-spacing: 0.14em;
    text-transform: uppercase;
    font-size: 0.78rem;
  }

  nav {
    display: flex;
    align-items: center;
    gap: 1rem;
    flex-wrap: wrap;
    justify-content: flex-end;
  }

  nav a {
    color: var(--muted);
    text-decoration: none;
    font-size: 0.9rem;
    font-weight: 600;
  }

  nav a:hover {
    color: var(--ink);
  }

  .nav-cta {
    color: var(--on-cyan) !important;
    background: var(--cyan);
    padding: 0.55rem 0.85rem;
    font-weight: 800 !important;
  }

  .hero {
    display: grid;
    gap: 2.5rem;
    align-items: center;
    padding: clamp(2rem, 6vw, 4.5rem) clamp(1.25rem, 4vw, 3.5rem) 3.5rem;
  }

  @media (min-width: 960px) {
    .hero {
      grid-template-columns: minmax(0, 1.05fr) minmax(0, 0.95fr);
      gap: 3rem;
      min-height: calc(100vh - 5rem);
      padding-top: 1.5rem;
      padding-bottom: 4rem;
    }
  }

  .eyebrow {
    margin: 0 0 0.75rem;
    color: var(--cyan);
    text-transform: uppercase;
    letter-spacing: 0.14em;
    font-weight: 800;
    font-size: 0.72rem;
  }

  h1 {
    margin: 0;
    font-size: clamp(3.4rem, 12vw, 6.5rem);
    line-height: 0.9;
    letter-spacing: -0.04em;
    font-weight: 800;
  }

  .tagline {
    margin: 1rem 0 0;
    font-size: clamp(1.25rem, 3vw, 1.75rem);
    letter-spacing: -0.02em;
    font-weight: 700;
    max-width: 18ch;
    line-height: 1.15;
  }

  .lede {
    margin: 1rem 0 0;
    color: var(--muted);
    font-size: 1.05rem;
    line-height: 1.55;
    max-width: 34rem;
  }

  .cta-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.85rem;
    margin-top: 1.75rem;
    align-items: center;
  }

  .primary {
    border: 0;
    border-radius: var(--radius);
    background: var(--cyan);
    color: var(--on-cyan);
    font-weight: 800;
    padding: 0.95rem 1.25rem;
    cursor: pointer;
    transition:
      transform 0.18s ease,
      filter 0.18s ease;
  }

  .primary:hover {
    transform: translateY(-1px);
    filter: brightness(1.05);
  }

  .ghost {
    color: var(--ink);
    text-decoration: none;
    font-weight: 700;
    border-bottom: 1px solid var(--line);
    padding-bottom: 0.15rem;
  }

  .ghost:hover {
    border-color: var(--cyan);
    color: var(--cyan);
  }

  .stage {
    justify-self: stretch;
  }

  .stage-frame {
    border: 1px solid var(--line);
    background:
      linear-gradient(180deg, rgba(0, 255, 245, 0.08), transparent 40%),
      var(--panel);
    padding: 1.25rem 1.25rem 1.35rem;
    display: grid;
    gap: 1rem;
    animation: rise 0.9s ease both;
  }

  .stage-meta {
    display: flex;
    align-items: center;
    gap: 0.65rem;
    color: var(--muted);
    font-size: 0.72rem;
    font-weight: 700;
    letter-spacing: 0.12em;
    text-transform: uppercase;
  }

  .stage-meta code {
    margin-left: auto;
    font-family: var(--mono);
    color: var(--cyan);
    letter-spacing: 0.08em;
    font-size: 0.85rem;
  }

  .pulse {
    width: 0.55rem;
    height: 0.55rem;
    background: var(--cyan);
    box-shadow: 0 0 0 0 rgba(0, 255, 245, 0.55);
    animation: ping 1.8s ease-out infinite;
  }

  .stage-prompt {
    margin: 0;
    font-size: clamp(1.6rem, 4vw, 2.3rem);
    font-weight: 800;
    letter-spacing: -0.03em;
    line-height: 1;
  }

  .hint-rail {
    display: grid;
    gap: 0.45rem;
  }

  .hint {
    border: 1px solid var(--line);
    background: var(--option);
    color: var(--muted);
    padding: 0.65rem 0.75rem;
    font-size: 0.9rem;
    font-weight: 600;
  }

  .hint.on {
    border-color: color-mix(in srgb, var(--cyan) 45%, var(--line));
    color: var(--ink);
    box-shadow: inset 3px 0 0 var(--cyan);
  }

  .bars {
    display: grid;
    grid-template-columns: repeat(8, 1fr);
    gap: 0.35rem;
    align-items: end;
    height: 4.5rem;
    margin-top: 0.25rem;
  }

  .bars span {
    display: block;
    height: var(--h);
    background: linear-gradient(180deg, var(--cyan), rgba(0, 255, 245, 0.2));
    transform-origin: bottom;
    animation: bounce 1.6s ease-in-out infinite;
  }

  .bars span:nth-child(2) {
    animation-delay: 0.1s;
  }
  .bars span:nth-child(3) {
    animation-delay: 0.18s;
  }
  .bars span:nth-child(4) {
    animation-delay: 0.05s;
  }
  .bars span:nth-child(5) {
    animation-delay: 0.22s;
  }
  .bars span:nth-child(6) {
    animation-delay: 0.12s;
  }
  .bars span:nth-child(7) {
    animation-delay: 0.28s;
  }
  .bars span:nth-child(8) {
    animation-delay: 0.08s;
  }

  .score-strip {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    border-top: 1px solid var(--line);
    padding-top: 0.85rem;
    color: var(--muted);
    font-size: 0.85rem;
    font-weight: 600;
  }

  .score-strip strong {
    color: var(--cyan);
    font-family: var(--mono);
    font-size: 1.35rem;
    letter-spacing: 0.04em;
  }

  .features,
  .how,
  .trial {
    padding: 3.5rem clamp(1.25rem, 4vw, 3.5rem);
  }

  .features h2,
  .how h2,
  .trial h2 {
    margin: 0;
    font-size: clamp(1.8rem, 4vw, 2.6rem);
    letter-spacing: -0.03em;
    line-height: 1.05;
    max-width: 16ch;
  }

  .features ul {
    list-style: none;
    margin: 2rem 0 0;
    padding: 0;
    display: grid;
    gap: 1px;
    background: var(--line);
    border: 1px solid var(--line);
  }

  @media (min-width: 800px) {
    .features ul {
      grid-template-columns: repeat(3, 1fr);
    }
  }

  .features li {
    background: var(--panel);
    padding: 1.35rem 1.25rem 1.5rem;
  }

  .features h3 {
    margin: 0 0 0.55rem;
    font-size: 1.1rem;
    letter-spacing: -0.02em;
  }

  .features p {
    margin: 0;
    color: var(--muted);
    line-height: 1.5;
    font-size: 0.98rem;
  }

  .how ol {
    list-style: none;
    margin: 2rem 0 0;
    padding: 0;
    display: grid;
    gap: 1rem;
  }

  @media (min-width: 800px) {
    .how ol {
      grid-template-columns: repeat(3, 1fr);
      gap: 1.25rem;
    }
  }

  .how li {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 0.9rem;
    border-top: 1px solid var(--line);
    padding-top: 1rem;
  }

  .code {
    font-family: var(--mono);
    color: var(--cyan);
    font-weight: 600;
    font-size: 0.9rem;
    letter-spacing: 0.06em;
  }

  .how h3 {
    margin: 0 0 0.35rem;
    font-size: 1.05rem;
  }

  .how p {
    margin: 0;
    color: var(--muted);
    line-height: 1.45;
    font-size: 0.95rem;
  }

  .trial {
    display: grid;
    gap: 1.75rem;
    padding-bottom: 4.5rem;
  }

  @media (min-width: 960px) {
    .trial {
      grid-template-columns: minmax(0, 0.85fr) minmax(0, 1.15fr);
      gap: 2.5rem;
      align-items: start;
    }
  }

  .trial-panel {
    border: 1px solid var(--line);
    background: var(--panel);
    padding: 1.35rem 1.25rem 1.5rem;
    box-shadow: inset 0 3px 0 var(--cyan);
    animation: rise 0.7s ease both;
  }

  footer {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem 1.25rem;
    align-items: center;
    justify-content: space-between;
    padding: 1.5rem clamp(1.25rem, 4vw, 3.5rem) 2rem;
    border-top: 1px solid var(--line);
  }

  .muted {
    color: var(--muted);
    font-size: 0.9rem;
  }

  @keyframes ping {
    0% {
      box-shadow: 0 0 0 0 rgba(0, 255, 245, 0.55);
    }
    70% {
      box-shadow: 0 0 0 10px rgba(0, 255, 245, 0);
    }
    100% {
      box-shadow: 0 0 0 0 rgba(0, 255, 245, 0);
    }
  }

  @keyframes bounce {
    0%,
    100% {
      transform: scaleY(0.72);
      opacity: 0.7;
    }
    50% {
      transform: scaleY(1);
      opacity: 1;
    }
  }

  @keyframes rise {
    from {
      opacity: 0;
      transform: translateY(12px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  @media (max-width: 720px) {
    nav a:not(.nav-cta) {
      display: none;
    }
  }
</style>
