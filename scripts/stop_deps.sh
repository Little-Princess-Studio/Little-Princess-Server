#!/bin/bash

echo Stop RabbitMQ...
docker kill lps-rabbitmq

echo Stop Redis...
docker kill lps-redis

echo Stop Mongo...
docker kill lps-mongo

echo Command completed.
