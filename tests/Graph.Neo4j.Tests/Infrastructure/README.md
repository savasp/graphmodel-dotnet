# Neo4j test database pools

Enterprise Neo4j test runs create databases named `graphtests-<run-id>-<index>`. The run ID
isolates concurrent test processes, and a normal teardown drops only that process's databases.
Community Neo4j, including the CI service, does not support multiple databases and continues to
use and clean only the configured default database.

A killed test process can leave its namespaced databases behind. This is intentionally not cleaned
up automatically because another process may still own them. When no Neo4j tests are running, list
the orphan candidates in the `system` database:

```cypher
SHOW DATABASES YIELD name WHERE name STARTS WITH 'graphtests-' RETURN name;
```

Review the results, then remove each orphan, escaping the hyphenated name:

```cypher
DROP DATABASE `graphtests-<run-id>-<index>` IF EXISTS;
```
