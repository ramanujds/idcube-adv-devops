const fs = require("node:fs");
const path = require("node:path");
const Database = require("better-sqlite3");

function initDb() {
  const db = new Database(":memory:");
  const schemaPath = path.join(__dirname, "..", "resources", "schema.sql");
  const dataPath = path.join(__dirname, "..", "resources", "data.sql");

  db.exec(fs.readFileSync(schemaPath, "utf8"));
  db.exec(fs.readFileSync(dataPath, "utf8"));

  return db;
}

module.exports = {
  initDb
};

