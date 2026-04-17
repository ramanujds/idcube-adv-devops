const express = require("express");
const path = require("node:path");
const { v4: uuidv4 } = require("uuid");
const { initDb } = require("./db");
const PartRepository = require("./partRepository");

function createApp() {
  const db = initDb();
  const partRepository = new PartRepository(db);

  const app = express();
  app.use(express.json());
  app.use(express.urlencoded({ extended: true }));

  app.set("view engine", "ejs");
  app.set("views", path.join(__dirname, "views"));

  app.post("/api/parts", (req, res) => {
    const part = {
      id: uuidv4(),
      sku: req.body.sku,
      name: req.body.name,
      price: Number(req.body.price),
      stock: Number(req.body.stock)
    };

    const savedPart = partRepository.create(part);
    return res.status(200).json(savedPart);
  });

  app.get("/api/parts", (_req, res) => {
    return res.json(partRepository.findAll());
  });

  app.get("/api/parts/:id", (req, res) => {
    const part = partRepository.findById(req.params.id);
    if (!part) {
      return res.status(404).send();
    }

    return res.json(part);
  });

  app.get("/api/parts/sku/:sku", (req, res) => {
    return res.json(partRepository.findBySku(req.params.sku));
  });

  app.put("/api/parts/:id", (req, res) => {
    const existing = partRepository.findById(req.params.id);
    if (!existing) {
      return res.status(404).send();
    }

    const updatedPart = partRepository.update(req.params.id, {
      sku: req.body.sku,
      name: req.body.name,
      price: Number(req.body.price),
      stock: Number(req.body.stock)
    });

    return res.json(updatedPart);
  });

  app.delete("/api/parts/:id", (req, res) => {
    const deleted = partRepository.deleteById(req.params.id);
    if (!deleted) {
      return res.status(404).send();
    }

    return res.status(204).send();
  });

  app.post("/api/parts/place-order", (req, res) => {
    const { sku, quantity } = req.body;
    const parts = partRepository.findBySku(sku);

    if (!parts.length) {
      return res.status(400).json({
        partSku: "",
        status: "Part not found",
        quantity: 0,
        totalPrice: 0
      });
    }

    const part = parts[0];
    const orderQty = Number(quantity);

    if (part.stock < orderQty) {
      return res.status(400).json({
        partSku: "",
        status: "Insufficient stock",
        quantity: 0,
        totalPrice: 0
      });
    }

    partRepository.decrementStock(part.id, orderQty);

    return res.json({
      partSku: part.sku,
      status: "Order placed successfully",
      quantity: orderQty,
      totalPrice: Number(part.price) * orderQty
    });
  });

  app.get(["/", "/inventory"], (_req, res) => {
    return res.render("index", { parts: partRepository.findAll() });
  });

  app.get("/inventory-update", (req, res) => {
    return res.render("inventory-update", {
      type: req.query.type || "success",
      message: req.query.message || "Operation completed.",
      nextUrl: req.query.nextUrl || "/inventory",
      nextLabel: req.query.nextLabel || "Back to Inventory"
    });
  });

  app.post("/parts", (req, res) => {
    partRepository.create({
      id: uuidv4(),
      sku: req.body.sku,
      name: req.body.name,
      price: Number(req.body.price),
      stock: Number(req.body.stock)
    });

    return res.render("inventory-update", {
      type: "success",
      message: "Part added successfully.",
      nextUrl: "/inventory",
      nextLabel: "Back to Inventory"
    });
  });

  app.get("/parts/:id/edit", (req, res) => {
    const part = partRepository.findById(req.params.id);

    if (!part) {
      return res.render("inventory-update", {
        type: "error",
        message: "Part not found.",
        nextUrl: "/inventory",
        nextLabel: "Back to Inventory"
      });
    }

    return res.render("edit-part", { part });
  });

  app.post("/parts/:id", (req, res) => {
    partRepository.update(req.params.id, {
      sku: req.body.sku,
      name: req.body.name,
      price: Number(req.body.price),
      stock: Number(req.body.stock)
    });

    return res.render("inventory-update", {
      type: "success",
      message: "Part updated successfully.",
      nextUrl: "/inventory",
      nextLabel: "Back to Inventory"
    });
  });

  app.post("/parts/:id/delete", (req, res) => {
    partRepository.deleteById(req.params.id);

    return res.render("inventory-update", {
      type: "success",
      message: "Part deleted successfully.",
      nextUrl: "/inventory",
      nextLabel: "Back to Inventory"
    });
  });

  app.locals.db = db;
  app.locals.partRepository = partRepository;

  return app;
}

module.exports = { createApp };

