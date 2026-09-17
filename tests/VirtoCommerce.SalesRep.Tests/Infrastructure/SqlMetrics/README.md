# SQL metrics

Records every SQL command the component tests run, so that a call quietly starting to issue more
queries is noticed. Off by default; nothing is recorded and no file is written until it is switched on.

## Running it

1. Set `"Enabled": true` in `sqlmetrics.json` (tests project root).
2. Run the suite **on one thread**:

   ```
   dotnet test --settings sqlmetrics.runsettings
   ```

3. Set `"Enabled"` back to `false`.

Two files land in `tests/VirtoCommerce.SalesRep.Tests/sql-metrics/`:

| file | what it is |
|---|---|
| `sql-counts.txt` | One line per test, `name<TAB>count`, sorted. **Committed.** |
| `sql-report.html` | The queries themselves, grouped under the test that ran them. Not committed — it is a few megabytes and changes every run. |

## The workflow

`sql-counts.txt` is the metric. Commit it, and after a change re-run and `git diff` it: every test whose
query count moved is one diff line, with its name attached. Nothing fails, nothing is asserted — you
read the diff and decide whether the change is expected. When it is, commit the new counts alongside
the change and the diff becomes its record.

`sql-report.html` is for the question that follows: *why* did that test go from 12 to 19. Open it,
expand the test, read the queries in order. It is the same view the local Application Insights
stand-in gives (`P:\VC\_VIRTO\_temp\app_insights_stub`), with the records written into the page
instead of fetched. It carries the heaviest `HtmlMaxTests` tests only; the counts file has them all.

## Why one thread

Counts have to repeat between runs, or the diff is noise rather than a signal. In parallel they do
not, and the reason is ordinary behaviour rather than a defect: **caches are process wide, so
whichever test reaches a thing first pays for filling it and the ones after it do not.** Which test
gets there first changes with the scheduling. Measured on two parallel runs of the unchanged suite,
38 tests moved and the movement summed to 146 queries.

Running on one thread fixes the order, and with it who pays. **Two sequential runs produce byte
identical counts** — that is what makes the file worth committing.

Two things follow that are worth knowing when reading a diff:

- A test's count is *its own queries plus whatever it happened to warm up first*. It is a number to
  compare against itself between runs, not a measure of how much that call costs in isolation.
- Adding, removing or renaming a test can move the counts of **unrelated** tests, because the order
  changed and a different test is now the cold one. A wide diff after a change that touched only the
  test list is that, not a regression.

## What the numbers mean, and what they do not

- **Counts and query shapes carry over to production. Durations do not.** This is in-memory SQLite
  with a handful of seeded rows; an n+1 over ten rows costs nothing here and looks healthy. Read the
  report to see that a call issues *more* queries, never to judge how slow it is. For real timings,
  point the Application Insights stand-in at a backend on PostgreSql.
- **The count includes the test's own setup and seeding**, not only the call under test. That is
  deliberate — it needs no markers in the tests, and a seeded count is just as stable to diff. It
  does mean a change to a shared seeding helper moves many lines at once.
- **Lucene is in memory and is not SQL**, so work that moves from the database to the index
  disappears from this report rather than showing as an improvement.

## How it is wired

- `SqlMetricsInterceptor` — an EF Core `DbCommandInterceptor`, so it sees every context and does not
  depend on the provider.
- `SqliteTestDbContextFactory` attaches it, and only after `EnsureCreated`, so the schema burst is
  not counted against whichever test happened to build the context.
- Commands raised outside a test are dropped: a report grouped by test has nowhere to put them.
- `SqlMetricsSink` collects the run and writes on process exit — on the first recorded command, so a
  run that records nothing leaves the previous report alone rather than emptying it.
