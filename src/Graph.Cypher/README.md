<p>
  <a href="https://cvoya.com"><img src="https://cvoya.com/images/cvoya-logo-dark-blue.png" alt="CVOYA" width="160"></a>
</p>

**Downloadable open-source computer software from [CVOYA](https://cvoya.com).** See the
[CVOYA software catalog](https://cvoya.com/software).

# Cvoya.Graph.Cypher

`Cvoya.Graph.Cypher` contains the typed Cypher abstract syntax tree, shared planner and renderer, and `ICypherDialect` provider SPI used by CVOYA graph query generation. A provider supplies syntax and capabilities through the dialect; the shared renderer returns text, parameters, and an exact projection-column schema in `CypherRenderResult`.
