import adapter from '@sveltejs/adapter-static';

/** @type {import('@sveltejs/kit').Config} */
const config = {
  kit: {
    adapter: adapter(),
    paths: {
      // Allow setting BASE_PATH at build time if needed
      base: process.env.BASE_PATH || ''
    }
  }
};

export default config;
