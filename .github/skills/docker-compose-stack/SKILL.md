---
name: docker-compose-stack
description: Use when changing, starting, debugging, or documenting the local Docker Compose stack that runs the SUT images from Docker Hub.
---

# Local Compose stack

- `docker-compose.yml` runs pre-built SUT images from Docker Hub (`gromozeka1/hardpoint-productapi`, `gromozeka1/hardpoint-webapp`); this repository has no Dockerfiles or build contexts. Image source and build logic live in https://github.com/gromozeka1/sut-hardpoint.
- Select the SUT version with `SUT_TAG` (default `1.0.0`; also `latest` or `sha-<shortsha>`).
- The Compose services are `productapi` and `webapp`; WebApp must call `http://productapi:8080/` on the Compose network, not `localhost`.
- The published local endpoints are `http://localhost:5044` for ProductAPI and `http://localhost:5168` for WebApp.
- ProductAPI persists its SQLite file in `/app/Database`, backed by the `productapi-data` named volume. Do not remove or replace persistent storage unintentionally.
- Validate edits with `docker compose config`, then use `docker compose up -d --wait` when Docker is available. Report startup or environmental failures rather than presenting configuration validation as an integration test.
