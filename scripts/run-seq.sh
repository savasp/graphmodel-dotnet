#!/bin/bash

docker run \
    --name seq \
    -d --restart unless-stopped \
    -e ACCEPT_EULA=Y \
    -e SEQ_PASSWORD="" \
    -p 5341:80 \
    -v /Users/savasp/tmp/logdata:/data \
    datalust/seq:latest
