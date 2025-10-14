import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig(({ mode }) => {
  const base = process.env.BASE_PATH || '';
  return {
    base,
    plugins: [sveltekit()],
    build: {
      outDir: 'build'
    }
  };
});
