CREATE EXTENSION IF NOT EXISTS dblink;
CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
DO $$
DECLARE
  v_dbname text := 'TestDb';
  v_sql    text;
BEGIN
  IF NOT EXISTS (SELECT FROM pg_database WHERE datname = v_dbname) THEN
    v_sql := format(
      'CREATE DATABASE %I
         WITH ENCODING = ''UTF8''
              TEMPLATE = template0
              LOCALE_PROVIDER = icu
              ICU_LOCALE = ''en-US'';',
      v_dbname
    );
    PERFORM dblink_exec('dbname=' || current_database(), v_sql);
  END IF;
END
$$ LANGUAGE plpgsql;   