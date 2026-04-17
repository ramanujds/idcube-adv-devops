const { createApp } = require("./app");

const app = createApp();
const port = Number(process.env.PORT || 8080);

app.listen(port, () => {
  console.log(`part-inventory-service-node running on port ${port}`);
});

