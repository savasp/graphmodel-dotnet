---
---

# 0001 — Shared Cypher translation layer and multi-provider architecture

- **Status:** Accepted (all decisions ratified by @savasp on PR #97; contributor comments on the PR #66 landing path remain welcome via #86)
- **Date:** 2026-07-02
- **Related:** #53, #66, #67, #80, #81, #84, #85, #86, #90, #93, #94, #95, #96
- **Related code:** src/Graph/GraphQueryable/, src/Cvoya.Graph.Neo4j/Querying/

## Context

CVOYA graph has one LINQ-to-Cypher translation pipeline and, as of PR #66, two providers that need it.

The in-tree Neo4j provider translates by string-building: `ExpressionToCypherVisitor`
(src/Cvoya.Graph.Neo4j/Querying/Cypher/Visitors/ExpressionToCypherVisitor.cs, 1,079 lines) returns Cypher
fragments as `Expression.Constant(string)`, and `CypherQueryBuilder`
(src/Cvoya.Graph.Neo4j/Querying/Cypher/Builders/CypherQueryBuilder.cs, 1,216 lines) accumulates raw clause
strings (`AddMatchPattern(string)`, `AddWhere(string)`). Dialect syntax — `datetime()`, `apoc.text.*`,
`db.index.fulltext` calls — is welded inline into LINQ recognition. Terminal operations are dispatched by
string method name in `CypherQueryVisitor`
(src/Cvoya.Graph.Neo4j/Querying/Cypher/Visitors/Core/CypherQueryVisitor.cs, 1,683 lines), matching ~60
public `*AsyncMarker` stubs (src/Graph/GraphQueryable/QueryableAsyncExtensionsMarkers.cs).

PR #66 (@paule96) contributes a complete Apache AGE provider for PostgreSQL (~23k added lines). Because
nothing in the pipeline was reusable, it had to fork the stack (`AgeGraphQueryableBase`,
`AgeGraphQueryProvider` — byte-similar to their Neo4j counterparts) — but its internals are *better* than
what they fork: a typed fragment IR plus a renderer (`AgeQueryFragments`, `AgeFragmentRenderer` under
src/Cvoya.Graph.Age/Querying/Cypher/Visitors/Core/ in the PR) instead of string accumulation, and a thin
shared `src/Cvoya.Graph.Cypher` package that the Neo4j provider does not yet consume.

Without a shared layer, every LINQ feature must be built twice and the two implementations will drift
semantically. The public query surface compounds the cost: the node/relationship distinction is modeled as
an interface hierarchy (`IGraphNodeQueryable<T>`, `IGraphRelationshipQueryable<T>`), which forces ~3,700
lines of shadowed LINQ operators across `GraphQueryableExtensions.cs` (838 lines),
`GraphNodeQueryableExtensions.cs` / `GraphRelationshipQueryableExtensions.cs` (110 lines each), and
`QueryableAsyncExtensions.cs` (1,736 lines) — and the shadowing is incomplete, so `.OrderBy(…).Traverse<…>()`
does not compile (#93 §B4).

A full design review of the surface, operator model, and translation architecture ran as #93 (§A–§F).
@savasp's decisions there (single interface with constraints; markers redesigned as internals; breaking
changes accepted pre-1.0; `IGraphPath` in phase 1) fixed most of the architecture. This ADR records those
decisions, settles the remaining packaging and migration questions, and establishes the repo's ADR
convention (see [README.md](README.md)).

## Decision

### Decided in #93 and its decision comment (recorded here, not re-litigated)

1. **Single queryable interface, operators gated by constraints.** The public surface collapses to
   `IGraphQueryable<T>` + `IOrderedGraphQueryable<T>`. Graph operators are gated by generic constraints
   (`where TStart : INode`, `where T : IRelationship`), not receiver interfaces. The node/relationship
   interface hierarchy is retired (kept for one release as aliases, then removed by #240). This deletes the triple-shadowed
   operator layers and fixes the ordering-degradation bug by construction. Rationale: the hierarchy's only
   job was operator gating, which constraints do with zero shadowing (#93 §C1). Implementation: #94.

2. **Marker methods become internals with `MethodInfo`-identity dispatch.** The ~60 public `*AsyncMarker`
   stubs are replaced by a non-extension internal `QueryTerminals` class; providers dispatch on
   `MethodInfo` identity from a shared table — no string-name dispatch anywhere (#93 §C2). The numeric
   `Sum`/`Average` overload triplication collapses to one definition. Implementation: #94.

3. **Breaking changes are accepted pre-1.0; `IGraphPath` lands in phase 1.** `IGraphPath` (`Start`, `End`,
   ordered `Segments`) becomes the result type of variable-length traversals (`TraversePaths(...)`);
   `IGraphPathSegment<S,R,T>` remains the single-hop case. Sync query roots, `IAsyncEnumerable<T>`, and the
   traversal-ergonomics changes ride the same breaking window (#94, with #76/#73 scheduled adjacent per the
   #90 plan) so consumers absorb one breaking release, not three.

4. **Two-level IR.** Level 1 is a semantic **`GraphQueryModel`** — what the query *means*: root
   (node/relationship/dynamic/search), predicates, ordered traversal steps (relationship type, direction,
   depth, relationship predicates), projection shape, ordering/paging, terminal operation. It is produced by
   the shared LINQ front-end (the recognition halves of `CypherQueryVisitor`/`ExpressionToCypherVisitor`,
   dispatching on `MethodInfo`), and it is where new operators bind — once. A shared **`CypherQueryPlanner`**
   lowers it to level 2, a typed **Cypher fragment AST** (clause fragments + expression nodes instead of
   strings), rendered per dialect through **`ICypherDialect`** — function mappings, full-text strategy,
   label/ID/parameter syntax, and a capability surface that fails unsupported constructs at translation time
   with an error naming the construct and dialect. A future non-Cypher provider (Gremlin, SQL) targets
   level 1 and skips level 2 entirely. This amends the original single-IR scope of #84/#85 per #93 §E.

5. **Neutral result wire model.** Provider-independent graph values (node/relationship/path/scalar/list/map
   records). The hard materialization logic of `CypherResultProcessor`
   (src/Cvoya.Graph.Neo4j/Querying/Cypher/Execution/CypherResultProcessor.cs, 1,173 lines — complex-property
   reassembly, polymorphic type resolution, path stitching) moves into shared code operating on the wire
   model; each provider shrinks to a driver→wire-model adapter (#85).

6. **`tests/Cvoya.Graph.Tests` ships as a provider compatibility suite** with a harness SPI
   (create/tear down store, produce `IGraph`, per-test isolation) and a capability model aligned 1:1 with
   the `ICypherDialect` capability surface, so "dialect says unsupported" and "suite skips-with-reason" can
   never disagree. The in-tree Neo4j tests consume it exactly as an external provider would (#95).

7. **Adopt and generalize PR #66's fragment/renderer design** for the level-2 AST rather than inventing a
   third design. @paule96's `AgeQueryFragments` + `AgeFragmentRenderer` split independently arrived at the
   right shape — typed fragments rendered per dialect — and is the design input for #84. Credit belongs in
   the #84 PR descriptions as well as here.

### Settled by this ADR (ratified by @savasp on PR #97)

**(a) Package layout: fold the shared querying front-end into `Graph`; the shared Cypher package is
`Cvoya.Graph.Cypher`.** The #93 §A decomposition left open whether the shared queryable implementations,
provider base, and LINQ front-end live in core or a separate `Cvoya.Graph.Querying`. Decision: fold
into `Graph`. The operator surface and the front-end that recognizes it are one release unit — the
`MethodInfo` dispatch table binds directly to operator identities defined in the same assembly, so a
separate package would version in lockstep anyway (pure SemVer coupling with no independent consumer) while
adding a second dependency for every provider and an `InternalsVisibleTo` seam for the semantic model's
internals. Folding keeps `GraphQueryModel` internals internal and gives non-Cypher providers a
single-package dependency. The level-2 package keeps PR #66's name, `Cvoya.Graph.Cypher` (NuGet ID
`Cvoya.Graph.Cypher`, matching the existing ID convention): it is accurate — the package contains the
Cypher AST, planner, dialect SPI, and renderer base — and reusing the contributed name minimizes friction
with #66. Alternatives (`Cvoya.Graph.OpenCypher`, `Cvoya.Graph.Cypher.Core`) add no information. The shared
wire-model/materialization code (#85) follows the same folding logic into `Graph`, since a non-Cypher
provider needs it too; final placement is an #85 implementation detail.

**(b) `Cvoya.Graph.Cypher` ships as a public NuGet package.** Decision: public from the release in
which #84 lands, not internal shared source. The multi-provider story is the point of this ADR; an external
Cypher-dialect provider (and eventually AGE, whether in-tree or out) needs the planner/dialect SPI as a
package, and #95 already commits to shipping a provider-author-facing package — a provider SPI without the
translation SPI is half a story. The stability concern is moot pre-1.0: the whole library is 0.x, breaking
changes are accepted (#93 decision), and holding the package back buys no stability a `0.x` version number
doesn't already disclaim. Document it as an SPI package that versions in lockstep with `Cvoya.Graph`.

**(c) Compatibility-suite package name: `Cvoya.Graph.CompatibilityTests`.** As per the #95 working
name. "CompatibilityTests" is self-describing on nuget.org where "TCK" is jargon, and the ID sorts with the
package family it certifies.

**(d) PR #66 landing path: stage, then converge (path (a) of #86).** #86 lists three candidates:
(a) merge nothing until the shared layer lands, then converge the AGE provider as dialect + adapter;
(b) merge now as an explicitly-experimental preview package; (c) split — land the shareable pieces first,
hold the fork pipeline. Decision: **(a)**, because the now-fixed plan removes what (b) and (c) would
buy. Merging the fork now (b) means merging ~40 classes that #94 breaks (surface rewrite) and #84/#85 then
delete (the pipeline it forks), maintaining a second CI lane through that churn, with 864 lines of
self-reported security issues (docs/age/issues/security-issues.md in the PR) still to triage. Splitting (c)
has lost its payload: the genuinely shareable pieces are being absorbed by design rather than by merge —
the fragment/renderer approach becomes the #84 AST (decision 7), the PR's `Cvoya.Graph.Cypher` seed is
superseded by that package's two-level rebuild, and the AGE test fixture's natural home is as the second
harness of the #95 suite once that SPI exists. Under the converged architecture the AGE provider becomes an
`AgeDialect` + Npgsql/AGE wire-model adapter + store/transaction layer — a fraction of the current 23k
lines. Mitigation for contributor friction, per #86: maintainers do the conversion collaboratively with
@paule96, the security triage happens as part of that review, and #86 posts a concrete "what mergeable
means" checklist on #66/#53.

## Consequences

- **Ordered migration path** (track B of the #90 execution plan; hard ordering encoded as blocked-by
  links): #80 (characterization/snapshot safety net) → #94 (query surface v2 — the one big breaking PR)
  → #84 (shared front-end producing `GraphQueryModel`; planner; Cypher AST; Neo4j cutover with
  byte-identical snapshots) → #85 (`ICypherDialect` + wire model) → #95 (compatibility suite packaging) →
  #86 (AGE convergence) / #96 (new operators, one vertical slice each). #81 closes when this ADR is
  Accepted.
- **Testing policy** (binding, per the #90 plan): every implementation PR ships both provider-free unit
  tests (expression-shape, IR-structure, Cypher-snapshot — no Docker) and integration coverage through the
  compatibility suite so all providers inherit it. Snapshot diffs are reviewed individually; intentional
  Cypher changes are one commit each with justification. CI must report a non-trivial executed-test count
  per lane — green-with-zero-tests is a failure. New operators land with capability-gated tests so
  non-supporting providers skip-with-reason, never fail or silently pass.
- **Impact on PR #66 / @paule96:** the fork pipeline as posted will not merge; the convergence target is
  `AgeDialect` + result adapter + store layer + compatibility harness once #85/#95 land, done
  collaboratively. The fragment/renderer design is adopted upstream with credit (decision 7). AGE's known
  capability gaps (nested transactions, full-text search — #53) become declared capabilities: translation
  fails informatively and suite tests skip-with-reason instead of failing. Security-issue triage from the
  PR's own docs happens during #86. Concrete checklist lands on #66/#53 per #86.
- **New shipped packages:** `Cvoya.Graph.Cypher` and `Cvoya.Graph.CompatibilityTests` join the
  release pipeline (#71). Both are 0.x and version in lockstep with the core.
- **Cost per new operator drops to one binding:** surface + `GraphQueryModel` node + planner lowering +
  per-dialect rendering + capability entry + suite tests (#96) — instead of one full visitor-stack
  implementation per provider.
- **Funcletization is bounded and structurally allowlisted:** the shared front-end evaluates only
  parameter-free expression subtrees at translation time. That includes closure-member reads and
  parameterless method calls, matching standard LINQ-provider behavior; an expression that references a
  query parameter is never compiled and invoked. This is deliberately not a method/type sandbox: query
  authors already execute application code while constructing expression trees, so the policy prevents
  accidental evaluation of server-side expressions without pretending to isolate trusted application
  code. Translation rejects trees over 10,000 nodes or 100 levels deep by default; internal callers may
  override both limits for generated queries.
- **Breaking-change window:** consumers absorb one breaking release (#94, with #76/#73 adjacent), with
  `docs/migration-0.x.md` covering every break.
- The provider guide (#82) grows a "Certifying a provider" chapter driven by the compatibility suite.

## Alternatives considered

- **Two independent providers sharing only contract tests and a conventions doc.** Rejected. This is the
  status quo PR #66 demonstrates: a ~40-class byte-similar fork, every LINQ feature built twice, and
  semantic drift bounded only by test coverage — which the contract suite cannot fully provide, since
  translation nuances (null semantics, depth boundaries, aggregation edge cases) surface as subtle result
  differences, not failures. The fork tax also falls on every future provider.
- **Single-level, Cypher-shaped IR** (the original #84 plan). Rejected in favor of the two-level IR (#93
  §E): with only a Cypher AST, new operators bind to Cypher syntax, every operator addition touches all
  renderers directly, and a non-Cypher provider gets nothing. The semantic level is where operator meaning
  lives once; lowering is shared; only rendering is per-dialect.
- **Keep the interface hierarchy and complete the shadowing.** Rejected (#93 §C1): completing ~3,700 lines
  of shadow operators (and maintaining the matrix forever, for every new operator times every interface)
  treats the symptom; constraints on a single interface remove the disease.
- **A fresh fragment-IR design, ignoring PR #66's.** Rejected: `AgeQueryFragments`/`AgeFragmentRenderer`
  already validate the fragments + per-dialect-renderer shape in a working provider; a third design would
  re-derive the same structure while discarding both the validation and the contribution.
- **Separate `Cvoya.Graph.Querying` package** — see decision (a); rejected
  (SemVer-coupled with no independent consumer).
- **Hold `Cvoya.Graph.Cypher` as internal shared source** — see decision (b); rejected
  (pre-1.0 versioning already disclaims the stability that internalizing would protect).
