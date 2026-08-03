<script lang="ts">
  let { children } = $props();
</script>

<svelte:head>
  <meta name="theme-color" content="#00FFF5" />
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous" />
  <link
    href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@400;500;600;700;800&family=IBM+Plex+Mono:wght@500;600&display=swap"
    rel="stylesheet"
  />
</svelte:head>

{@render children()}

<style>
  :global(:root) {
    color-scheme: dark;
    --cyan: #00fff5;
    --on-cyan: #001413;
    --bg: #05090b;
    --panel: #091115;
    --option: #0c181d;
    --line: #17363a;
    --muted: #91a5aa;
    --ink: #ecffff;
    --success: #5ec99d;
    --warn: #ffa040;
    --error: #ff6b93;
    --radius: 0;
    --font: "IBM Plex Sans", "Segoe UI", system-ui, sans-serif;
    --mono: "IBM Plex Mono", ui-monospace, monospace;
  }

  :global(*),
  :global(*::before),
  :global(*::after) {
    box-sizing: border-box;
  }

  :global(html),
  :global(body) {
    margin: 0;
    min-height: 100%;
    background: var(--bg);
    color: var(--ink);
    font-family: var(--font);
    -webkit-font-smoothing: antialiased;
    text-rendering: optimizeLegibility;
  }

  :global(a) {
    color: var(--cyan);
  }

  :global(button),
  :global(input),
  :global(select),
  :global(textarea) {
    font: inherit;
  }

  :global(:focus-visible) {
    outline: 2px solid var(--cyan);
    outline-offset: 2px;
  }

  @media (prefers-reduced-motion: reduce) {
    :global(*),
    :global(*::before),
    :global(*::after) {
      animation-duration: 0.01ms !important;
      animation-iteration-count: 1 !important;
      transition-duration: 0.01ms !important;
    }
  }
</style>
