# Multi-Container Pod (Sidecar Pattern) 

## The Situation

You have a typical Spring Boot microservice:

* Handles APIs
* Writes logs to files (`/var/log/app.log`)
* Needs:

    * Centralized logging
    * Zero changes to application code

Now the question:

> “How do I ship logs to ELK / Splunk without touching my Spring Boot code?”

This is where **Sidecar Pattern** shines.

---

# Architecture Overview

### What we’ll build

* **Main container** → Spring Boot app
* **Sidecar container** → Fluentd (log shipper)
* **Shared volume** → for logs


# How It Works (Step-by-Step)

### 1. Spring Boot App

* Writes logs to file:

```properties
logging.file.name=/var/log/app/app.log
```

---

### 2. Shared Volume

Both containers mount:

```yaml
volumes:
- name: log-volume
  emptyDir: {}
```

---

### 3. Sidecar (Fluentd)

* Reads logs from same volume
* Ships to ELK / external system

---

# Kubernetes YAML 

## Pod Definition

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: springboot-logging-pod
spec:
  volumes:
    - name: log-volume
      emptyDir: {}

  containers:
    - name: springboot-app
      image: my-springboot-app:latest
      ports:
        - containerPort: 8080
      volumeMounts:
        - name: log-volume
          mountPath: /var/log/app

    - name: fluentd-sidecar
      image: fluent/fluentd
      volumeMounts:
        - name: log-volume
          mountPath: /var/log/app
```

---

# What’s Actually Happening Internally

### Communication Pattern

* Spring Boot → writes logs
* Fluentd → reads logs

No REST call, no network.

> This is **file-based communication via shared volume**

---

# Why This Is Powerful (Teach This Clearly)

### 1. Separation of Concerns

* App focuses on business logic
* Logging handled externally

---

### 2. No Code Change Required

You didn’t:

* Add logging libraries
* Modify Spring Boot logic

---

### 3. Reusable Pattern

Same sidecar can be used for:

* 10 services
* 100 services

---

# Real-World Extension 

You typically won’t run plain Pods. You’ll use:

* Deployment
* ConfigMaps (Fluentd config)
* External logging system (ELK)

# Alternative Sidecar Use Cases 


### 1. Security (mTLS via Envoy)

* Sidecar handles encryption
* Spring Boot stays simple

---

### 2. Monitoring Agent

* Prometheus exporter sidecar

---

### 3. API Gateway Proxy

* Istio / Envoy sidecar
* Traffic routing, retries, circuit breaking

---

# When SHOULD You Use Sidecar?

Use it when:

* You want to **extend functionality without changing app**
* Cross-cutting concerns:

    * Logging
    * Security
    * Monitoring

---

# When NOT to Use It 

Avoid if:

* Adds unnecessary complexity
* Tight coupling not required
* Can be handled externally (e.g., node-level logging)

---

# Common Pitfalls 

### 1. Volume Issues

* Wrong mount path → logs not visible

### 2. Resource Contention

* Sidecar consuming CPU/memory

### 3. Debugging Confusion

* “App is fine, sidecar is broken”

---

# Debugging This Setup

```bash
kubectl exec -it springboot-logging-pod -c springboot-app -- ls /var/log/app
kubectl exec -it springboot-logging-pod -c fluentd-sidecar -- tail -f /var/log/app/app.log
```

