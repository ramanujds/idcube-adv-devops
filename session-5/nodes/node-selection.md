# What is Node Selection in Kubernetes?

In Kubernetes, **the scheduler decides which node a Pod runs on**.

By default:

* You create a Pod / Deployment
* Kubernetes scheduler checks:

    * CPU/memory availability
    * Taints & tolerations
    * Node affinity
    * Node selectors
* Then it assigns the Pod to a node.

In our case:

```
docker-desktop (only 1 node)
```

So scheduler has only one option.

But we can still use **nodeSelector**, and it will work logically — even though there's only one node.

---

# First, Check Your Node

Run:

```bash
kubectl get nodes --show-labels
```

You’ll see something like:

```
docker-desktop   Ready   control-plane   ...
kubernetes.io/hostname=docker-desktop
beta.kubernetes.io/os=linux
...
```

Your node already has labels.

Node selection works using these **labels**.

---

# How nodeSelector Works

You tell Kubernetes:

> "Only schedule this Pod on a node that has this label."

Example:

```yaml
nodeSelector:
  disktype: ssd
```

Scheduler logic:

* Find nodes with label `disktype=ssd`
* Schedule only there

---

# Let’s Create a Real Example on docker-desktop

Since you only have 1 node, let’s add a custom label to it.

### Step 1: Add Label to Node

```bash
kubectl label nodes docker-desktop env=dev
```

Now check:

```bash
kubectl get nodes --show-labels
```

You should see:

```
env=dev
```

---

# Create Deployment Using nodeSelector

Create `nginx-node-selector.yaml`

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-node-selector
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
      nodeSelector:
        env: dev
      containers:
        - name: nginx
          image: nginx
          ports:
            - containerPort: 80
```

Apply it:

```bash
kubectl apply -f nginx-node-selector.yaml
```

Check:

```bash
kubectl get pods -o wide
```

You’ll see:

```
NODE
docker-desktop
```

It scheduled successfully because node has `env=dev`.

---

# Now Let’s Break It (Important for Understanding)

Delete the deployment:

```bash
kubectl delete -f nginx-node-selector.yaml
```

Now create another YAML:

```yaml
nodeSelector:
  env: prod
```

Apply it.

Now check:

```bash
kubectl get pods
```

You’ll see:

```
Pending
```

Check reason:

```bash
kubectl describe pod <pod-name>
```

You’ll see:

```
0/1 nodes are available: 1 node(s) didn't match node selector.
```

That’s how scheduler behaves.

Even with single node, logic is same as production cluster.

---

# Where Node Selection Is Used in Real Projects

You’ll use nodeSelector when:

### 1. GPU Nodes

Only schedule ML workloads on GPU nodes.

### 2. SSD Nodes

Database pods on high IOPS nodes.

### 3. Environment Separation

```
env=dev
env=qa
env=prod
```

### 4. Spot vs On-Demand Nodes (EKS)

```
node-type=spot
node-type=on-demand
```

---

# Difference Between nodeSelector and Node Affinity

| Feature         | nodeSelector | Node Affinity           |
|-----------------|--------------|-------------------------|
| Simple          | Yes          | Advanced                |
| Operators       | No           | Yes (In, NotIn, Exists) |
| Preferred rules | No           | Yes                     |
| Recommended     | Basic use    | Production              |

---

# Important Note

Since `docker-desktop` is:

* Single node
* Control-plane + worker combined
* No real separation of roles

Node selection here is purely conceptual.

But behavior = exactly same as real multi-node cluster.

---
