# SESSION 7 — Troubleshooting (CrashLoopBackOff, OOMKilled, ImagePullBackOff) 

---

## Session Goal

* Diagnose common Kubernetes failures
* Use `kubectl` effectively for debugging
* Identify root causes and fix issues

---

# Continuing Case Study

> Your system now auto-scales and handles traffic.
>
> Suddenly:
>
> * Some pods are restarting continuously
> * Some are stuck in pending/image errors
> * APIs intermittently fail
>
> Your task:
> Debug and restore system stability

---

# LAB 1 — Debug Flow (Baseline)

---

## Commands to Use Throughout

```bash
kubectl get pods
kubectl describe pod <pod-name>
kubectl logs <pod-name>
kubectl logs <pod-name> --previous
kubectl get events
```

---

## Task

* Observe current system state
* Identify abnormal pods

---

---

# LAB 2 — CrashLoopBackOff

---

## Inject Failure

Modify `order-service`:

* Exit immediately after start
  (simulate crash)

---

## Observe

```bash
kubectl get pods
```

* Status: `CrashLoopBackOff`

---

## Debug

```bash
kubectl logs <pod-name>
kubectl logs <pod-name> --previous
kubectl describe pod <pod-name>
```

---

## Fix

* Correct application logic
* Redeploy

---

---

# LAB 3 — Misconfigured Command

---

## Inject Issue

```yaml
command: ["wrong-command"]
```

---

## Tasks

* Deploy
* Observe failure

---

## Debug

* Logs show command not found

---

## Fix

* Correct command

---

---

# LAB 4 — OOMKilled

---

## Inject Issue

Set low memory limit:

```yaml
resources:
  limits:
    memory: "100Mi"
```

---

## Simulate

* Add memory-heavy operation

---

## Observe

```bash
kubectl describe pod <pod-name>
```

* Status: `OOMKilled`

---

## Fix

* Increase memory limits
* Optimize application

---

---

# LAB 5 — ImagePullBackOff

---

## Inject Issue

```yaml
image: order-service:wrong-tag
```

---

## Observe

* Pod stuck in `ImagePullBackOff`

---

## Debug

```bash
kubectl describe pod <pod-name>
```

---

## Fix

* Correct image tag
* Verify registry access

---

---

# LAB 6 — Unauthorized Image Pull (401)

---

## Inject Issue

* Remove image pull secret / identity

---

## Observe

* Unauthorized error

---

## Debug

* Events show 401

---

## Fix

* Add correct credentials
* Reconfigure identity

---

---

# LAB 7 — Pending Pods

---

## Inject Issue

* Request high CPU:

```yaml
resources:
  requests:
    cpu: "4"
```

---

## Observe

* Pod stuck in `Pending`

---

## Debug

```bash
kubectl describe pod <pod-name>
kubectl get nodes
```

---

## Fix

* Reduce requests
* Scale cluster

---

---

# LAB 8 — Service Failure Debugging

---

## Inject Issue

* Break service selector

---

## Observe

* API calls failing

---

## Debug

```bash
kubectl get svc
kubectl get endpoints
```

---

## Fix

* Correct selector

---

---

# LAB 9 — Combined Failure Scenario

---

## Scenario

* order-service crashing
* inventory-service unreachable
* one pod in pending

---

## Tasks

* Identify all issues
* Fix step by step

---

---

# Real Case Scenario

---

## Issue

* Intermittent API failures
* Pods restarting
* Some requests timing out

---

## Investigation Flow

1. Check pod status
2. Identify failing pods
3. Check logs
4. Review events
5. Validate resources & configs

---

## Root Causes

* CrashLoopBackOff (bad code/config)
* OOMKilled (low memory)
* Service misconfiguration

---

## Resolution

* Fix app crash
* Adjust resource limits
* Correct service mapping

---

---

# Validation Tasks

---

* All pods in Running state
* No restart loops
* Services reachable
* No pending pods

---

---

# Final Architecture After Session

```id="d4s6qe"
Client → order-service (stable) → inventory-service (reachable)
```

---
