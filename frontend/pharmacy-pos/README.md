# PharmaPOS Web (Angular)

The Angular 22 SPA for PharmaPOS. For setup, running the full stack, demo identities, tests and
conventions, see the [root README](../../README.md) and the
[development guide](../../docs/development.md).

Quick reference:

```bash
npm install
npm start          # http://localhost:4200
npm run build
npm test -- --watch=false
```

Runtime configuration is loaded from `/config.json` (see `public/config.json`), so one build can be
deployed against different API and Keycloak endpoints without rebuilding.
