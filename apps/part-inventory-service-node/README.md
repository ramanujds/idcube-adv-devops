# part-inventory-service-node

Node.js + Express replication of `apps/part-inventory-service`.

## What it includes

- REST API compatible with the Spring Boot version (`/api/parts/**`)
- Server-rendered inventory pages (`/`, `/inventory`, part CRUD forms)
- In-memory SQL database using `better-sqlite3` (`:memory:`), seeded from:
  - `resources/schema.sql`
  - `resources/data.sql`

## Run locally

```bash
npm install
npm start
```

Server runs on `http://localhost:8080` by default.

## Run tests

```bash
npm test
```

## API parity

- `POST /api/parts`
- `GET /api/parts`
- `GET /api/parts/:id`
- `GET /api/parts/sku/:sku`
- `PUT /api/parts/:id`
- `DELETE /api/parts/:id`
- `POST /api/parts/place-order`

