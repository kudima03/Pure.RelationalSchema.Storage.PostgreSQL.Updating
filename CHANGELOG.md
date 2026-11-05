# Changelog

All notable changes to `Pure.RelationalSchema.Storage.PostgreSQL.Updating` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Row updating: `PostgreSqlStoredSchemaDataSetWithUpdatedRows` wraps an `IPostgreSqlStoredSchemaDataSet` and lazily applies updates grouped by table. Each update is a grouping whose key is the cells identifying the stored rows and whose elements are the cells to set; it becomes one `UPDATE … SET … WHERE …` statement (no `WHERE` for an empty key, so it updates every row), and a table's statements run in the supplied order as one command. Updates with nothing to set are skipped, and embedded single quotes in values are escaped.
