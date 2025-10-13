import { sveltekit } from '@sveltejs/kit/vite';
import type { UserConfig } from 'vite';

// PUBLIC_API_BASE will be embedded into the static build for client-side fetches
const config: UserConfig = {
  plugins: [sveltekit()],
  define: {
    __PUBLIC_API_BASE__: JSON.stringify(process.env.PUBLIC_API_BASE || '')
  }
};

export default config;
