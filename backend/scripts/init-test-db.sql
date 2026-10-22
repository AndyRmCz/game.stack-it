-- Runs once when the docker compose Postgres volume is first created.
-- The test suite drops and recreates the public schema of this database, so it must never be
-- the development database.
CREATE DATABASE stackit_test;
