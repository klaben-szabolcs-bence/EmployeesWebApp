import { AppEnvironment } from './environment.model';

export const environment: AppEnvironment = {
  production: true,

  // Our own subdomain, not the host's URL. The API can move between hosts by
  // repointing the CNAME, and the client does not need a rebuild for that.
  // Verify with: grep -r "localhost" dist/ -- it must return nothing.
  apiBaseUrl: 'https://employees-api.klaben.hu'
};
