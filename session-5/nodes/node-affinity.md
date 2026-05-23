# What is Node Affinity?

Node Affinity lets you control **which nodes a Pod can run on**, but with:

* Advanced matching rules (In, NotIn, Exists, etc.)
* Hard rules (must match)
* Soft rules (preferred but not mandatory)

This is what real clusters use — especially in EKS / GKE multi-node setups.

Even on your **single-node docker-desktop**, we can simulate everything.

---

# First — Check Your Node

```bash
kubectl get nodes --show-labels
```

You’ll see something like:

```
docker-desktop
kubernetes.io/hostname=docker-desktop
kubernetes.io/os=linux
```

Let’s add a label to experiment.

```bash
kubectl label nodes docker-desktop env=dev
```

---

# Structure of Node Affinity

Node affinity is defined under:

```yaml
spec:
  affinity:
    nodeAffinity:
```

There are two types:

## requiredDuringSchedulingIgnoredDuringExecution

Hard rule — MUST match
If not → Pod stays Pending

## preferredDuringSchedulingIgnoredDuringExecution

Soft rule — Try to match
If not → Scheduler still places Pod

---

# Example 1 — Hard Rule (Required)

Create this:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-affinity-required
spec:
  replicas: 1
  selector:
    matchLabels:
      app: nginx
  template:
    metadata:
      labels:
        app: nginx
    spec:
      affinity:
        nodeAffinity:
          requiredDuringSchedulingIgnoredDuringExecution:
            nodeSelectorTerms:
              - matchExpressions:
                  - key: env
                    operator: In
                    values:
                      - dev
      containers:
        - name: nginx
          image: nginx
```

Apply:

```bash
kubectl apply -f file.yaml
```

It will run successfully because:

```
docker-desktop has env=dev
```

---

# Now Break It

Change:

```
values:
  - prod
```

Reapply.

Now:

```bash
kubectl get pods
```

Status:

```
Pending
```

Describe it:

```bash
kubectl describe pod <pod-name>
```

You’ll see:

```
0/1 nodes are available: 1 node(s) didn't match node affinity.
```

Exactly like multi-node production clusters.

---

# Example 2 — Soft Rule (Preferred)

```yaml
affinity:
  nodeAffinity:
    preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 1
        preference:
          matchExpressions:
            - key: env
              operator: In
              values:
                - prod
```

Now:

Even though your node doesn’t have `env=prod`,
Pod will still schedule.

Why?

Because it’s just a preference.

---

# Operators Available

This is where affinity becomes powerful.

| Operator     | Meaning                        |
|--------------|--------------------------------|
| In           | Value must match one of listed |
| NotIn        | Must NOT match                 |
| Exists       | Key must exist                 |
| DoesNotExist | Key must not exist             |
| Gt           | Greater than                   |
| Lt           | Less than                      |

Example:

```yaml
operator: Exists
```

Means:

If node has label key — doesn’t matter value.

---

# Real Production Use Cases

Imagine EKS cluster:

Node Group A:

```
node-type=spot
```

Node Group B:

```
node-type=on-demand
```

Critical workloads:

```yaml
required:
  node-type=on-demand
```

Non-critical workloads:

```yaml
preferred:
  node-type=spot
```

This is how cost optimization is done in real systems.

---

# nodeSelector vs Node Affinity

nodeSelector:

```yaml
nodeSelector:
  env: dev
```

Node Affinity:

```yaml
matchExpressions:
  - key: env
    operator: In
    values:
      - dev
```

nodeSelector is just a simplified version of required affinity.

---

# Important Concept

Notice the name:

```
requiredDuringSchedulingIgnoredDuringExecution
```

Meaning:

* Required when scheduling
* But ignored after scheduling

If you remove label from node later,
Pod will NOT be evicted.

That’s an important interview question.

---



