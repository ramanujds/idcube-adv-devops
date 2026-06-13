
output "gcp_instance_ip" {
  value = module.gcp_vm.instance_public_ip
}

output "aws_instance_ip" {
  value = module.aws_vm.instance_public_ip
}
