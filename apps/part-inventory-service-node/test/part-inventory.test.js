const test = require("node:test");
const assert = require("node:assert/strict");
const request = require("supertest");
const { createApp } = require("../src/app");

test("GET /api/parts returns seeded data", async () => {
  const app = createApp();
  const response = await request(app).get("/api/parts");

  assert.equal(response.status, 200);
  assert.equal(response.body.length, 10);
  assert.equal(response.body[0].sku, "SEAT-SAFE45-001");

  app.locals.db.close();
});

test("POST /api/parts creates a part with UUID", async () => {
  const app = createApp();
  const payload = { sku: "ABC-123", name: "Demo", price: 12.5, stock: 8 };

  const response = await request(app).post("/api/parts").send(payload);

  assert.equal(response.status, 200);
  assert.match(response.body.id, /^[0-9a-f-]{36}$/i);
  assert.equal(response.body.sku, payload.sku);

  app.locals.db.close();
});

test("POST /api/parts/place-order decreases stock", async () => {
  const app = createApp();

  const response = await request(app)
    .post("/api/parts/place-order")
    .send({ sku: "SEAT-SAFE45-001", quantity: 2 });

  assert.equal(response.status, 200);
  assert.equal(response.body.status, "Order placed successfully");
  assert.equal(response.body.quantity, 2);
  assert.equal(response.body.totalPrice, 59.98);

  const partAfter = await request(app).get("/api/parts/sku/SEAT-SAFE45-001");
  assert.equal(partAfter.body[0].stock, 98);

  app.locals.db.close();
});

test("POST /api/parts/place-order returns 400 for unknown SKU", async () => {
  const app = createApp();

  const response = await request(app)
    .post("/api/parts/place-order")
    .send({ sku: "DOES-NOT-EXIST", quantity: 2 });

  assert.equal(response.status, 400);
  assert.equal(response.body.status, "Part not found");

  app.locals.db.close();
});

test("DELETE /api/parts/:id returns 404 for unknown id", async () => {
  const app = createApp();
  const response = await request(app).delete("/api/parts/missing-id");

  assert.equal(response.status, 404);

  app.locals.db.close();
});

