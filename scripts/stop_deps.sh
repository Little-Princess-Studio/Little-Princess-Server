#!/bin/bash

echo Stop RabbitMQ...
docker kill lps-rabbitmq

echo Stop Redis...
docker kill lps-redis

echo Command completed.
