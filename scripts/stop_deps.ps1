Write-Output  Stop RabbitMQ...
docker kill lps-rabbitmq

Write-Output  Stop Redis...
docker kill lps-redis

Write-Output  Stop MongoDB...
docker kill lps-mongo

Write-Output  Command completed.
pause
