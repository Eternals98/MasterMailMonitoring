# Changelog

All notable changes to this project are documented in this file.

## [2026.04.01] - 2026-04-01

### Added
- REST API for operational management:
  - `settings` (`GET/PUT`)
  - `companies` (`GET/POST/PUT/DELETE`)
  - `graph-settings` (`GET/PUT`)
  - `triggers` (`GET/POST/PUT/DELETE`)
  - `email-statistics` query endpoint
  - `reports/export` Excel endpoint
  - `health/graph` connectivity endpoint
- Web operational console pages:
  - Settings
  - Companies
  - Graph Settings
  - Monitoring and Excel export
- Worker scheduling service with Quartz trigger loading and fallback cron.
- Unit test suite for domain/filter/storage behavior.
- Integration test suite for API + SQLite persistence.
- D5 closure documentation set:
  - bug triage
  - scope freeze
  - QA final results
  - E2E rehearsal
  - go-live and rollback checklist
  - DoD validation
  - final delivery report

### Changed
- Graph secret masking behavior now fully masks short secrets (length <= 4).
- D5 release process now enforces scope freeze and go-live rollback criteria.

### Fixed
- Fixed `POST /api/triggers` returning HTTP 500 due to route generation in create response.
- Added integration regression tests for:
  - Graph secret masking
  - Trigger creation with `201 Created` and `Location` resolution.

### Security
- Reduced risk of exposing credentials by hardening Graph secret masking in API responses.

### Notes
- Manual Graph mailbox and UNC path E2E validation remains an environment-gated go-live check.
