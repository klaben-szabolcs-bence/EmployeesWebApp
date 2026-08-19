/**
 * Shared shape for the environment files.
 *
 * angular.json swaps environment.ts for environment.prod.ts at build time, so
 * TypeScript only ever type-checks whichever one is active. Typing both against
 * this interface is what makes a key added to one and forgotten in the other a
 * compile error rather than a production-only surprise.
 */
export interface AppEnvironment {
  production: boolean;

  /** Origin of the Web API, no trailing slash. */
  apiBaseUrl: string;
}
