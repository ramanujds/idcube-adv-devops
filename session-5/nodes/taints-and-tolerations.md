Node Affinity is:

> “Pod chooses the node”

**Taints & Tolerations** is:

> “Node rejects pods”

That mindset shift is everything.

---

# How It Works

Think like this:

* Node says: “I don’t want normal pods.”
* Pod says: “It’s okay, I can tolerate this.”

If pod doesn’t tolerate → Scheduler won’t place it.

---

# Step 1 — Check Your Node

```bash
kubectl get nodes
```

You likely have:

```
docker-desktop
```

---

# Step 2 — Add a Taint to the Node

Let’s taint your only node:

```bash
kubectl taint nodes docker-desktop env=dev:NoSchedule
```

Now check:

```bash
kubectl describe node docker-desktop
```

You’ll see:

```
Taints: env=dev:NoSchedule
```

---

# What Does This Mean?

The node is saying:

> “Don’t schedule any new pods here unless they tolerate env=dev”

---

# Step 3 — Try Creating a Normal Pod

Create a simple nginx deployment:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-test
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
      containers:
        - name: nginx
          image: nginx
```

Apply it.

Now:

```bash
kubectl get pods
```

Status:

```
Pending
```

Describe:

```bash
kubectl describe pod <pod-name>
```

You’ll see:

```
1 node(s) had taint {env: dev}, that the pod didn't tolerate
```

This is how scheduler blocks it.

---

# Step 4 — Add Toleration

Now modify YAML:

```yaml
spec:
  tolerations:
    - key: "env"
      operator: "Equal"
      value: "dev"
      effect: "NoSchedule"
  containers:
    - name: nginx
      image: nginx
```

Apply again.

Now:

```bash
kubectl get pods
```

It runs.

Why?

Because pod says:

> “I tolerate env=dev:NoSchedule”

Node says:

> “Okay fine, you can come.”

---

# Taint Structure

```
key=value:effect
```

Example:

```
env=dev:NoSchedule
```

---

# 3 Types of Taint Effects

## NoSchedule

* New pods won’t be scheduled
* Existing pods remain

Most commonly used.

---

## PreferNoSchedule

* Soft rule
* Scheduler tries to avoid but may place pod

Like preferred affinity.

---

## NoExecute

Strongest one.

* New pods not scheduled
* Existing pods get evicted (unless tolerated)

This is powerful.

---

# Example: NoExecute with Time

```yaml
tolerations:
  - key: "env"
    operator: "Equal"
    value: "dev"
    effect: "NoExecute"
    tolerationSeconds: 60
```

Meaning:

Pod can stay for 60 seconds,
then gets evicted.

This is used in node failure scenarios.

---

# Production Use Cases

## Dedicated Nodes

Example:

```
kubectl taint nodes node1 dedicated=database:NoSchedule
```

Only DB pods with toleration can run there.

---

## Spot Instances (EKS)

Taint spot nodes:

```
node-type=spot:NoSchedule
```

Only non-critical workloads tolerate it.

---

## Control Plane Protection

In some clusters:

```
node-role.kubernetes.io/control-plane:NoSchedule
```

So normal pods don’t run on master.

In docker-desktop, you may see something similar.

---

# Affinity vs Taints

Affinity says:

> “I prefer this node.”

Taints say:

> “You’re not allowed here.”

In real clusters, they’re often used together.

# Remove the Taint (Cleanup)

```bash
kubectl taint nodes docker-desktop env=dev:NoSchedule-
```

The dash removes it.

---

# Visual Flow

Scheduler logic simplified:

1. Check taints
2. Check tolerations
3. Check nodeSelector / affinity
4. Check resources
5. Bind pod

