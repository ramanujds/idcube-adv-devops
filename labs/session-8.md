# SESSION 8 — Monitoring (Prometheus + Grafana)

---

## Session Goal

* Set up cluster monitoring
* Collect application and system metrics
* Visualize data using dashboards
* Detect issues early

---

# Continuing Case Study

> Your system is stable after troubleshooting.
>
> But:
>
> * You don’t know when CPU spikes happen
> * You can’t see trends
> * Failures are detected too late
>
> Your task:
> Build a monitoring system for visibility

---

# LAB 1 — Deploy Prometheus Stack

---

## Tool

Use Helm

---

## Install

```bash id="xq9u3g"
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install monitoring prometheus-community/kube-prometheus-stack
```

---

## Verify

```bash id="2y82km"
kubectl get pods
```

---

---

# LAB 2 — Access Grafana

---

## Get Service

```bash id="it38x3"
kubectl get svc
```

---

## Access

* Port-forward or LoadBalancer

```bash id="9r7xy9"
kubectl port-forward svc/monitoring-grafana 3000:80
```

---

## Login

* Default credentials from secret

---

---

# LAB 3 — Explore Default Dashboards

---

## Tasks

* Open Kubernetes dashboards
* View:

    * Node CPU
    * Pod memory
    * Network usage

---

---

# LAB 4 — Monitor order-service & inventory-service

---

## Add Labels

Ensure pods have labels:

```yaml id="0d4h1h"
labels:
  app: order-service
```

---

## Tasks

* Filter metrics in Grafana
* Observe:

    * CPU usage
    * Memory usage

---

---

# LAB 5 — Generate Load & Observe Metrics

---

## Generate Load

```bash id="z9g7pe"
while true; do wget -q -O- http://order-service/orders; done
```

---

## Observe in Grafana

* CPU spike
* Increased requests

---

---

# LAB 6 — Prometheus Query Basics

---

## Open Prometheus UI

---

## Example Queries

```id="rd22fi"
rate(container_cpu_usage_seconds_total[1m])
```

```id="d2v62n"
kube_pod_container_status_restarts_total
```

---

## Tasks

* Run queries
* Correlate with system behavior

---

---

# LAB 7 — Create Custom Dashboard

---

## Tasks

* Create new Grafana dashboard
* Add panels:

    * CPU usage (order-service)
    * Memory usage
    * Pod restarts

---

---

# LAB 8 — Alerts Setup (Basic)

---

## Example Alert

* High CPU usage

---

## Tasks

* Configure alert rule in Grafana
* Trigger via load

---

---

# LAB 9 — Detect Failure via Monitoring

---

## Inject Issue

* Crash order-service
* Increase memory usage

---

## Tasks

* Observe:

    * Restart spikes
    * CPU/memory anomalies

---

---

# LAB 10 — Combined Scenario

---

## Scenario

* Traffic spike
* Pod restart
* Memory increase

---

## Tasks

* Use dashboards to:

    * Identify issue
    * Correlate metrics

---

---

# Real Case Scenario

---

## Issue

* Users report slow responses
* No logs indicate problem

---

## Investigation

* Check CPU usage
* Check pod restarts
* Check memory usage

---

## Root Causes

* CPU saturation
* High restart count

---

## Resolution

* Scale pods
* Optimize resources

---

---

# Validation Tasks

---

* Grafana dashboard shows metrics correctly
* Load reflected in charts
* Alerts triggered successfully

---

---

# Final Architecture After Session

```id="2w2t5o"
Client → order-service → inventory-service
          ↑
     Prometheus (metrics)
     Grafana (visualization)
```


