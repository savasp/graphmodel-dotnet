LOAD 'age';
SET search_path = ag_catalog, "$user", public;

SELECT * FROM ag_catalog.cypher('graphmodeltests000', $$ CREATE (n:PersonWithNullableProperties)
      SET n.Name = "mytest", n.user_id = "user123"
      RETURN n $$) as (n agtype);




SELECT * FROM ag_catalog.cypher('graphmodeltests000', $$ MATCH (n:PersonWithNullableProperties)
      WHERE n.user_id = "user123"
      RETURN n
      LIMIT 1 $$) 
as (n agtype);

SELECT drop_graph('graphmodeltests000', true);

MATCH (n:Person)
        WHERE n.FirstName = $param_0
        RETURN 
            toUpper(n.FirstName) AS Upper, 
            toLower(n.LastName) AS Lower, 
            trim(n.Bio) AS Trimmed, 
            substring(n.Bio, $param_1, $param_2) AS Substring,
            replace(n.FirstName, $param_3, $param_4) AS Replaced, 
            n.FirstName STARTS WITH $param_5 AS StartsWith, 
            n.LastName ENDS WITH $param_6 AS EndsWith, 
            n.Bio =~ ('.*' + $param_7 + '.*') AS Contains, 
            size(n.Bio) AS Length, 
            abs((n.Age - $param_8)) AS AbsAge, 
            ceil((n.Age / $param_9)) AS Ceiling, 
            floor((n.Age / $param_10)) AS Floor, 
            round((n.Age / $param_11)) AS Round, 
            sqrt(n.Age) AS Sqrt, 
            ($param_12) ^ ($param_13) AS Power, 
            $param_14 AS Now, 
            $param_15 AS Today, 
            $param_16 AS UtcNow, 
            toInteger(substring($param_17, 0, 4)) AS Year, 
            toInteger(substring($param_18, 5, 2)) AS Month, 
            toInteger(substring($param_19, 8, 2)) AS Day


SELECT * FROM ag_catalog.cypher('graphmodeltests000', $$ MATCH (n:Person)
WHERE n.FirstName = "John"
RETURN 
    toUpper(n.FirstName) AS Upper, 
    toLower(n.LastName) AS Lower, 
    trim(n.Bio) AS Trimmed, 
    substring(n.Bio, 0, 8) AS Substring, 
    replace(n.FirstName, "o", "0") AS Replaced, 
    n.FirstName STARTS WITH "J" AS StartsWith, 
    n.LastName ENDS WITH "oe" AS EndsWith, 
    n.Bio =~ ('.*' + "Engineer" + '.*') AS 'Contains', 
    size(n.Bio) AS Length, 
    abs((n.Age - 25)) AS AbsAge, 
    ceil((n.Age / 7)) AS Ceiling, 
    floor((n.Age / 7)) AS Floor, 
    round((n.Age / 7)) AS Round, 
    sqrt(n.Age) AS Sqrt, 
    (2) ^ (3) AS Power $$) as (
        Upper agtype,
        Lower agtype,
        Trimmed agtype,
        Substring agtype,
        Replaced agtype,
        StartsWith agtype,
        EndsWith agtype,
        "Contains" agtype,
        Length agtype,
        AbsAge agtype,
        Ceiling agtype,
        Floor agtype,
        Round agtype,
        Sqrt agtype,
        Power agtype
    );


SELECT * FROM ag_catalog.cypher('graphmodeltests000', $$ MATCH (n:Person)
WHERE n.FirstName = $param_0
RETURN 
    toUpper(n.FirstName) AS "Upper", 
    toLower(n.LastName) AS "Lower", 
    trim(n.Bio) AS "Trimmed", 
    substring(n.Bio, $param_1, $param_2) AS "Substring", 
    replace(n.FirstName, $param_3, $param_4) AS "Replaced", 
    n.FirstName STARTS WITH $param_5 AS "StartsWith", 
    n.LastName ENDS WITH $param_6 AS "EndsWith", 
    n.Bio =~ ('.*' + $param_7 + '.*') AS "Contains", 
    size(n.Bio) AS "Length", 
    abs((n.Age - $param_8)) AS "AbsAge", 
    ceil((toFloat(n.Age) / $param_9)) AS "Ceiling", 
    floor((toFloat(n.Age) / $param_10)) AS "Floor", 
    round((toFloat(n.Age) / $param_11)) AS "Round", 
    sqrt(toFloat(n.Age)) AS "Sqrt", 
    ($param_12) ^ ($param_13) AS "Power", 
    $param_14 AS "Now", 
    $param_15 AS "Today", 
    $param_16 AS "UtcNow", 
    toInteger(substring($param_17, 0, 4)) AS "Year", 
    toInteger(substring($param_18, 5, 2)) AS "Month", 
    toInteger(substring($param_19, 8, 2)) AS "Day" $$, $1) as (
        "Upper" agtype, 
        "Lower" agtype, 
        "Trimmed" agtype, 
        "Substring" agtype, 
        "Replaced" agtype, 
        "StartsWith" agtype, 
        "EndsWith" agtype, 
        "Contains" agtype, "Length" agtype, "AbsAge" agtype, "Ceiling" agtype, "Floor" agtype, "Round" agtype, "Sqrt" agtype, "Power" agtype, "Now" agtype, "Today" agtype, "UtcNow" agtype, "Year" agtype, "Month" agtype, "Day" agtype);


SELECT 
    pid,
    datname,
    usename,
    application_name,
    state,
    wait_event_type,
    wait_event,
    EXTRACT(EPOCH FROM (NOW() - state_change)) as seconds_in_state,
    query_start,
    left(query, 200) as query_preview
FROM pg_stat_activity 
WHERE datname = 'postgres'
  AND pid != pg_backend_pid()
ORDER BY state_change DESC


 CREATE (n:PersonWithNullableProperties)
        SET n.Name = $prop0, n.id = $prop1
        RETURN n