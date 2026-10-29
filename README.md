## Settings template for new microservices

## Deploying
This project should be deployed within a container. 
### Configuration
See appsettings.json

### External Cassandra PEM migration

Core 0.7.5 supports `CASSANDRA__TLS__CA_CERT_PATH`,
`CASSANDRA__TLS__CERT_PATH`, `CASSANDRA__TLS__KEY_PATH` and
`CASSANDRA__TLS__SERVER_NAME` for external CQL connections. Retain existing hosts,
keyspace, user and password. The PEM section takes precedence over legacy PFX
configuration and validates the configured roots and server name.

Mount the complete client certificate chain, its key and the shared CA bundle,
without `subPath`, so renewal updates reach the process. New physical connections
reload PEM identities and trust; established sessions continue uninterrupted.
Core registers validated client intermediates in the workload user's CA store,
without changing machine trust or adding roots to that store. Provide a writable,
private `HOME` for this store (the container defaults to `/tmp`), retain overlapping
trust roots during CA changes, and verify a fresh connection after renewal.

Internal HTTP 8000, generated-client paths, `REDIS_HOST` connection options and
Redis publication/subscription behavior remain unchanged. Internal TLS activation
is deferred; managed in-cluster traffic uses the existing Cilium WireGuard layer.

Startup and every `GET /health/ready` perform a fresh Cassandra
`SELECT key FROM system.local` and Redis PING with a shared five-second deadline.
Readiness returns 200 on success and 503 on failure; startup fails if its check fails.
These checks do not write settings or publish notifications. Verify application reads
and notification delivery separately before advancing the rollout.

Before HTTP starts, a separate Kafka transport check makes one all-topics metadata
request and checks only the configured `TOPICS__SETTINGS` locally. No topic name is
sent in the request, avoiding implicit topic creation. The metadata timeout is three
seconds, with a five-second overall deadline. It uses existing endpoint/SASL configuration and
`KAFKA__TLS__CA_LOCATION`, `CERTIFICATE_LOCATION`, `KEY_LOCATION` file paths, with
strict CA and hostname validation. Errors expose only a generic type or Kafka code.
Recurring readiness does not repeat Kafka handshakes. This startup check proves neither
consumer group progress, delivery nor certificate renewal; retain the separate native
upgrade/consumer-only reload and delivery gates.

The Docker build runs `dotnet test -c Debug` before publishing Release; test sources
and test dependencies are excluded from the runtime publication.

