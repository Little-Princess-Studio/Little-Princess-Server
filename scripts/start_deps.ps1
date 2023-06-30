Write-Output Start RabbitMQ...
$output = docker ps -a --filter "name=lps-rabbitmq" --format "{{.Names}}"
if ($output -eq "lps-rabbitmq") {
    docker start lps-rabbitmq
} else {
    docker run --rm -it --name lps-rabbitmq -d -p 5672:5672 -p 15672:15672 rabbitmq:3.12-management
}

Write-Output Start Redis...
$output = docker ps -a --filter "name=lps-redis" --format "{{.Names}}"
if ($output -eq "lps-redis") {
    docker start lps-redis
} else {
    docker run --rm -it --name lps-redis -d redis
}

Write-Output Command completed.
pause
