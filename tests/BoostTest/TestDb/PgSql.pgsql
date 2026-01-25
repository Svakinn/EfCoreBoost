CREATE SCHEMA IF NOT EXISTS my;

CREATE SEQUENCE my."MySeq"
    AS bigint
    START WITH 1
    INCREMENT BY 1
    MINVALUE -9223372036854775808
    MAXVALUE 9223372036854775807
    CACHE 1;

CREATE OR REPLACE FUNCTION my."ReserveMyIds"(IdCount INT)
RETURNS TABLE(id BIGINT) AS $$
BEGIN
    RETURN QUERY
    SELECT nextval('my."MySeq"') FROM generate_series(1, IdCount);
END;
$$ LANGUAGE plpgsql;

-- Stored procedure test scalar result
CREATE OR REPLACE FUNCTION my."GetMaxIdByChanger"(Changer VARCHAR(50)) RETURNS BIGINT AS $$
BEGIN
    RETURN (
        SELECT MAX("Id") FROM my."MyTable" WHERE "LastChangedBy" = Changer
    );
END;
$$ LANGUAGE plpgsql;

CREATE VIEW my."MyTableRefView" AS
SELECT
    r."Id" AS "RefId",
    t."Id" AS "MyId",
    t."RowID",
    t."LastChanged",
    t."LastChangedBy",
    r."MyInfo",
    r."LastChanged" AS "RefLastChanged",
    r."LastChangedBy" AS "RefLastChangedBy"
FROM my."MyTableRef" r
INNER JOIN my."MyTable" t ON t."Id" = r."ParentId";

CREATE OR REPLACE FUNCTION my."GetMyTableRefViewByMyId"(MyId BIGINT)
RETURNS SETOF my."MyTableRefView" AS $$
BEGIN
    RETURN QUERY
    SELECT * FROM my."MyTableRefView" WHERE "MyId" = MyId;
END;
$$ LANGUAGE plpgsql;

