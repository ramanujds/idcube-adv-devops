## Application 1 — E-Commerce API

* Name: **`order-service`**
* Stack: Spring Boot
* Features:

    * Create order
    * Get order
* Endpoint:

    * `/orders`
    * `/health`

## Application 2 — Inventory Service

* Name: **`inventory-service`**
* Stack: Node.js / Express/ Spring Boot
* Features:

    * Check stock
    * Update stock
* Endpoint:

    * `/inventory`
    * `/health`

---

## Architecture (Baseline)

```
Client → order-service → inventory-service
```

* order-service calls inventory-service to check stock before creating order

---

# SESSION 1 — Docker Optimization + Image Strategy 

---

## Session Goal

Make learners:

* Build **production-grade Docker images**
* Understand **why bad images break production**
* Learn **image strategy used in real companies**

---

# Case Study

### Scenario: Production Incident

> Your team deployed `order-service` and `inventory-service` to production.
>
> Problems observed:
>
> * Images are **900MB+**
> * Deployments are **slow (5–8 mins)**
> * Frequent **ImagePullBackOff**
> * Security team flagged **vulnerabilities**
>
> Your job:
> Fix performance, reliability, and security.

---

# LAB 1 — Build Naive Images 

## Setup

Provide:

* Spring Boot jar (order-service)
* Node app (inventory-service)

---

## Bad Dockerfile 

```dockerfile
FROM openjdk:17
COPY . .
RUN mvn clean package
CMD ["java", "-jar", "target/app.jar"]
```

---

## Tasks

1. Build image
2. Check size
3. Run container

---

## Expected Observations

* Huge image (~800MB–1GB)
* Slow build
* Includes unnecessary files

---


# LAB 2 — Optimize Using Multi-Stage Build

## Improved Dockerfile

```dockerfile
FROM maven:3.9 AS builder
WORKDIR /app
COPY . .
RUN mvn clean package -DskipTests

FROM openjdk:17-jdk-slim
WORKDIR /app
COPY --from=builder /app/target/app.jar app.jar
CMD ["java", "-jar", "app.jar"]
```

---

## Tasks

* Rebuild image
* Compare size
* Compare build time

---

## Expected Outcome

* Image size reduced drastically (~70%+)
* Faster build

---


# LAB 3 — `.dockerignore` Impact

## Add

```
.git
node_modules
target/
```

---

## Tasks

* Build again
* Observe context size reduction

---


---

# LAB 4 — Tagging Strategy

## Problem

Developers are using:

```
order-service:latest
```

---

## Tasks

1. Tag images:

```
order-service:v1
order-service:v2
order-service:build-101
```

2. Simulate rollback

---

## Case Scenario

> v2 has a bug → rollback needed

---



# LAB 5 — Simulate ImagePullBackOff

## Break Scenario

* Use wrong tag in Kubernetes deployment:

```
order-service:v99
```

---

## Tasks

1. Deploy to cluster
2. Observe failure
3. Debug using:

```
kubectl describe pod
```

---

## Expected Output

* Image not found error
* Backoff retries

---

## Fix

* Correct tag
* Redeploy

---


# LAB 6 — Security & Base Image Optimization

## Compare

| Base Image | Size   | Security             |
| ---------- | ------ | -------------------- |
| openjdk    | large  | more vulnerabilities |
| slim       | medium | better               |
| distroless | small  | best                 |

---

## Task

* Switch to `openjdk:17-jdk-slim`
* Discuss distroless


---

# LAB 7 — BuildKit (Optional Advanced)

## Enable

```
DOCKER_BUILDKIT=1 docker build .
```

---

## Show

* Parallel builds
* Faster execution

---

# Final Case Study Resolution

## Before

* 900MB images
* Slow deploy
* Frequent failures

## After

* ~150–200MB images
* Fast deploy
* Stable pull
* Secure base images

