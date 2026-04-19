# part-inventory-service-dotnet

ASP.NET Core MVC replication of `apps/part-inventory-service-node`.

## What it includes

- REST API compatible with the existing inventory contract (`/api/parts/**`)
- Server-rendered inventory pages (`/`, `/inventory`, part CRUD forms)
- In-memory SQLite database seeded from:
  - `PartInventoryService.DotNet/resources/schema.sql`
  - `PartInventoryService.DotNet/resources/data.sql`
- xUnit integration tests covering the main API and HTML flows

## Project layout

- `PartInventoryService.DotNet/` - MVC app
- `tests/PartInventoryService.DotNet.Tests/` - integration tests
- `Dockerfile` - container image for the .NET service

## Run locally

If `dotnet` is already on your `PATH`:

```bash
cd "/Users/ramanuj/Documents/training-projects/adv-devops-kubernetes/apps/part-inventory-service-dotnet"
dotnet run --project PartInventoryService.DotNet/PartInventoryService.DotNet.csproj
```

If your shell cannot find `dotnet`, use the absolute SDK path:

```bash
cd "/Users/ramanuj/Documents/training-projects/adv-devops-kubernetes/apps/part-inventory-service-dotnet"
/usr/local/share/dotnet/dotnet run --project PartInventoryService.DotNet/PartInventoryService.DotNet.csproj
```

By default, `dotnet run` uses the generated launch profile and serves the app on `http://localhost:5044`.

If you want a specific port, set `PORT` explicitly:

```bash
cd "/Users/ramanuj/Documents/training-projects/adv-devops-kubernetes/apps/part-inventory-service-dotnet"
PORT=8080 /usr/local/share/dotnet/dotnet run --project PartInventoryService.DotNet/PartInventoryService.DotNet.csproj
```

## Run tests

```bash
cd "/Users/ramanuj/Documents/training-projects/adv-devops-kubernetes/apps/part-inventory-service-dotnet"
/usr/local/share/dotnet/dotnet test PartInventoryServiceDotNet.slnx
```

## Build and run with Docker

```bash
cd "/Users/ramanuj/Documents/training-projects/adv-devops-kubernetes/apps/part-inventory-service-dotnet"
docker build -t part-inventory-service:dotnet .
docker run --rm -p 8083:8080 part-inventory-service:dotnet
```

The container serves the app on `http://localhost:8083`.

## API parity

- `POST /api/parts`
- `GET /api/parts`
- `GET /api/parts/:id`
- `GET /api/parts/sku/:sku`
- `PUT /api/parts/:id`
- `DELETE /api/parts/:id`
- `POST /api/parts/place-order`

