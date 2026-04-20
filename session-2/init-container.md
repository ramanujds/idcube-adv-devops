# Init Containers — Spring Boot Real Use Case

## The Situation

You have a Spring Boot microservice that:

* Depends on a **MySQL database**
* Needs:

    * DB to be ready
    * Schema/migrations applied before startup

Now the real-world problem:

> “Spring Boot starts faster than the database → app crashes with connection errors”

---

# Why Init Container?

Instead of:

* Writing retry logic in Java
* Adding hacks in code

We handle it at **Kubernetes level**

> “Don’t let the app start until everything is ready”

---

# What is an Init Container?

* Runs **before main container**
* Must complete successfully
* Runs **sequentially**
* If it fails → Pod doesn’t start

---

# Real Use Case 1: Wait for Database

## Problem

Spring Boot tries:

```
JDBC connection → FAIL (DB not ready)
```

---

## Solution: Init Container waits for DB

### YAML Example

```yaml id="x7k2p1"
apiVersion: v1
kind: Pod
metadata:
  name: springboot-init-demo
spec:
  containers:
    - name: springboot-app
      image: my-springboot-app
      ports:
        - containerPort: 8080

  initContainers:
    - name: wait-for-db
      image: busybox
      command:
        - sh
        - -c
        - |
          until nc -z mysql-service 3306; do
            echo "Waiting for MySQL..."
            sleep 3
          done
```

---

## What Happens Internally

1. Init container starts
2. Checks:

   ```
   mysql-service:3306
   ```
3. If NOT ready → keeps retrying
4. Once ready → exits successfully
5. Spring Boot container starts

---

# Real Use Case 2: DB Migration (Very Practical)

This is gold for interviews + real systems.

---

## Scenario

You use:

* Flyway / Liquibase
* Need schema created before app starts

---

## Init Container Approach

* Run migration script
* Then start app

---

### YAML Example

```yaml id="6c9p2z"
initContainers:
  - name: db-migration
    image: flyway/flyway
    command: ["flyway", "migrate"]
    env:
      - name: FLYWAY_URL
        value: jdbc:mysql://mysql-service:3306/mydb
      - name: FLYWAY_USER
        value: root
      - name: FLYWAY_PASSWORD
        value: password
```

---

## Why This Is Better Than App-Level Migration

* No race conditions
* Clear separation of concerns
* Fail fast before app starts

---

# Real Use Case 3: Config Download (Very Common)

## Scenario

* Config stored in:

    * S3 / Git / Vault

---

## Init Container:

* Downloads config
* Saves to shared volume

---

### Example

```yaml id="y2p9b3"
volumes:
  - name: config-volume
    emptyDir: {}

initContainers:
  - name: fetch-config
    image: curlimages/curl
    command:
      - sh
      - -c
      - curl -o /config/app.properties http://config-server/app.properties
    volumeMounts:
      - name: config-volume
        mountPath: /config

containers:
  - name: springboot-app
    image: my-app
    volumeMounts:
      - name: config-volume
        mountPath: /app/config
```

---

# Key Differences: Sidecar vs Init 

| Feature      | Init Container      | Sidecar        |
| ------------ | ------------------- | -------------- |
| When it runs | Before app          | Alongside app  |
| Lifecycle    | One-time            | Continuous     |
| Purpose      | Setup               | Support        |
| Example      | DB ready, migration | Logging, proxy |

---

# Debugging Init Containers 

### Check status

```bash id="3f8k1x"
kubectl get pod springboot-init-demo
```

---

### Describe pod

```bash id="b8n2c7"
kubectl describe pod springboot-init-demo
```

Look for:

* Init container status
* Errors

---

### Logs

```bash id="m1p9d4"
kubectl logs springboot-init-demo -c wait-for-db
```

---

# Common Mistakes (Seen in Real Projects)

### 1. Infinite Loop

* Bad script → pod never starts

---

### 2. Wrong Service Name

* `mysql-service` typo → stuck forever

---

### 3. Network Issues

* DNS not resolving

---




