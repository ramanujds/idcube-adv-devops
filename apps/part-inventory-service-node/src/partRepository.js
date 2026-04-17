class PartRepository {
  constructor(db) {
    this.db = db;
  }

  create(part) {
    this.db
      .prepare(
        "INSERT INTO parts (id, sku, name, price, stock) VALUES (@id, @sku, @name, @price, @stock)"
      )
      .run(part);

    return this.findById(part.id);
  }

  findAll() {
    return this.db.prepare("SELECT id, sku, name, price, stock FROM parts").all();
  }

  findById(id) {
    return this.db
      .prepare("SELECT id, sku, name, price, stock FROM parts WHERE id = ?")
      .get(id);
  }

  findBySku(sku) {
    return this.db
      .prepare("SELECT id, sku, name, price, stock FROM parts WHERE sku = ?")
      .all(sku);
  }

  update(id, partDetails) {
    this.db
      .prepare(
        "UPDATE parts SET sku = @sku, name = @name, price = @price, stock = @stock WHERE id = @id"
      )
      .run({ id, ...partDetails });

    return this.findById(id);
  }

  deleteById(id) {
    const result = this.db.prepare("DELETE FROM parts WHERE id = ?").run(id);
    return result.changes > 0;
  }

  existsById(id) {
    const row = this.db.prepare("SELECT 1 AS present FROM parts WHERE id = ?").get(id);
    return Boolean(row);
  }

  decrementStock(id, quantity) {
    this.db
      .prepare("UPDATE parts SET stock = stock - ? WHERE id = ?")
      .run(quantity, id);

    return this.findById(id);
  }
}

module.exports = PartRepository;

