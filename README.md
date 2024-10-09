# Postgres: Use Views to Refactor to Soft Delete

Hello! You have found the repository containing the demo/test code for my article [Postgres: Use Views to Refactor to Soft Delete](https://ian.wold.guru/Posts/postgres_use_views_to_refactor_to_soft_delete.html). I encourage you to read that article before or while proceeding with this repo, it will make a lot more sense!

## Repo Structure

* `Program` contains the whole test code. You can paste in connection strings to your own Postgres at the top. You can also set the variable `clearDatabaseBeforeTests` to `true` if you want to run the test multiple times on a fresh database. Beware: turning this flag on will delete your `public` schema.
* `DatabaseExecutor` is a barebones class to execute queries against the database.
* `SingleTableMigration` and `SeparateTableMigration` store the queries specific to each of the two migration methods described in my article. Each has three migration steps, as well as the queries which you would use after the migration to access soft-deleted records.

## How the Tests Work

Each of the two strategies is run through the same test with the same starting schema and the same queries to read and write data to the database. This shows how each strategy can be implemented without altering the queries you're already executing against your database. The goal will be to test all the normal database operations between each migration step.

To start, the three initial tables are created in the database. Then, for each of the three migration steps records are inserted, deleted, and updated from these tables, with the ids of the inserted and deleted records being tracked over time. After each simulation of user actions, the database is checked to ensure it contains the ids it should and does not contain the ids it should not. After this, the migration step is applied to the database. This allows each step to be tested individually.

After each of the migration steps, another round of user actions is simulated, except this time we will expect the deleted records to be soft deleted, since the migration is finished. We do the same check as we have before to ensure that we can access all the records we ahve inserted and cannot access the records we have deleted. However, we do an addition step to ensure that our new queries to access soft deleted data return those records we deleted after the migration.
