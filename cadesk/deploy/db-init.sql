-- Run ONCE as a PostgreSQL superuser to create the two roles and the databases.
-- Docker Compose runs this automatically on first start.
-- Change the passwords for any real environment (use secrets, not this file).

create role cadesk_owner login password 'owner_dev_pw';
create role cadesk_app   login password 'app_dev_pw';   -- not an owner: row-level security applies

create database cadesk      owner cadesk_owner;
create database cadesk_test owner cadesk_owner;        -- used by integration tests

