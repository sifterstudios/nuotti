<script>
  let { data: _data } = $props();
</script>

<svelte:head>
  <title>Nuotti</title>
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <meta name="theme-color" content="#00FFF5" />
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous" />
  <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;700;800&display=swap" rel="stylesheet" />
</svelte:head>

<main>
  <p class="eyebrow">Nuotti</p>
  <h1>Live music quiz</h1>
  <p class="lede">Static site is deployed and running.</p>
</main>

<style>
  :root {
    color-scheme: dark;
    --cyan: #00fff5;
    --bg: #05090b;
    --panel: #091115;
    --line: #17363a;
    --muted: #91a5aa;
    --ink: #ecffff;
  }

  :global(html),
  :global(body) {
    margin: 0;
    min-height: 100%;
    background: var(--bg);
    color: var(--ink);
  }

  main {
    min-height: 100vh;
    display: grid;
    align-content: center;
    gap: 0.75rem;
    padding: clamp(2rem, 6vw, 4rem);
    background:
      linear-gradient(180deg, rgba(0, 255, 245, 0.06), transparent 42%),
      var(--bg);
    border-left: 4px solid var(--cyan);
    font-family: "IBM Plex Sans", "Segoe UI", system-ui, sans-serif;
  }

  .eyebrow {
    margin: 0;
    color: var(--cyan);
    text-transform: uppercase;
    letter-spacing: 0.14em;
    font-weight: 800;
    font-size: 0.75rem;
  }

  h1 {
    margin: 0;
    font-size: clamp(2.4rem, 7vw, 4.5rem);
    line-height: 0.95;
    letter-spacing: -0.03em;
  }

  .lede {
    margin: 0;
    max-width: 28rem;
    color: var(--muted);
    font-size: 1.05rem;
  }
</style>
