// See https://kit.svelte.dev/docs/types#app for information about these interfaces
// and what to do when importing types
declare global {
  namespace App {
    // interface Error {}
    // interface Locals {}
    // interface PageData {}
    // interface PageState {}
    // interface Platform {}
  }

  /** Injected by Vite define from PUBLIC_API_BASE at build time. */
  const __PUBLIC_API_BASE__: string;
}

export {};
