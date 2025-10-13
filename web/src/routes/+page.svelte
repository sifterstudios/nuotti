<script>
  // This constant is embedded at build time from vite.config.ts
  const apiBase = (typeof __PUBLIC_API_BASE__ !== 'undefined' && __PUBLIC_API_BASE__) || '';
</script>

<svelte:head>
  <title>Nuotti Web</title>
</svelte:head>

<main style="max-width: 720px; margin: 2rem auto; font-family: system-ui, sans-serif;">
  <h1>Nuotti Web</h1>
  <p>This is a minimal SvelteKit stub intended to deploy alongside Backend and Audience.</p>
  <p>
    API base URL (build-time): <code>{apiBase || '(not set)'}</code>
  </p>
  <p>You can set PUBLIC_API_BASE at build time to point this site at your API.</p>
</main>
