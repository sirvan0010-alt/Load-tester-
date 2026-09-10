# MailLoadTester — AI Engineering Instructions

## 1. Purpose

This file is the engineering contract for any AI agent, coding assistant, reviewer or developer working in this repository.

**Repository:** `sirvan0010-alt/Load-tester-`

**Application:** MailLoadTester — C#/.NET 8 application for controlled SMTP/load testing, diagnostics and performance validation.

The repository is the source of truth. Never assume that a previous conversation, generated snippet or external audit accurately describes the current code.

The existing MailLoadTester 2.9.39 feature set must be preserved unless source-level evidence shows that a feature is obsolete, unsafe, broken or intentionally replaced.

---

## 2. Product boundary

MailLoadTester is a controlled testing and diagnostics application. It must remain suitable for authorized environments and legitimate SMTP/load-testing scenarios.

Core responsibilities include, where implemented:

- SMTP connectivity and protocol testing;
- persistent SMTP sessions;
- bounded concurrency;
- adaptive pacing/rate limiting;
- retry and SMTP error classification;
- circuit breaker behavior;
- cancellation and graceful shutdown;
- SMTP/TLS diagnostics;
- DNS/MX-related diagnostics;
- proxy/IP rotation where explicitly supported for authorized testing;
- warm-up/burst testing;
- per-recipient and domain-aware controls where implemented;
- session logging and observability;
- EML/message generation;
- GUI and CLI operation;
- automated tests and CI.

Do not add evasion, stealth, abuse or bypass behavior. Testing controls must be explicit and bounded.

---

## 3. Architecture principles

Keep responsibilities separated:

```text
MailLoadTester
│
├── Core
│   ├── Models / Options
│   ├── SMTP execution
│   ├── Pacing / Rate limiting
│   ├── Adaptive concurrency
│   ├── Retry / Backoff
│   ├── Circuit breaker
│   ├── DNS / MX diagnostics
│   ├── Message generation
│   └── Observability
│
├── GUI
│   └── presentation / user interaction
│
└── Tests
    └── unit / integration / regression tests
```

Do not move production business logic into UI code merely for convenience.

Do not create duplicate implementations of an existing mechanism. Before adding an abstraction, inspect the repository for an equivalent implementation and its callers.

Prefer small, coherent changes over uncontrolled rewrites.

---

## 4. Repository-first audit rule

Before changing code:

1. inspect the complete relevant source tree;
2. inspect project files and dependencies;
3. inspect the target implementation and all important callers;
4. inspect tests and CI;
5. identify what is actually implemented;
6. distinguish bugs from optional improvements;
7. check cancellation, disposal, concurrency and error paths;
8. only then modify the code.

Never mark a feature as implemented merely because a filename, class name or documentation claims that it exists.

External AI audits are input, not authority. Verify every claim against the current source code.

---

## 5. Technical principles

Prefer:

- C# 12 / .NET 8 as defined by the project files;
- asynchronous APIs for I/O;
- `CancellationToken` propagation;
- `SemaphoreSlim`, `Interlocked` and `Volatile` where appropriate for thread-safe coordination;
- bounded concurrency;
- deterministic state transitions;
- persistent SMTP sessions where appropriate;
- explicit retry/error classification;
- bounded retry counts;
- cancellation-safe waits;
- safe disposal of SMTP clients and other resources;
- structured logging and measurable results;
- unit tests for concurrency-sensitive behavior.

Do not use `.Result`, `.Wait()` or blocking synchronization in asynchronous execution paths.

Do not introduce hardcoded credentials, API keys or production secrets.

---

## 6. Libraries and runtime

The current MailLoadTester Core project defines the following direct dependencies:

### 6.1 MailKit

`MailKit` version `4.7.1`.

Use for SMTP protocol communication and SMTP client/session handling.

### 6.2 MimeKit

`MimeKit` version `4.7.1`.

Use for MIME/message construction and parsing where required.

### 6.3 DnsClient

`DnsClient` version `1.8.0`.

Use for DNS/MX and related DNS diagnostics where the existing application requires them.

### 6.4 Bogus

`Bogus` version `35.6.1`.

Use for deterministic/test-oriented synthetic data generation where appropriate. Do not use it to generate uncontrolled production recipient data.

### 6.5 System.IO.Pipelines

`System.IO.Pipelines` version `8.0.0`.

Use where the existing implementation benefits from efficient streaming/pipeline processing.

### 6.6 Runtime and language

- Target framework: `.NET 8`
- Language version: `C# 12`

### 6.7 Dependency rules

Before adding a new package:

1. verify that the functionality is not already provided by the BCL or an existing dependency;
2. verify that a duplicate library is not already present;
3. verify .NET 8 compatibility;
4. add the smallest necessary dependency;
5. document the reason in the project/architecture documentation when the dependency materially changes the architecture.

Never invent a dependency or claim that a package is used without verifying the project file.

---

## 7. SMTP session lifecycle

Persistent SMTP sessions are preferred where supported and useful for the test scenario.

A single `MailKit.Net.Smtp.SmtpClient` instance must not be used concurrently by multiple operations unless the implementation explicitly serializes access.

Handle:

- connection failure;
- TLS negotiation failure;
- authentication failure;
- server disconnect;
- idle timeout;
- SMTP throttling;
- cancellation;
- disposal.

Do not reconnect for every message unless the selected test scenario explicitly requires it.

---

## 8. Concurrency and pacing

Concurrency and pacing are different mechanisms.

### Concurrency
Controls the number of simultaneous SMTP operations/sessions.

### Pacing/rate limiting
Controls when operations are allowed to start over time.

### Backoff
Controls how a failed operation delays its next attempt.

The current implementation may contain `SmartPaceController`, adaptive concurrency and other pacing components. Inspect their actual ownership and interaction before modifying them.

Important invariants:

- cancellation must interrupt waits;
- reservations must not leak;
- retry paths must not accidentally bypass required global pacing;
- retry paths must not create duplicate logical reservations;
- adaptive concurrency must never exceed configured bounds;
- shared counters/state must be thread-safe.

Do not replace a working pacing architecture with arbitrary `Task.Delay` calls without demonstrating why the existing mechanism is incorrect.

---

## 9. Retry and SMTP error classification

At minimum distinguish:

- successful SMTP acceptance;
- temporary SMTP `4xx`;
- permanent SMTP `5xx`;
- timeout;
- connection/I/O failure;
- SMTP protocol failure;
- authentication failure;
- cancellation;
- local message-generation/serialization failure.

Typical policy:

- transient network/protocol/timeout failures may be retried when bounded;
- SMTP `4xx` may be retried according to policy;
- SMTP `5xx` normally must not be retried indefinitely;
- authentication failures normally require configuration correction rather than repeated retries;
- cancellation must not be converted into an ordinary failure/retry.

Every retry must remain bounded by configuration and retain the same logical test operation/recipient identity where applicable.

Never implement infinite retries.

---

## 10. Circuit breaker

The circuit breaker must protect the SMTP test execution from repeated failures when configured to do so.

Audit explicitly:

- state transitions;
- failure counting;
- success recovery;
- open/half-open/closed semantics if implemented;
- interaction with retries;
- cancellation;
- thread safety;
- whether failures are counted once per actual failed attempt or accidentally duplicated.

Do not change semantics without verifying all callers and tests.

---

## 11. Adaptive concurrency

Adaptive concurrency must remain bounded by configured minimum/maximum values.

Audit explicitly:

- acquisition/release symmetry;
- waiting behavior;
- cancellation while waiting;
- thread safety;
- adjustment after success/failure;
- interaction with retries;
- interaction with circuit breaker;
- prevention of permit leaks;
- prevention of double release;
- behavior during shutdown.

Any adaptive value shared between tasks must use appropriate atomic/thread-safe access.

---

## 12. Statistics and observability

Results and counters must be internally consistent.

Audit explicitly:

- `Sent`;
- `Failed`;
- `Retries`;
- SMTP `4xx`/`5xx` counters;
- timeouts;
- elapsed time;
- throughput;
- active/pool connections;
- adaptive concurrency;
- circuit breaker state;
- cancellation state;
- session logs.

Avoid races where a final result is read while worker tasks are still updating shared state.

Do not report a message as successfully sent when the SMTP operation was only started or when the result is ambiguous.

---

## 13. Security and authorization

The tool is intended for authorized testing.

Never commit:

- SMTP passwords;
- API keys;
- OAuth tokens;
- private keys;
- credential-bearing connection strings;
- production recipient lists;
- unnecessary personal data.

Use environment variables, configuration providers or secret stores.

Do not log credentials, authorization headers or sensitive tokens.

The command-line requirement `--unauthorized` must remain supported where required by the current application contract. Do not silently remove or rename it.

Do not implement mechanisms intended to evade provider controls, spam defenses, rate limits or abuse detection.

---

## 14. Cancellation and disposal

Cancellation must propagate through asynchronous operations wherever supported.

Shutdown should:

1. stop starting new work;
2. cancel pending waits where appropriate;
3. allow safe in-flight operations to finish when possible;
4. release concurrency/pacing reservations;
5. dispose/release SMTP resources;
6. return a consistent final result.

Normal `OperationCanceledException` should not be reported as an unexpected application crash.

---

## 15. Testing

Tests should cover at minimum where applicable:

- SMTP success;
- SMTP `4xx`;
- SMTP `5xx`;
- timeout;
- connection/TLS failure;
- authentication failure;
- retry limit;
- retry backoff;
- cancellation during delay/wait;
- pacing first-slot behavior;
- retry re-entry into global pacing;
- adaptive concurrency acquire/release;
- cancellation while acquiring adaptive concurrency;
- circuit breaker transitions;
- SMTP session reconnect;
- concurrent workers;
- statistics consistency;
- configuration validation;
- shutdown/disposal;
- regression cases for every verified bug fix.

Network tests must use mocks/fakes or a dedicated local test SMTP server. Never require production credentials.

---

## 16. Documentation and audit

The repository should keep implementation status and audit results separate from assumptions.

Recommended documents include:

```text
README.md
AI_INSTRUCTIONS.md
AUDIT-*.md
README-BUILD.md
README-MAINTENANCE.md
```

Audit documents must distinguish:

- verified implementation;
- partial implementation;
- architectural risk;
- missing functionality;
- verified defect;
- planned improvement.

Do not describe an unverified feature as implemented.

---

## 17. Mandatory AI workflow

Every AI agent working on this repository must follow exactly this process:

**1. Projdu část podle plánu**

→ najdu konkrétní problém.

**2. Rozhodnu, jestli je to skutečná chyba**

Ne každá věc, která se dá „vylepšit“, je bug.

**3. Pokud je to skutečná chyba → opravím ji přímo v GitHubu.**

Ne jen:

> „Tady je problém, doporučuji opravit.“

Ale:

> „Našel jsem chybu → opravil jsem zdroják → commit → ověřím návaznosti.“

**4. Pokud změnu nelze bezpečně udělat bez dalších podkladů**, nebudu si vymýšlet. Zapíšu nález a pokračuju.

**5. Nakonec testy/build.**

Additional mandatory rules:

- Inspect the current source before every non-trivial change.
- Fix verified bugs directly in the correct source file whenever the change can be made safely.
- Do not substitute an audit note for a source fix when a verified safe fix is possible.
- After each source fix, inspect callers, related state transitions and tests for regressions.
- Commit source fixes with a descriptive commit message.
- Do not claim that a fix was pushed unless GitHub confirms the resulting commit.
- Run tests/build after the planned changes whenever the available environment permits it.
- If tests/build cannot be executed, state that explicitly rather than claiming validation.

---

## 18. Anti-patterns

Do NOT:

- hard-code credentials;
- use blocking waits in async execution paths;
- create unlimited parallel tasks;
- leak semaphore/limiter permits;
- double-release concurrency permits;
- retry permanently failed SMTP operations forever;
- bypass global pacing during retries;
- create duplicate pacing reservations for the same logical retry;
- treat SMTP `250` as proof of final delivery;
- mix GUI presentation with core SMTP execution logic;
- silently change existing test behavior;
- delete working features without source-level evidence;
- introduce dependencies without need;
- implement stealth/evasion mechanisms;
- bypass spam, abuse or provider controls;
- log secrets;
- claim a bug is fixed without a source change and commit confirmation.

---

## 19. Definition of done

A change is complete only when:

- it is implemented in the correct layer;
- the actual source reflects the intended behavior;
- responsibilities remain separated;
- async/cancellation behavior is correct where applicable;
- shared state is thread-safe where applicable;
- retries and errors are classified correctly;
- concurrency/pacing reservations are balanced;
- important failure paths have tests;
- documentation is updated when behavior changes;
- tests/build pass, or limitations are explicitly documented;
- the resulting diff is reviewed;
- no unrelated behavior was silently changed;
- the resulting GitHub commit is verified.

---

## 20. Current audit/fix sequence

Unless source evidence requires a different order, continue audits in this order:

1. Retry/error classification.
2. CircuitBreaker.
3. AdaptiveConcurrencyLimiter.
4. SMTP pool/session lifecycle.
5. Statistics and race conditions.
6. Regression tests for verified fixes.
7. `dotnet test`.
8. `dotnet build -c Release`.

For each stage:

```text
inspect → identify concrete defect → verify defect → fix source if safe
→ inspect callers/dependencies → add/update regression test → commit → validate
```

Do not stop after writing an audit report if a safe source-level fix is available.

---

## Final rule

**MailLoadTester must remain an efficient, bounded, asynchronous and testable SMTP/load-testing application for authorized environments.**

The repository source is authoritative. Verify first, fix real bugs directly, do not invent missing facts, and always validate the resulting change.
