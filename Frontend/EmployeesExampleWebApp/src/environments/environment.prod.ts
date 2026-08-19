import { AppEnvironment } from './environment.model';

export const environment: AppEnvironment = {
  production: true,

  // Set to the deployed API origin before building for production.
  // Verify with: grep -r "localhost" dist/ -- it must return nothing.
  apiBaseUrl: 'https://REPLACE-ME.onrender.com'
};
