# SESSION 3 — Probes (Liveness, Readiness, Startup)

---

## Session Goal

* Configure and tune probes correctly
* Understand impact of probes on traffic and restarts
* Debug probe-related production failures

---

# Continuing Case Study

> After fixing communication, system is deployed.
>
> New issues:
>
> * Pods are restarting frequently
> * Users are getting intermittent failures
> * Some pods receive traffic even when not ready
>
> Your task:
> Stabilize application using proper probe configuration

---

# LAB 1 — Deploy Without Probes (Baseline)

---

## Setup

Deploy `order-service` and `inventory-service` **without probes**

---

## Tasks

* Access APIs
* Simulate delay in application startup

---

## Inject Delay (example)

* Add artificial startup delay (sleep 20–30 seconds)

---

## Observe

* Traffic hits pod immediately
* Failures during startup

---

---

# LAB 2 — Readiness Probe

---

## Add Readiness Probe

```yaml
readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5
```

---

## Tasks

* Redeploy application
* Call API continuously

---

## Observe

* Traffic only goes to ready pods
* No failures during startup

---

---

# LAB 3 — Break Readiness Probe

---

## Inject Failure

Change path:

```yaml
path: /wrong-health
```

---

## Tasks

* Deploy and test

---

## Debug Commands

```bash
kubectl describe pod <pod-name>
kubectl get pods
```

---

## Observe

* Pod is running
* But not receiving traffic

---

---

# LAB 4 — Liveness Probe

---

## Add Liveness Probe

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
```

---

## Tasks

* Simulate app crash (make `/health` fail after some time)
* Observe behavior

---

## Observe

* Container restarts automatically

---

---

# LAB 5 — Misconfigured Liveness (Common Production Issue)

---

## Inject Issue

```yaml
initialDelaySeconds: 2
```

(App needs ~20 seconds to start)

---

## Tasks

* Deploy application
* Monitor pods

---

## Observe

* Continuous restarts
* CrashLoopBackOff

---

## Debug

```bash
kubectl get pods
kubectl describe pod
```

---

---

# LAB 6 — Startup Probe (Fix for Slow Apps)

---

## Add Startup Probe

```yaml
startupProbe:
  httpGet:
    path: /health
    port: 8080
  failureThreshold: 30
  periodSeconds: 5
```

---

## Tasks

* Combine with liveness probe
* Redeploy

---

## Observe

* No premature restarts
* Stable startup

---

---

# LAB 7 — Combined Probes (Production Setup)

---

## Final Configuration

```yaml
readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5

livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 20
  periodSeconds: 10

startupProbe:
  httpGet:
    path: /health
    port: 8080
  failureThreshold: 30
  periodSeconds: 5
```

---

## Tasks

* Deploy full configuration
* Validate stability

---

---

# Real Case Scenario

---

## Issue

* Pods restarting continuously
* Application never becomes stable

---

## Investigation Flow

1. Check pod status
2. Describe pod
3. Review probe configuration
4. Compare startup time vs probe timing

---

## Root Cause

* Liveness probe triggering too early

---

## Fix

* Add startup probe
* Increase initial delay

---

---

# Validation Tasks

---

* Verify pod restarts count = stable
* Ensure traffic only goes to ready pods
* Confirm no failures during startup

---

---

# Final State After Session

```
Client → order-service (stable probes) → inventory-service
```


