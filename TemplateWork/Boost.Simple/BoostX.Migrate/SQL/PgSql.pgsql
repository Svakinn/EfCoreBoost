CREATE OR REPLACE FUNCTION core."GetIpId"(ipno TEXT)
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
    FROM core."IpInfo" i
    WHERE i."IpNo" = ipno;
    IF found_id IS NULL THEN
        -- Insert new record
        INSERT INTO core."IpInfo" ("IpNo", "LastChangedUtc", "Processed")
        VALUES (ipno, NOW() AT TIME ZONE 'UTC', false)
        RETURNING "Id" INTO found_id;
    ELSIF processed AND lch + INTERVAL '180 days' > NOW() AT TIME ZONE 'UTC' THEN
        -- Recheck hostname after 6 months
        UPDATE core."IpInfo" SET processed = false WHERE "Id" = found_id;
    END IF;
    RETURN found_id;
END;
$$;

CREATE VIEW core."IpInfoView" AS
  SELECT i."Id", i."IpNo", i."HostName"
  FROM core."IpInfo";
  
 CREATE OR REPLACE FUNCTION core."GetIpViewByIpId"(ipid BIGINT)
 RETURNS SETOF core."IpInfoView" 
 LANGUAGE plpgsql
 AS $$
 BEGIN
   RETURN QUERY
   SELECT 
     "Id",
     "IpNo",
     "HostName"
    FROM core."IpInfo"
    WHERE "Id" = ipid;
 END;
 $$;