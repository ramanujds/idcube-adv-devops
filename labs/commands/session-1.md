## Docker Build Command

```bash

docker buildx build --platform linux/amd64,linux/arm64 \
  -t ram1uj/part-order-service:v3 \
  -f Dockerfile .

```