#!/bin/bash

echo Start RabbitMQ...
docker run -it --name lps-rabbitmq -d -p 5672:5672 -p 15672:15672 rabbitmq:3.12-management

echo Start Redis...
docker run -it --name lps-redis -d redis

echo Command completed.
pause
