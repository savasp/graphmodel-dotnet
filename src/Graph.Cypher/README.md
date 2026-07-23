**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph.Cypher

`Cvoya.Graph.Cypher` contains the typed Cypher abstract syntax tree, shared planner and renderer,
and `ICypherDialect` provider SPI used by CVOYA Graph query generation. A provider supplies syntax
and capabilities through the dialect; the shared renderer returns text, parameters, and an exact
projection-column schema in `CypherRenderResult`.

The dialect advertises translation capabilities, while provider certification remains the job of
`Cvoya.Graph.CompatibilityTests`. Providers keep driver values and physical entity identity behind
their adapters; neither is part of the public Cypher AST contract.
