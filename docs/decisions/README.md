# Architecture decision records

This directory holds the project's architecture decision records (ADRs): short documents that capture one
significant architectural decision each — the context that forced it, the decision itself, and its
consequences — so the reasoning survives after the discussion threads go cold. Records are numbered
sequentially (`NNNN-short-title.md`, starting at 0001) and never renumbered. Each record carries a status:
**Proposed** (open for comment), **Accepted** (the decision stands and implementation may proceed), or
**Superseded** (replaced by a later record, which it links to). Records are immutable once Accepted — a
changed decision gets a new ADR that supersedes the old one.

## Index

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-shared-cypher-translation-layer.md) | Shared Cypher translation layer and multi-provider architecture | Proposed |
