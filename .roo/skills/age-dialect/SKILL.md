---
name: age-dialect
description: "Expert-level reference for the PostgreSQL AGE graph extension dialect (openCypher embedded in SQL). Use this skill when the task involves writing, reviewing, or understanding AGE-specific SQL/Cypher syntax; the agtype data type system; AGE operators and precedence; all Cypher clauses (MATCH, CREATE, MERGE, DELETE, SET, REMOVE, RETURN, WITH, ORDER BY, LIMIT, SKIP, UNWIND); aggregation semantics; the complete function catalog; advanced usage (CTEs, JOINs, SQL expressions, PL/pgSQL, prepared statements, multiple graphs, agload); comparability/equality/orderability/equivalence distinctions; or integration with the GraphModel .NET library. Do not use for standard Neo4j Cypher, for general PostgreSQL without AGE, or for non-graph-relational database tasks."
---

# PostgreSQL AGE Dialect Expert Reference

Use this skill when you need expert-level understanding of Apache AGE — a PostgreSQL extension that embeds the openCypher graph query language inside SQL. This skill covers the AGE-specific SQL/Cypher hybrid syntax, the [`agtype`](#c-the-agtype-data-type-system) type system, all operators and clauses, the complete function catalog, advanced usage patterns, and how AGE differs from standard Cypher.

> **Source documentation** is available in the [Apache AGE Manual](https://age.apache.org/age-manual/master/index.html). The major sections are:
> - [`intro/`](https://age.apache.org/age-manual/master/intro/) — setup, types, operators, precedence, comparability, graphs, Cypher format, aggregation, agload
> - [`advanced/`](https://age.apache.org/age-manual/master/advanced/) — CTEs, JOINs, SQL expressions, PL/pgSQL, prepared statements, SQL-in-Cypher
> - [`scripts/`](https://age.apache.org/age-manual/master/scripts/) — setup SQL scripts

---

## A. What PostgreSQL AGE Is

**Apache AGE** (A Graph Extension) is a PostgreSQL extension that adds graph database capabilities to PostgreSQL. It integrates the **openCypher** query language inside SQL, allowing users to work with both relational and graph data models in a single database.

Key architectural concepts:

- **`ag_catalog` schema**: All AGE functions, types, and metadata tables live in the `ag_catalog` schema. This includes [`create_graph()`](#creating-a-graph), [`drop_graph()`](#deleting-a-graph), [`create_vlabel()`](#creating-labels), [`create_elabel()`](#creating-labels), and the [`cypher()`](#b-cypher-function) query function.
- **`agtype` data type**: The universal return type for all Cypher query output. It is a superset of JSON and a custom implementation of JSONB. Every column returned by `cypher()` must be declared as `agtype`.
- **Graph-as-namespace storage model**: Each graph you create gets its own PostgreSQL schema (namespace). Two tables are created per graph — `_ag_label_vertex` and `_ag_label_edge` — which serve as parent tables. Vertex and edge labels become child tables under the graph's namespace.

---

## B. Core Syntax & Setup

### Loading the Extension

Every session that uses AGE must load the extension and set the search path:

```sql
LOAD 'age';
SET search_path = ag_catalog, "$user", public;
```

> **Note**: Non-superusers need a symlink to `age.so` in the plugins directory and `USAGE` privilege on `ag_catalog`.

### Creating and Deleting Graphs

```sql
-- Create a graph
SELECT * FROM ag_catalog.create_graph('graph_name');

-- Delete a graph (cascade recommended to clean up all labels/data)
SELECT * FROM ag_catalog.drop_graph('graph_name', true);
```

### Creating Labels

```sql
-- Create vertex label
SELECT create_vlabel('graph_name', 'Person');

-- Create edge label
SELECT create_elabel('graph_name', 'KNOWS');
```

Labels are automatically created when you use `CREATE` in Cypher. The `create_vlabel()`/`create_elabel()` functions are used for explicit pre-creation (e.g., before bulk loading).

### The `cypher()` Function

Cypher queries are embedded in SQL using the `cypher()` function in the `FROM` clause:

```sql
SELECT * FROM cypher('graph_name', $$
    -- Cypher query here
$$) AS (col1 agtype, col2 agtype);
```

The `$$` delimiter (dollar-quoting) is used to wrap the Cypher query, avoiding escaping issues with single quotes inside Cypher strings.

**Important**: Every `cypher()` call **must** have a column definition list with `agtype` types, even if the query returns no rows.

### The Three Arguments of `cypher()`

1. `graph_name` — the target graph name (string literal)
2. `query_string` — the Cypher query (dollar-quoted)
3. `parameters` — an optional agtype map for prepared statements (default NULL)

```sql
-- With parameters (only works with prepared statements)
SELECT * FROM cypher('graph_name', $$
    MATCH (v:Person) WHERE v.name = $name RETURN v
$$, '{"name": "Tobias"}') AS (v agtype);
```

### Critical Restrictions

- `cypher()` **cannot** be used in the `SELECT` clause as an independent column — it must be in `FROM`.
- `cypher()` **cannot** be used in expressions directly — use subqueries instead (see [Cypher in SQL Expressions](#using-cypher-in-sql-expressions)).

---

## C. The `agtype` Data Type System

`agtype` is the only data type returned by AGE. It is a superset of JSON and a custom implementation of JSONB.

### Simple Types

| Type | Description | Example |
|------|-------------|---------|
| **null** | Missing/undefined value. `null = null` yields `null` (not true). |  |
| **integer** | 64-bit signed integer (-2^63 to 2^63-1) | `RETURN 1` |
| **float** | IEEE-754 double-precision. Supports `Infinity`, `-Infinity`, `NaN` | `RETURN 1.0` |
| **numeric** | Arbitrary precision. Must use `::numeric` cast. Preferred for monetary amounts. | `RETURN 1.0::numeric` |
| **boolean** | `true` or `false`. Unknown represented by null. Outputs full words (`true`/`false`), not `t`/`f`. | `RETURN TRUE` |
| **string** | Single-quoted in input, double-quoted in output. Supports escape sequences: `\t`, `\b`, `\n`, `\r`, `\f`, `\'`, `\"`, `\\`, `\uXXXX`. | `RETURN 'Hello'` |

### Composite Types

**List**: Ordered collection of values. Supports:
- Zero-based indexing: `lst[3]`
- Negative indexing (from end): `lst[-3]`
- Slicing: `lst[0..3]`, `lst[0..-5]`, `lst[..4]`, `lst[-5..]`
- Out-of-bound slices are truncated; out-of-bound single elements return null
- Nested elements: `lst[1].key`

**Map**: Key-value collection (keys are strings).
- Property access: `m.key_name`
- Nested access: `m.listKey[0]`
- Keys are sorted alphabetically in output

### Graph Entities

**Vertex** (`::vertex`):
```
{id: 1; label: 'Person'; properties: {name: "John", age: 30}}::vertex
```

| Attribute | Description |
|-----------|-------------|
| `id` | graphid (unique within graph) |
| `label` | Name of the vertex label |
| `properties` | Map of property key-value pairs |

**Edge** (`::edge`):
```
{id: 3; start_id: 1; end_id: 2; label: 'KNOWS'; properties: {since: 2020}}::edge
```

| Attribute | Description |
|-----------|-------------|
| `id` | graphid for this edge |
| `start_id` | graphid of the source node |
| `end_id` | graphid of the target node |
| `label` | Name of the edge label |
| `properties` | Map of property key-value pairs |

**Path** (`::path`): A series of alternating vertices and edges. Must start with a vertex and have at least one edge.
```
[{...}::vertex, {...}::edge, {...}::vertex]::path
```

### Comparability, Equality, Orderability, Equivalence

AGE defines four distinct concepts:

| Concept | Used By | Key Behavior |
|---------|---------|-------------|
| **Comparability** | `<`, `>`, `<=`, `>=` | Defined across all types. Numbers compared as arbitrary-precision big decimals. |
| **Equality** | `=`, `<>`, `IN` | `null = null` yields `null`. |
| **Orderability** | `ORDER BY` | Across types, ordering hierarchy applies (see below). |
| **Equivalence** | `DISTINCT`, `GROUP BY` | Two nulls treated as same; used for grouping. |

**Type ordering hierarchy** (smallest to largest for cross-type comparison):
1. Path
2. Edge
3. Vertex
4. Map (Object)
5. List (Array)
6. String
7. Boolean
8. Numeric / Integer / Float
9. NULL

---

## D. All Operators (with Precedence)

| Precedence | Operator | Description |
|:----------:|----------|-------------|
| 1 (highest) | `.` | Property access |
| 2 | `[]` | Map/list subscripting |
| 2 | `()` | Function call |
| 3 | `STARTS WITH` | Case-sensitive prefix matching |
| 3 | `ENDS WITH` | Case-sensitive suffix matching |
| 3 | `CONTAINS` | Case-sensitive substring matching |
| 3 | `=~` | POSIX regular expression matching |
| 4 | `-` | Unary minus (negation) |
| 5 | `IN` | List membership |
| 5 | `IS NULL` | Null check |
| 5 | `IS NOT NULL` | Not-null check |
| 6 | `^` | Exponentiation |
| 7 | `*` `/` `%` | Multiplication, division, remainder |
| 8 | `+` `-` | Addition, subtraction, string concatenation |
| 9 | `=` `<>` | Relational equality and inequality |
| 9 | `<` `<=` `>` `>=` | Relational comparison |
| 10 | `NOT` | Logical negation |
| 11 | `AND` | Logical conjunction |
| 12 | `OR` | Logical disjunction |

### String operators in detail

```sql
-- STARTS WITH: case-sensitive prefix
WHERE v.name STARTS WITH "J"

-- ENDS WITH: case-sensitive suffix
WHERE v.name ENDS WITH "n"

-- CONTAINS: case-sensitive inclusion
WHERE v.name CONTAINS "o"

-- =~ : POSIX regex (case-insensitive with (?i))
WHERE v.name =~ 'Jo.n'       -- single char wildcard
WHERE v.name =~ 'Johz*n'     -- zero or more of preceding char
WHERE v.name =~ 'Bil+'       -- one or more of preceding char
WHERE v.name =~ 'J.*'        -- any string starting with J
WHERE v.name =~ '(?i)John'   -- case-insensitive
```

---

## E. All Clauses — Complete Reference

### MATCH

Pattern matching for traversing the graph:

```sql
-- Match all vertices
MATCH (v)

-- Match with label
MATCH (v:Person)

-- Match with label and property filter
MATCH (v:Person {name: 'John'})

-- Directed edge
MATCH (a:Person)-[e:KNOWS]->(b:Person)

-- Undirected edge
MATCH (a:Person)-[e:KNOWS]-(b:Person)

-- Variable-length path
MATCH (a:Person)-[*1..3]->(b:Person)

-- Path assignment
MATCH p = (a:Person)-[:KNOWS*1..3]->(b:Person)
RETURN p
```

Named paths (`p = (...)-->()`) are of type `::path` and can be returned, used in functions, or further traversed.

### CREATE

Creating vertices, edges, and setting properties:

```sql
-- Single vertex
CREATE (:Person {name: 'John', age: 30})

-- Vertex and edge in one statement
CREATE (a:Person {name: 'Alice'})-[:KNOWS {since: 2020}]->(b:Person {name: 'Bob'})

-- Relationship property with expressions
CREATE (a)-[:KNOWS {since: date('2020-01-01')}]->(b)
```

### MERGE

Match-or-create semantics. The entire pattern must match for no creation to occur (no partial matching):

```sql
-- Match vertex by property; create if not found
MERGE (v:Person {name: 'John'})

-- Match-on-property, then SET additional properties
MERGE (v:Person {name: 'John'})
ON MATCH SET v.lastSeen = timestamp()
ON CREATE SET v.created = timestamp()
```

### DELETE / DETACH DELETE

```sql
-- Delete a vertex (fails if it has edges)
DELETE v

-- Delete edges only
DELETE e

-- Delete vertex and all its edges (cascade)
DETACH DELETE v

-- Delete all vertices and edges
DETACH DELETE n
```

### SET

Setting and removing properties:

```sql
-- Set a property
SET v.property = value

-- Set multiple properties
SET v = {name: 'New Name', age: 25}

-- Remove a property (set to null)
SET v.property = NULL

-- Mutating properties with +=
SET v += {lastName: 'Smith'}
```

### REMOVE

```sql
-- Remove a property
REMOVE v.property
```

### RETURN

```sql
-- Return specific columns (must match column definition in AS clause)
RETURN v.name, v.age

-- Aliasing
RETURN v.name AS person_name

-- Expressions
RETURN v.name + ' (' + v.age + ')'

-- Pattern expressions
RETURN size((v)-[:KNOWS]->())

-- All columns
RETURN *

-- Distinct values
RETURN DISTINCT v.label
```

The column definition in the outer SQL `AS` clause must list columns in the same order as `RETURN`:

```sql
SELECT * FROM cypher('g', $$
    MATCH (n) RETURN n.name, n.age
$$) AS (name agtype, age agtype);
```

### WITH

Pipes data between query parts. Used for filtering aggregates, limiting branching, or preparing data before further matching:

```sql
MATCH (v:Person)
WITH v, v.age AS age
WHERE age > 25
RETURN v.name, age

-- Aggregation in WITH
MATCH (v:Person)
WITH v.eyes AS eye_color, count(*) AS cnt
WHERE cnt > 1
RETURN eye_color, cnt
```

### ORDER BY

```sql
-- Sort by property
RETURN v.name ORDER BY v.name

-- Descending
RETURN v.name ORDER BY v.name DESC

-- Multiple keys
RETURN v.name, v.age ORDER BY v.age DESC, v.name

-- NULL handling (nulls sort last by default)
ORDER BY v.age DESC NULLS LAST
```

### LIMIT and SKIP

```sql
-- Limit rows
RETURN v.name LIMIT 10

-- Skip rows
RETURN v.name SKIP 5

-- Combined
RETURN v.name SKIP 5 LIMIT 10

-- Any expression is valid
RETURN v.name LIMIT 5 * 2
```

### UNWIND

Expands a list into individual rows:

```sql
-- Basic unwinding
WITH [1, 2, 3] AS lst
UNWIND lst AS element
RETURN element

-- NULL handling: UNWIND NULL yields no rows
UNWIND NULL AS x RETURN x  -- no rows

-- Empty list: UNWIND [] yields no rows
UNWIND [] AS x RETURN x    -- no rows

-- UNWIND with nested access
WITH [{name: 'Alice'}, {name: 'Bob'}] AS people
UNWIND people AS person
RETURN person.name

-- UNWIND with path functions
MATCH p = (a)-[:KNOWS*1..3]->(b)
UNWIND nodes(p) AS n
RETURN n
```

---

## F. Aggregation

### Auto Group By

Cypher uses automatic grouping: non-aggregate expressions in `RETURN` or `WITH` become implicit grouping keys:

```sql
MATCH (v:Person)
RETURN v.name, count(*)  -- v.name is the grouping key
```

### Aggregation Functions

| Function | Description |
|----------|-------------|
| `min(expr)` | Minimum value |
| `max(expr)` | Maximum value |
| `avg(expr)` | Average (numeric) |
| `sum(expr)` | Sum (numeric) |
| `count(expr)` | Count non-null values |
| `count(*)` | Count all rows |
| `stDev(expr)` | Sample standard deviation |
| `stDevP(expr)` | Population standard deviation |
| `percentileCont(expr, percentile)` | Continuous percentile |
| `percentileDisc(expr, percentile)` | Discrete percentile |

### DISTINCT Aggregation

```sql
RETURN count(DISTINCT v.eyes), count(v.eyes)
```

### Ambiguous Grouping Rules

AGE requires that in a `WITH` or `RETURN` containing both aggregate and non-aggregate expressions, any variable used in a non-aggregate expression must either be:
- Explicitly listed as a grouping key column, OR
- Used inside an aggregate function

**Invalid** (variable `x` not explicitly listed as a key):
```sql
RETURN x.a + count(*) + x.b
```

**Valid** (explicit grouping key):
```sql
RETURN x.a + count(*) + x.b + x.c, x.a, x.b, x.c
```

**Also valid** (grouping key is the vertex itself):
```sql
RETURN count(*) + x.a, x
```

---

## G. All Functions — Complete Catalog

### Scalar Functions

| Function | Description |
|----------|-------------|
| `id(vertex_or_edge)` | Returns the graph ID |
| `start_id(edge)` | Returns the source node ID |
| `end_id(edge)` | Returns the target node ID |
| `type(edge)` | Returns the edge label as a string |
| `properties(entity)` | Returns the properties map |
| `head(list)` | Returns the first element of a list |
| `last(list)` | Returns the last element of a list |
| `length(path)` | Returns the length of a path (number of edges) |
| `size(string)` | Returns the number of characters in a string |
| `size(list)` | Returns the number of elements in a list |
| `size(pattern_expression)` | Returns the number of matching paths |
| `startNode(path)` | Returns the start node of a path |
| `endNode(path)` | Returns the end node of a path |
| `timestamp()` | Returns current time as a Unix timestamp (milliseconds) |
| `toBoolean(expr)` | Converts a value to boolean |
| `toFloat(expr)` | Converts a value to float |
| `toInteger(expr)` | Converts a value to integer |
| `coalesce(expr1, expr2, ...)` | Returns the first non-null value |

### List Functions

| Function | Description |
|----------|-------------|
| `keys(map)` | Returns the keys of a map as a list of strings |
| `range(start, end [, step])` | Returns a list of integers from start to end (inclusive) |
| `labels(vertex)` | Returns the labels of a vertex as a list of strings |
| `nodes(path)` | Returns the nodes (vertices) of a path |
| `relationships(path)` | Returns the relationships (edges) of a path |
| `toBooleanList(list)` | Converts each element in a list to boolean |

### Numeric Functions

| Function | Description |
|----------|-------------|
| `rand()` | Returns a random float in [0, 1) |
| `abs(value)` | Absolute value |
| `ceil(value)` | Ceiling (rounds up to nearest integer) |
| `floor(value)` | Floor (rounds down to nearest integer) |
| `round(value)` | Rounds to nearest integer |
| `sign(value)` | Returns -1, 0, or 1 |

### String Functions

| Function | Description |
|----------|-------------|
| `replace(string, search, replace)` | Replaces occurrences of search string |
| `split(string, delimiter)` | Splits string into list of strings |
| `left(string, n)` | Returns the first n characters |
| `right(string, n)` | Returns the last n characters |
| `substring(string, start [, length])` | Returns substring |
| `rTrim(string)` | Removes trailing whitespace |
| `lTrim(string)` | Removes leading whitespace |
| `trim(string)` | Removes leading and trailing whitespace |
| `toLower(string)` | Converts to lowercase |
| `toUpper(string)` | Converts to uppercase |
| `reverse(string)` | Reverses string |
| `toString(expr)` | Converts value to string |

### Logarithmic Functions

| Function | Description |
|----------|-------------|
| `e()` | Returns the base of natural logarithms |
| `sqrt(value)` | Square root |
| `exp(value)` | Exponential (e^value) |
| `log(value)` | Natural logarithm |
| `log10(value)` | Base-10 logarithm |

### Trigonometric Functions

| Function | Description |
|----------|-------------|
| `degrees(radians)` | Converts radians to degrees |
| `radians(degrees)` | Converts degrees to radians |
| `pi()` | Returns the mathematical constant pi |
| `sin(angle)` | Sine |
| `cos(angle)` | Cosine |
| `tan(angle)` | Tangent |
| `cot(angle)` | Cotangent |
| `asin(value)` | Arc sine |
| `acos(value)` | Arc cosine |
| `atan(value)` | Arc tangent |
| `atan2(y, x)` | Arc tangent of y/x |

### Predicate Functions

| Function | Description |
|----------|-------------|
| `exists(property)` | Returns true if the property exists on the entity (e.g., `exists(v.name)`) |
| `EXISTS(path_expression)` | Returns true if the path pattern has at least one match |

### Map Functions

| Function | Description |
|----------|-------------|
| `vertex_stats()` | Returns graph statistics as a map of vertex counts |

### User-Defined Functions (Calling SQL from Cypher)

You can call PostgreSQL functions from Cypher using the `pg_catalog.` namespace:

```sql
-- Create a SQL function
CREATE OR REPLACE FUNCTION public.get_event_year(name agtype) RETURNS agtype AS $$
    SELECT year::agtype FROM history AS h WHERE h.event_name = name::text LIMIT 1;
$$ LANGUAGE sql;

-- Call it from Cypher
SELECT * FROM cypher('graph_name', $$
    MATCH (e:event)
    WHERE e.year < public.get_event_year(e.name)
    RETURN e.name
$$) AS (n agtype);
```

> **Note**: Only void and scalar-value functions are supported. Set-returning functions are not currently supported.

---

## H. Advanced Features

### Using Cypher in CTEs

```sql
WITH graph_query AS (
    SELECT * FROM cypher('graph_name', $$
        MATCH (n) RETURN n.name, n.age
    $$) AS (name agtype, age agtype)
)
SELECT * FROM graph_query;
```

### Using Cypher in JOINs

```sql
SELECT sql_person.name, graph_query.age
FROM schema_name.sql_person AS t
JOIN cypher('graph_name', $$
    MATCH (n:Person) RETURN n.name, n.age
$$) AS graph_query(name agtype, age agtype)
ON t.person_name = graph_query.name;
```

**Restriction**: Write clauses (`CREATE`, `SET`, `REMOVE`) cannot be used in JOINs directly. Wrap them in CTEs as a workaround.

### Using Cypher in SQL Expressions

**Equality subquery** (single column, single row):
```sql
WHERE t.name = (SELECT a FROM cypher('g', $$ MATCH (v) RETURN v.name $$) AS (a agtype) LIMIT 1)
```

**IN subquery** (single column, multiple rows):
```sql
WHERE t.name IN (SELECT * FROM cypher('g', $$ MATCH (v:Person) RETURN v.name $$) AS (a agtype))
```

**EXISTS subquery** (multiple columns):
```sql
WHERE EXISTS (
    SELECT * FROM cypher('g', $$ MATCH (v:Person) RETURN v.name, v.age $$) AS (name agtype, age agtype)
    WHERE name = t.name AND age = t.age
)
```

### Querying Multiple Graphs Simultaneously

```sql
SELECT graph_1.name, graph_1.age, graph_2.license_number
FROM cypher('graph_1', $$ MATCH (v:Person) RETURN v.name, v.age $$) AS graph_1(name agtype, age agtype)
JOIN cypher('graph_2', $$ MATCH (v:Doctor) RETURN v.name, v.license_number $$) AS graph_2(name agtype, license_number agtype)
ON graph_1.name = graph_2.name;
```

### PL/pgSQL Functions with Cypher

```sql
CREATE OR REPLACE FUNCTION get_all_actor_names()
RETURNS TABLE(actor agtype)
LANGUAGE plpgsql
AS $BODY$
BEGIN
    LOAD 'age';
    SET search_path TO ag_catalog;

    RETURN QUERY
    SELECT * FROM ag_catalog.cypher('imdb', $$
        MATCH (v:actor) RETURN v.name
    $$) AS (a agtype);
END
$BODY$;
```

**Important**: Always include `LOAD 'age'` and `SET search_path TO ag_catalog` inside the function body to ensure consistent behavior.

**Dynamic Cypher** using `EXECUTE`:
```sql
CREATE OR REPLACE FUNCTION get_actors_who_played_role(role agtype)
RETURNS TABLE(actor agtype, movie agtype)
LANGUAGE plpgsql
AS $function$
DECLARE sql VARCHAR;
BEGIN
    LOAD 'age';
    SET search_path TO ag_catalog;
    sql := format('
        SELECT * FROM cypher(''imdb'', $$
            MATCH (actor)-[:acted_in {role: %s}]->(movie:movie)
            RETURN actor.name, movie.title
        $$) AS (actor agtype, movie agtype);', role);
    RETURN QUERY EXECUTE sql;
END
$function$;
```

### Prepared Statements with Cypher Parameters

Cypher parameters use `$` prefix (e.g., `$name`), and the third argument to `cypher()` passes the parameter map:

```sql
-- Prepare
PREPARE cypher_stored_procedure(agtype) AS
SELECT * FROM cypher('graph_name', $$
    MATCH (v:Person) WHERE v.name = $name RETURN v
$$, $1) AS (v agtype);

-- Execute (parameter names without $ in the map)
EXECUTE cypher_stored_procedure('{"name": "Tobias"}');
```

### Calling SQL Functions from Cypher

See [User-Defined Functions](#user-defined-functions-calling-sql-from-cypher) above. The pattern is to create a PostgreSQL function and call it via `pg_catalog.` or the schema-qualified name.

### Graph Data Loading (agload)

Bulk-load data from CSV files:

```sql
-- 1. Create graph and labels first
SELECT create_graph('agload_test_graph');
SELECT create_vlabel('agload_test_graph', 'Country');
SELECT create_vlabel('agload_test_graph', 'City');
SELECT create_elabel('agload_test_graph', 'has_city');

-- 2. Load vertices (with id field)
SELECT load_labels_from_file('agload_test_graph', 'Country', '/path/to/countries.csv');
SELECT load_labels_from_file('agload_test_graph', 'City', '/path/to/cities.csv');

-- 3. Load edges
SELECT load_edges_from_file('agload_test_graph', 'has_city', '/path/to/edges.csv');

-- 4. Load vertices without id field (4th parameter = false)
SELECT load_labels_from_file('agload_test_graph', 'Country2', '/path/to/countries.csv', false);
```

**CSV Format**:
- **Vertices**: First column is `id` (optional if 4th parameter is false), remaining columns are property names
- **Edges**: `start_id`, `start_vertex_type`, `end_id`, `end_vertex_type`, then property columns

---

## I. Unique Aspects of AGE vs Standard Cypher

| Aspect | AGE (PostgreSQL Cypher) | Standard Cypher (Neo4j) |
|--------|------------------------|------------------------|
| **Query embedding** | `cypher()` function in SQL `FROM` clause | Direct Cypher queries |
| **Return type** | `agtype` (universal JSONB-like type) | Typed results |
| **Column definition** | Required `AS (col agtype, ...)` after `cypher()` | Not needed |
| **Cross-type orderability** | Defined ordering across all types (Path → Edge → Vertex → Map → List → String → Bool → Numeric → NULL) | More restrictive |
| **SQL integration** | Full SQL-Cypher hybrid: CTEs, JOINs, subqueries | Cypher-only |
| **Graph storage** | PostgreSQL schemas + tables per graph | Internal store |
| **Bulk loading** | `load_labels_from_file()` / `load_edges_from_file()` with CSV | `LOAD CSV` (different syntax) |
| **Boolean output** | Full words `true`/`false` | `true`/`false` (similar but context differs) |
| **NaN handling** | `NaN` compares equal to itself for ordering | `NaN` ≠ `NaN` |
| **`vertex_stats()`** | AGE-specific function | Not available |
| **`graph_path` variable** | Not used — graph name is first argument to `cypher()` | `graph_path` setting |
| **Prepared statements** | 3rd parameter in `cypher()` with agtype map | Parameter syntax |

### NaN Equality

AGE evaluates `NaN::float = NaN::float` as **true** to allow correct sorting. This differs from IEEE 754, which specifies NaN should never compare equal to anything (including itself).

### Boolean Output Format

AGE outputs booleans as full words `true` and `false`, unlike PostgreSQL's internal `t` / `f` format.

### No `graph_path` Variable

In standard openCypher, the `graph_path` variable controls which graph is queried. In AGE, the graph is specified as the first argument to the `cypher()` function instead.

---

## J. Practical Usage Patterns

### Setup Script Pattern

A complete setup sequence for working with AGE:

```sql
-- 1. Load extension (per session)
LOAD 'age';
SET search_path TO ag_catalog, "$user", public;

-- 2. Create graph (one-time)
SELECT create_graph('my_graph');

-- 3. Create data
SELECT * FROM cypher('my_graph', $$
    CREATE (alice:Person {name: 'Alice', age: 30}),
           (bob:Person {name: 'Bob', age: 25}),
           (alice)-[:KNOWS {since: 2020}]->(bob)
$$) AS (result agtype);

-- 4. Query
SELECT * FROM cypher('my_graph', $$
    MATCH (a:Person)-[e:KNOWS]->(b:Person)
    RETURN a.name, b.name, e.since
$$) AS (name1 agtype, name2 agtype, since agtype);
```

### Common Query Patterns

**Find all neighbors**:
```sql
SELECT * FROM cypher('g', $$
    MATCH (a:Person {name: 'Alice'})-[:KNOWS]->(friend)
    RETURN friend.name, friend.age
$$) AS (name agtype, age agtype);
```

**Find shortest path** (length not direct function — use query shape):
```sql
SELECT * FROM cypher('g', $$
    MATCH p = shortestPath((a:Person {name: 'Alice'})-[:KNOWS*]-(b:Person {name: 'David'}))
    RETURN length(p) AS pathLength
$$) AS (path_length agtype);
```

**Pattern comprehension (size of related set)**:
```sql
SELECT * FROM cypher('g', $$
    MATCH (a:Person)
    RETURN a.name, size((a)-[:KNOWS]->()) AS friendCount
$$) AS (name agtype, friend_count agtype);
```

**Date range traversal**:
```sql
SELECT * FROM cypher('g', $$
    MATCH (a:Person)-[e:KNOWS]->(b:Person)
    WHERE e.since >= 2020
    RETURN a.name, b.name, e.since
$$) AS (name1 agtype, name2 agtype, since agtype);
```

### Integration with GraphModel .NET Library

The GraphModel library (in `src/Graph.Model.Age/`) translates LINQ queries into AGE-dialect Cypher. When working with this library, the AGE dialect skill helps you:

1. **Understand the generated Cypher**: The library's `AgeCypherQueryVisitor` and `AgeFragmentRenderer` produce AGE-specific SQL. Knowledge of the dialect helps verify correctness.
2. **Debug query issues**: Recognize whether a problem is in the LINQ translation or in the AGE dialect itself.
3. **Write raw AGE queries**: For scenarios the library doesn't cover, you can write direct AGE SQL/Cypher using the patterns in this skill.
4. **Understand type mapping**: The library maps .NET types to/from `agtype` via `AgeValueConverters`.

---

## References

- **Architecture overview**: [`docs/age/architecture.md`](../../../docs/age/architecture.md)
- **Full-text search limitations**: [`docs/age/age-fulltext-search-limitations.md`](../../../docs/age/age-fulltext-search-limitations.md)
- **Pattern comprehension limitations**: [`docs/age/pattern-comprehension-limitations.md`](../../../docs/age/pattern-comprehension-limitations.md)
- **Introductory dialect docs**: [`intro/`](https://age.apache.org/age-manual/master/intro/)
  - [`overview.md`](https://age.apache.org/age-manual/master/intro/overview.html) — What AGE is
  - [`setup.md`](https://age.apache.org/age-manual/master/intro/setup.html) — Installation and session setup
  - [`cypher.md`](https://age.apache.org/age-manual/master/intro/cypher.html) — Cypher query format and restrictions
  - [`types.md`](https://age.apache.org/age-manual/master/intro/types.html) — The agtype data type system
  - [`operators.md`](https://age.apache.org/age-manual/master/intro/operators.html) — All operators with examples
  - [`precedence.md`](https://age.apache.org/age-manual/master/intro/precedence.html) — Operator precedence table
  - [`graphs.md`](https://age.apache.org/age-manual/master/intro/graphs.html) — Graph creation, deletion, and storage model
  - [`aggregation.md`](https://age.apache.org/age-manual/master/intro/aggregation.html) — Aggregation semantics and ambiguous grouping
  - [`comparability.md`](https://age.apache.org/age-manual/master/intro/comparability.html) — Comparability, equality, orderability, equivalence
  - [`agload.md`](https://age.apache.org/age-manual/master/intro/agload.html) — Bulk CSV loading
- **Advanced dialect docs**: [`advanced/`](https://age.apache.org/age-manual/master/advanced/)
  - [`advanced.md`](https://age.apache.org/age-manual/master/advanced/advanced.html) — CTEs, JOINs, SQL expressions, multiple graphs
  - [`plpgsql.md`](https://age.apache.org/age-manual/master/advanced/plpgsql.html) — PL/pgSQL functions with Cypher
  - [`prepared_statements.md`](https://age.apache.org/age-manual/master/advanced/prepared_statements.html) — Prepared statements with Cypher parameters
  - [`sql_in_cypher.md`](https://age.apache.org/age-manual/master/advanced/sql_in_cypher.html) — Calling SQL functions from Cypher

