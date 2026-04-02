# SESSION 6 — Autoscaling (HPA + Cluster Autoscaler)

---

## Session Goal

* Configure **Horizontal Pod Autoscaler (HPA)**
* Understand **Cluster Autoscaler (CA)** behavior
* Handle traffic spikes automatically

---

# Continuing Case Study

> Your services are now properly scheduled.
>
> New situation:
>
> * Traffic spikes during peak hours
> * Response time increases
> * Pods are overloaded
>
> Your task:
> Enable automatic scaling to handle load

---

# LAB 1 — Baseline Without Autoscaling

---

## Setup

Deploy `order-service` with fixed replicas:

```yaml
replicas: 2
```

---

## Generate Load

Use any load tool:

```bash
kubectl run -it load-generator --image=busybox -- sh
```

Inside pod:

```bash
while true; do wget -q -O- http://order-service/orders; done
```

---

## Observe

```bash
kubectl top pods
```

* CPU increases
* No scaling happens

---

---

# LAB 2 — Install Metrics Server

---

## Requirement

HPA depends on metrics server

---

## Verify

```bash
kubectl top nodes
kubectl top pods
```

---

---

# LAB 3 — Configure HPA

---

## Create HPA

```bash
kubectl autoscale deployment order-service \
  --cpu-percent=50 \
  --min=2 \
  --max=6
```

---

## Verify

```bash
kubectl get hpa
```

---

---

# LAB 4 — Trigger Autoscaling

---

## Generate Load Again

Same load generator

---

## Observe

```bash
kubectl get pods
kubectl get hpa
```

---

## Expected

* Pods increase from 2 → 3 → 4+

---

---

# LAB 5 — Scale Down Behavior

---

## Stop Load

---

## Observe

* Pods gradually reduce

---

## Note

* Scale down is slower than scale up

---

---

# LAB 6 — Misconfiguration Scenario

---

## Issue

Set unrealistic target:

```bash
--cpu-percent=10
```

---

## Tasks

* Apply HPA
* Generate load

---

## Observe

* Too many pods created
* Over-scaling

---

---

# LAB 7 — Resource Requests Requirement

---

## Problem

HPA not working correctly

---

## Fix

Add resource requests:

```yaml
resources:
  requests:
    cpu: "200m"
```

---

## Tasks

* Apply configuration
* Re-test autoscaling

---

---

# LAB 8 — Cluster Autoscaler (Concept + Demo)

---

## Scenario

* HPA increases pods
* But cluster has no capacity

---

## Observe

```bash
kubectl get pods
```

* Pods in Pending state

---

## Explanation

* Cluster Autoscaler adds nodes automatically

---

## Cloud Demo (if available)

* Trigger scale → new node created

---

---

# LAB 9 — Combined Scenario

---

## Objective

* Traffic spike
* HPA scales pods
* CA scales nodes

---

## Tasks

* Generate heavy load
* Observe:

    * Pod scaling
    * Node scaling

---

---

# Real Case Scenario

---

## Issue

* High traffic → slow response
* Pods not scaling

---

## Investigation

```bash
kubectl get hpa
kubectl describe hpa
kubectl top pods
```

---

## Root Causes

* Metrics server missing
* No resource requests
* Wrong CPU threshold

---

## Resolution

* Install metrics server
* Define resource requests
* Adjust scaling threshold

---

---

# Validation Tasks

---

* Verify pods scale up during load
* Verify pods scale down after load
* Confirm no pending pods (if CA enabled)

---

---

# Final Architecture After Session

```
Client → order-service (auto scaling pods) → inventory-service
           ↑
     HPA (pods)
     CA (nodes)
```

---

