
provider "aws" {
  region = "us-east-1"
}

resource "aws_instance" "app_server" {
  ami                    = "ami-09e6f87a47903347c"
  instance_type          = "t2.micro"
  vpc_security_group_ids = ["sg-096e7821981a6b2a8"]

  tags = {
    Name = "ExampleAppServerInstance"
  }

  user_data = <<-EOF
    #!/bin/bash
    sudo yum update -y
    sudo yum install docker -y
    sudo service docker start
    sudo docker run -d -p 80:8080 --name part-inventory-service -d ram1uj/part-inventory-service:latest
  EOF
}

output "instance_public_ip" {
  value = aws_instance.app_server.public_ip
}
