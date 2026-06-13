## Why IaC is Needed

- **Consistency & reproducibility**: The same code can create identical environments (dev, QA, prod), reducing configuration drift and “works on my machine” issues.[3][4][5]
- **Speed & automation**: Provisioning becomes automated and repeatable, so environments can be created, changed, or destroyed in minutes via CI/CD instead of days of manual work.[4][6][5]
- **Auditability & collaboration**: IaC lives in Git, so every change is versioned, reviewable (PRs), and auditable, which supports compliance and rollback.[7][8][3]
- **Reduced human error & cost**: Scripts replace manual steps, lowering misconfigurations and operational overhead.[9][5][4]

You can explain it simply: infrastructure becomes **software**, so teams apply the same discipline (version control, testing, code review, CI/CD) to infra as they do to application code.[2][1]

***

## Terraform – Core IaC Engine

- Terraform is an IaC tool that uses declarative configuration files (HCL) to describe desired infrastructure across many providers (Azure, AWS, GCP, Kubernetes, etc.).[6][10][8]
- Key capabilities:
    - **Plan → Apply workflow**: `terraform plan` shows the diff, `terraform apply` makes changes, enabling safe, reviewable modifications.[8][11]
    - **State management**: Terraform tracks resources in a state file so it knows what exists and can detect drift or out‑of‑band changes.[10][7]
    - **Modules**: Reusable building blocks (VPC module, AKS module, etc.) to standardize patterns across teams.[11][7]

**Typical uses**

- Provisioning complete environments: networks, VMs, AKS clusters, databases, load balancers.[6][11]
- Creating “landing zones” or baseline setups for new subscriptions/accounts with consistent security, logging, and tagging policies.[7][11]

***

## Checkov – Policy & Security Scanner for IaC

- Checkov is an open‑source static analysis tool that scans Terraform and other IaC (CloudFormation, Kubernetes, etc.) for security, compliance, and best‑practice violations.[12][13]
- It comes with many built‑in policies (e.g., encryption required, logging enabled, no public storage buckets) and supports custom rules.[14][13][12]

**How it is used**

- Local or CI command such as: `checkov -d path/to/terraform`.[15][12]
- Typical findings:
    - Open security groups / public ingress.
    - Unencrypted data at rest.
    - Missing logging or weak IAM policies.[13][12]
- In pipelines, teams configure Checkov to fail the build if severe issues (e.g., HIGH/CRITICAL) are detected, enforcing guardrails early.[14][15]

***

## TFSec – Terraform-Focused Security Scanner

- TFSec is a Terraform‑specific static analysis tool that scans HCL to flag insecure or non‑compliant configurations before `terraform apply`.[16][12][13]
- It focuses on Terraform first, with checks for:
    - Open security groups / firewalls.
    - Public S3/buckets/storage.
    - Missing encryption, logging, or HTTPS-only access.[12][16][13]

**How it is used**

- CLI usage: `tfsec path/to/terraform/code` or `tfsec . --format json/sarif` in CI for reporting and gating.[13][12]
- Integrates with CI tools like Jenkins, GitHub Actions, Azure DevOps so every PR or commit gets scanned automatically.[15][12]

***

## How Terraform, Checkov, and TFSec Work Together

- **Terraform** defines and provisions the infrastructure as code.[8][11]
- **Checkov** and **TFSec** statically analyze that Terraform code **before** apply, catching misconfigurations, security issues, and policy violations in the review/CI stages.[12][13][15]

A simple story you can use:

1. Developer edits Terraform to add a new subnet and VM.
2. CI runs `tfsec` and `checkov`; if any rule (e.g., public VM without NSG, unencrypted disk) fails, the pipeline blocks the change.[13][15][12]
3. Only when IaC passes security checks does the pipeline run `terraform plan` and `terraform apply` to update real infrastructure.[6][15]

## Terraform in a CI/CD Pipeline

A complete IaC pipeline gates infrastructure changes the same way app code changes are gated.

### Typical Pipeline Stages

```
PR opened
    │
    ▼
terraform fmt --check        ← fail if formatting is wrong
terraform validate           ← fail if HCL syntax is invalid
    │
    ▼
tfsec . --minimum-severity HIGH     ← security scan
checkov -d . --framework terraform  ← compliance scan
    │
    ▼
terraform plan               ← generate and post plan as PR comment
    │
    ▼
Manual approval (merge / workflow trigger)
    │
    ▼
terraform apply -auto-approve   ← runs against remote state backend
```

### GitHub Actions Example

```yaml
name: Terraform CI

on:
  pull_request:
    paths:
      - 'terraform-aks/**'

jobs:
  terraform:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3

      - name: Terraform Init
        run: terraform init
        working-directory: terraform-aks
        env:
          ARM_CLIENT_ID: ${{ secrets.ARM_CLIENT_ID }}
          ARM_CLIENT_SECRET: ${{ secrets.ARM_CLIENT_SECRET }}
          ARM_SUBSCRIPTION_ID: ${{ secrets.ARM_SUBSCRIPTION_ID }}
          ARM_TENANT_ID: ${{ secrets.ARM_TENANT_ID }}

      - name: Terraform Format Check
        run: terraform fmt -check
        working-directory: terraform-aks

      - name: Terraform Validate
        run: terraform validate
        working-directory: terraform-aks

      - name: Run tfsec
        uses: aquasecurity/tfsec-action@v1
        with:
          working_directory: terraform-aks

      - name: Terraform Plan
        run: terraform plan -out=tfplan
        working-directory: terraform-aks
```

### Azure Service Principal for CI Authentication

Terraform needs credentials to talk to Azure from CI. Use a Service Principal:

```bash
az ad sp create-for-rbac \
  --name "terraform-ci-sp" \
  --role Contributor \
  --scopes /subscriptions/<subscription-id>
```

Store the output values as CI secrets:

* `ARM_CLIENT_ID` → `appId`
* `ARM_CLIENT_SECRET` → `password`
* `ARM_TENANT_ID` → `tenant`
* `ARM_SUBSCRIPTION_ID` → your subscription ID

***

## References

<https://aws.amazon.com/what-is/iac/>
