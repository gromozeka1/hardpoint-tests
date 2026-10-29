---
applyTo: "**/docker-compose.yml"
---

# Container guidance

- This repository contains no Dockerfiles: the SUT images are built and published by https://github.com/gromozeka1/sut-hardpoint. Change image tags here, not image contents.
- Preserve the Compose service names `productapi` and `webapp`, their container port `8080`, and the internal WebApp-to-API URL `http://productapi:8080/`.
- Keep host port mappings at `5044` for ProductAPI and `5168` for WebApp unless the task explicitly changes the documented local endpoints.
- ProductAPI stores SQLite data under `/app/Database`; preserve the `productapi-data` named volume and ensure the runtime user can write to that path.
- Keep the image version pin deliberate: select the SUT release with `SUT_TAG` (default `1.0.0`) instead of editing service definitions.
- Validate Compose edits with `docker compose config`; when available, start the stack (`docker compose up -d --wait`) to check cross-service configuration.
