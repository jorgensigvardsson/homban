#!/usr/bin/env bash

docker-compose build
docker-compose push
docker-compose --context homban_prod pull
docker-compose --context homban_prod down
docker-compose --context homban_prod up -d