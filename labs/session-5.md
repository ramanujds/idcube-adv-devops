# SESSION 5 — Node Scheduling (Affinity, Anti-Affinity, Taints & Tolerations) 

---

## Session Goal

* Control pod placement across nodes
* Isolate workloads
* Handle multi-tenant and production scenarios

---

# Continuing Case Study

> Your application is running with controlled rollouts.
>
> New problems appear:
>
> * Critical services slow down during load
> * Some nodes are overloaded
> * You want to isolate workloads (e.g., order-service vs others)
>
> Your task:
> Control scheduling and ensure predictable performance

---

# LAB 1 — Baseline Scheduling (Default Behavior)

---

## Setup

* Deploy:

    * `order-service`
    * `inventory-service`

---

## Tasks

```bash
kubectl get pods -o wide
```

---

## Observe

* Pods randomly distributed across nodes

---

---

# LAB 2 — Node Labeling

---

## Step 1 — Label Nodes

```bash
kubectl label nodes <node1> workload=critical
kubectl label nodes <node2> workload=general
```

---

## Tasks

```bash
kubectl get nodes --show-labels
```

---

---

# LAB 3 — Node Selector

---

## Apply Node Selector

```yaml
nodeSelector:
  workload: critical
```

---

## Tasks

* Apply to `order-service`
* Redeploy

---

## Observe

```bash
kubectl get pods -o wide
```

* Pods only run on labeled nodes

---

---

# LAB 4 — Node Affinity (Advanced Scheduling)

---

## Replace nodeSelector with affinity

```yaml
affinity:
  nodeAffinity:
    requiredDuringSchedulingIgnoredDuringExecution:
      nodeSelectorTerms:
        - matchExpressions:
            - key: workload
              operator: In
              values:
                - critical
```

---

## Tasks

* Apply configuration
* Verify scheduling

---

---

# LAB 5 — Preferred Affinity (Soft Rule)

---

## Update Affinity

```yaml
preferredDuringSchedulingIgnoredDuringExecution:
  - weight: 1
    preference:
      matchExpressions:
        - key: workload
          operator: In
          values:
            - critical
```

---

## Tasks

* Deploy multiple replicas
* Observe distribution

---

---

# LAB 6 — Pod Anti-Affinity (Avoid Same Node)

---

## Problem

Multiple replicas on same node → risk of downtime

---

## Add Anti-Affinity

```yaml
affinity:
  podAntiAffinity:
    requiredDuringSchedulingIgnoredDuringExecution:
      - labelSelector:
          matchLabels:
            app: order
        topologyKey: "kubernetes.io/hostname"
```

---

## Tasks

* Deploy replicas
* Verify pods are spread across nodes

---

---

# LAB 7 — Taints & Tolerations

---

## Step 1 — Taint Node

```bash
kubectl taint nodes <node1> dedicated=order:NoSchedule
```

---

## Step 2 — Observe

* Pods not scheduled on tainted node

---

## Step 3 — Add Toleration

```yaml
tolerations:
- key: "dedicated"
  operator: "Equal"
  value: "order"
  effect: "NoSchedule"
```

---

## Tasks

* Apply to `order-service`
* Verify scheduling

---

---

# LAB 8 — Combined Real Scenario

---

## Objective

* `order-service` → only critical nodes
* `inventory-service` → general nodes
* Spread replicas across nodes
* Allow only specific pods on dedicated nodes

---

## Tasks

* Combine:

    * Node affinity
    * Anti-affinity
    * Tolerations

---

---

# Real Case Scenario

---

## Issue

* High CPU workloads affecting critical APIs
* Pods randomly scheduled
* No isolation

---

## Investigation

```bash
kubectl get pods -o wide
kubectl describe node
```

---

## Root Cause

* No scheduling constraints
* Multiple workloads competing

---

## Resolution

* Label nodes
* Apply node affinity
* Add taints for isolation

---

---

# Validation Tasks

---

* Ensure `order-service` runs only on critical nodes
* Ensure replicas are distributed
* Ensure tainted nodes accept only intended pods

---

---

# Final Architecture After Session

```
Node Pool:
- Critical Nodes → order-service
- General Nodes → inventory-service

Client → order-service → inventory-service
```

---

