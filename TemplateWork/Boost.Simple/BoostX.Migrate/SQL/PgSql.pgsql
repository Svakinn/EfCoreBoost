CREATE OR REPLACE FUNCTION "BoostSchemaX"."GetIpId"(ipno TEXT)
RETURNS BIGINT
LANGUAGE plpgsql
AS $$
DECLARE
    found_id BIGINT;
    processed BOOLEAN;
    lch TIMESTAMP;
BEGIN
    -- Try to find existing record
    SELECT i."Id", i."Processed", i."LastChangedUtc"
    INTO found_id, processed, lch
    FROM "BoostSchemaX"."IpInfo" i
    WHERE i."IpNo" = ipno;
    IF found_id IS NULL THEN
        -- Insert new record
        INSERT INTO "BoostSchemaX"."IpInfo" ("IpNo", "LastChangedUtc", "Processed")
        VALUES (ipno, NOW() AT TIME ZONE 'UTC', false)
        RETURNING "Id" INTO found_id;
    ELSIF processed AND lch + INTERVAL '180 days' > NOW() AT TIME ZONE 'UTC' THEN
        -- Recheck hostname after 6 months
        UPDATE "BoostSchemaX"."IpInfo" SET "Processed" = false WHERE "Id" = found_id;
    END IF;
    RETURN found_id;
END;
$$;

CREATE VIEW "BoostSchemaX"."IpInfoView" AS
  SELECT i."Id", i."IpNo", i."HostName"
  FROM "BoostSchemaX"."IpInfo" i;
  
 CREATE OR REPLACE FUNCTION "BoostSchemaX"."GetIpViewByIpId"(ipid BIGINT)
 RETURNS SETOF "BoostSchemaX"."IpInfoView" 
 LANGUAGE plpgsql
 AS $$
 BEGIN
   RETURN QUERY
   SELECT 
     "Id",
     "IpNo",
     "HostName"
    FROM "BoostSchemaX"."IpInfo"
    WHERE "Id" = ipid;
 END;
 $$;
