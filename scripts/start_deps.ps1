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
    docker run --rm -it --name lps-redis -d -p 6379:6379 redis
}

Write-Output Start MongoDb...
$output = docker ps -a --filter "name=lps-mongo" --format "{{.Names}}"
if ($output -eq "lps-mongo") {
    docker start lps-mongo
} else {
    docker run -it --name lps-mongo -d -p 27017:27017 mongo:latest
}

Write-Output Command completed.
pause
