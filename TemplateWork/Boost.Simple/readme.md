# The Boost Simple Project Template

This folder contains two corresponding projects: the model and the migration.

### Model Project
The model project demonstrates the use of a single table, a view, and stored procedures. It also includes an example of seed data being part of the migration SQL.

### Migration Project
The migration project demonstrates how to build migrations for three major providers: SQL Server, MySQL, and PostgreSQL. It also highlights how to perform bulk-inserts of large datasets into the database.

The migration includes both the generated migration SQL and the manual part for views and routines, ensuring that the entire database schema is version-controlled and consistently updated across all providers.

Furthermore, an alternative way of applying migrations is showcased. Instead of standard EF Core migrations, one command is used to create the database and another to apply migrations at the SQL level.

This approach is flexible and can be adapted for both production and test setups. In enterprise environments, a database administrator might perform these steps manually using the provided SQL migration scripts.

