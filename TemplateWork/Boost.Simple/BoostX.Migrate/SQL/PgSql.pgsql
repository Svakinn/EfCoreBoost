CREATE OR REPLACE FUNCTION "BoostScemaX"."GetIpId"(ipno TEXT)
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
    FROM "BoostScemaX"."IpInfo" i
    WHERE i."IpNo" = ipno;
    IF found_id IS NULL THEN
        -- Insert new record
        INSERT INTO "BoostScemaX"."IpInfo" ("IpNo", "LastChangedUtc", "Processed")
        VALUES (ipno, NOW() AT TIME ZONE 'UTC', false)
        RETURNING "Id" INTO found_id;
    ELSIF processed AND lch + INTERVAL '180 days' > NOW() AT TIME ZONE 'UTC' THEN
        -- Recheck hostname after 6 months
        UPDATE "BoostScemaX"."IpInfo" SET processed = false WHERE "Id" = found_id;
    END IF;
    RETURN found_id;
END;
$$;

CREATE VIEW "BoostScemaX"."IpInfoView" AS
  SELECT i."Id", i."IpNo", i."HostName"
  FROM "BoostScemaX"."IpInfo";
  
 CREATE OR REPLACE FUNCTION "BoostScemaX"."GetIpViewByIpId"(ipid BIGINT)
 RETURNS SETOF "BoostScemaX"."IpInfoView" 
 LANGUAGE plpgsql
 AS $$
 BEGIN
   RETURN QUERY
   SELECT 
     "Id",
     "IpNo",
     "HostName"
    FROM "BoostScemaX"."IpInfo"
    WHERE "Id" = ipid;
 END;
 $$;