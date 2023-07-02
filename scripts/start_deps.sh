#!/bin/bash

echo Start RabbitMQ...
docker start lps-rabbitmq || docker run -it --name lps-rabbitmq -d -p 5672:5672 -p 15672:15672 rabbitmq:3.12-management

echo Start Redis...
docker start lps-redis || docker run -it --name lps-redis -d -p 6379:6379 redis

echo Start MongoDb...
docker start lps-mongo || docker run -it --name lps-mongo -d -p 27017:27017 mongo:latest

echo Command completed.
