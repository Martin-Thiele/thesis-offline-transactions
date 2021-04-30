#!/usr/bin/env bash
docker build -t kubank .
heroku container:push -a kubank web
heroku container:release -a kubank web