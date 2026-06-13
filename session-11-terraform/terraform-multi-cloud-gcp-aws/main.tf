
module "gcp_vm" {
  source     = "./modules/gcp"
}

module "aws_vm" {
  source   = "./modules/aws"
}
