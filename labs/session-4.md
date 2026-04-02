# SESSION 4 — Rollout Strategies (Blue-Green + Canary)

---

## Session Goal

* Deploy multiple versions of an application
* Control traffic between versions
* Perform safe releases and quick rollbacks

---

# Continuing Case Study

> Your application is now stable with proper probes.
>
> A new version (`v2`) of `order-service` is ready.
>
> After deployment:
>
> * Some users report failures
> * You need a way to test safely and rollback quickly
>
> Your task:
> Implement controlled deployment strategies

---

# LAB 1 — Baseline Rolling Update (Default Behavior)

---

## Setup

Deploy `order-service:v1`

Update deployment to:

```yaml
image: order-service:v2
```

---

## Tasks

* Apply deployment
* Observe rollout

---

## Commands

```bash
kubectl rollout status deployment order-service
kubectl get pods
```

---

## Observe

* Pods replaced gradually
* No control over traffic split

---

---

# LAB 2 — Blue-Green Deployment

---

## Concept Setup

* Two versions running:

    * `order-service-blue` (v1)
    * `order-service-green` (v2)

---

## Step 1 — Deploy Blue (v1)

Deployment label:

```yaml
labels:
  version: blue
```

---

## Step 2 — Deploy Green (v2)

```yaml
labels:
  version: green
```

---

## Step 3 — Service Routing

Service initially points to blue:

```yaml
selector:
  app: order
  version: blue
```

---

## Tasks

* Access application → v1 response
* Switch service selector to green

---

## Observe

* Instant traffic switch

---

---

# LAB 3 — Simulate Failure in Green

---

## Inject Issue

* Break `/orders` endpoint in v2

---

## Tasks

* Switch traffic to green
* Observe failures

---

## Rollback

```yaml
selector:
  version: blue
```

---

## Observe

* Traffic immediately restored

---

---

# LAB 4 — Canary Deployment (Manual)

---

## Setup

* Scale deployments:

```bash
kubectl scale deployment order-blue --replicas=3
kubectl scale deployment order-green --replicas=1
```

---

## Service selector

```yaml
selector:
  app: order
```

(both versions match)

---

## Tasks

* Send multiple requests
* Observe responses (v1 vs v2)

---

## Observe

* ~75% v1, ~25% v2

---

---

# LAB 5 — Controlled Canary Testing

---

## Tasks

* Gradually increase green replicas

```bash
kubectl scale deployment order-green --replicas=2
```

---

## Observe

* Traffic distribution changes

---

---

# LAB 6 — Canary Failure Scenario

---

## Inject Issue

* Add delay or error in v2

---

## Tasks

* Monitor responses
* Reduce green replicas to 0

---

## Observe

* Traffic shifts back to stable version

---

---

# LAB 7 — Rollout History & Rollback

---

## Commands

```bash
kubectl rollout history deployment order-service
kubectl rollout undo deployment order-service
```

---

## Tasks

* Deploy bad version
* Rollback using command

---

---

# Real Case Scenario

---

## Issue

* v2 deployed with bug
* All users impacted

---

## Investigation

* Identify version causing issue
* Check rollout history

---

## Resolution Options

* Blue-Green → instant switch
* Canary → reduce exposure
* Rollback → revert deployment

---

---

# Validation Tasks

---

* Verify both versions running simultaneously
* Confirm traffic switching works
* Test rollback scenarios

---

---

# Final Architecture After Session

```id="m5v9hc"
Client → order-service (v1 / v2 controlled rollout) → inventory-service
```

