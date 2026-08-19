import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    // Zone-based change detection, explicitly. Angular 22 no longer turns it on
    // implicitly, and the app updates its views from plain subscribe callbacks
    // rather than signals, so without this nothing re-renders.
    provideZoneChangeDetection(),
    provideRouter(routes),
    provideHttpClient(),
  ],
};
