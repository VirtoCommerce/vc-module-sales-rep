import { describe, expect, it } from "vitest";
import {
  buildDayMarkers,
  dueDateForDay,
  dueDateForDayAndTime,
  emptyTaskFilters,
  formatTimeOfDay,
  isCallTask,
  localCalendarWindow,
  localDayKey,
  localDayKeyToIso,
  localDayWindow,
  localTimeKey,
  startOfLocalDay,
  taskFilterVariables,
  taskPriorityLabelKey,
  taskStatus,
  taskSubline,
  timeKeysOfDay,
  timeKeyToDate,
  toMonthKey,
} from "./tasks";
import type { SalesRepTaskStatusType, SalesRepTaskType } from "./types/tasks";
import type { ComposerTranslation, DateTimeOptions } from "vue-i18n";

// Everything here is deliberately expressed in LOCAL time and compared against locally-constructed dates, never
// against literal ISO strings: the whole point of these helpers is that a day is a day on the viewer's calendar,
// so asserting UTC text would only pass in one timezone.

const DAY_START = startOfLocalDay(new Date(2026, 4, 28, 9, 30));

function task(overrides: Partial<{ isActive: boolean; completed: boolean | null; dueDate: string | null }> = {}) {
  return { isActive: true, completed: null, dueDate: null, ...overrides };
}

function isoAt(year: number, month: number, day: number, hours = 0, minutes = 0): string {
  return new Date(year, month - 1, day, hours, minutes).toISOString();
}

describe("startOfLocalDay", () => {
  it("strips the time, keeping the local calendar day", () => {
    const result = startOfLocalDay(new Date(2026, 4, 28, 23, 59, 59, 999));

    expect(result.getFullYear()).toBe(2026);
    expect(result.getMonth()).toBe(4);
    expect(result.getDate()).toBe(28);
    expect(result.getHours()).toBe(0);
  });
});

describe("localDayKey", () => {
  it("pads month and day", () => {
    expect(localDayKey(new Date(2026, 0, 5))).toBe("2026-01-05");
  });

  it("reads an instant on the viewer's calendar, not UTC's", () => {
    // Late-evening local time: slicing the ISO string would give the NEXT day for any negative UTC offset.
    const evening = new Date(2026, 4, 28, 23, 30);

    expect(localDayKey(evening.toISOString())).toBe("2026-05-28");
  });
});

describe("taskStatus", () => {
  it("reads a finished task as completed", () => {
    expect(taskStatus(task({ isActive: false, completed: true, dueDate: isoAt(2026, 5, 1) }), DAY_START)).toBe(
      "completed",
    );
  });

  it("reads a closed-but-unfinished task as canceled", () => {
    expect(taskStatus(task({ isActive: false, completed: false }), DAY_START)).toBe("canceled");
    expect(taskStatus(task({ isActive: false, completed: null }), DAY_START)).toBe("canceled");
  });

  it("reads an open task due before today as overdue", () => {
    expect(taskStatus(task({ dueDate: isoAt(2026, 5, 27, 23, 59) }), DAY_START)).toBe("overdue");
  });

  it("reads a task due at exactly midnight today as upcoming, not overdue", () => {
    // The boundary the backend's filter rules use: overdue is strictly BEFORE the start of today.
    expect(taskStatus(task({ dueDate: isoAt(2026, 5, 28, 0, 0) }), DAY_START)).toBe("upcoming");
  });

  it("reads a task due later today as upcoming", () => {
    expect(taskStatus(task({ dueDate: isoAt(2026, 5, 28, 9, 0) }), DAY_START)).toBe("upcoming");
  });

  it("reads an open task with no due date as upcoming", () => {
    // Only the admin app can create one; the storefront's inputs make the due date non-null.
    expect(taskStatus(task({ dueDate: null }), DAY_START)).toBe("upcoming");
  });
});

describe("buildDayMarkers", () => {
  it("collapses many tasks of one kind on a day into a single marker", () => {
    const markers = buildDayMarkers([
      { dueDate: isoAt(2026, 5, 28, 8), status: "overdue" },
      { dueDate: isoAt(2026, 5, 28, 12), status: "overdue" },
      { dueDate: isoAt(2026, 5, 28, 17), status: "overdue" },
    ]);

    expect(markers["2026-05-28"]).toEqual({ kinds: ["overdue"], count: 3 });
  });

  it("keeps one marker per distinct kind, in a stable order", () => {
    const markers = buildDayMarkers([
      { dueDate: isoAt(2026, 5, 28, 8), status: "completed" },
      { dueDate: isoAt(2026, 5, 28, 9), status: "upcoming" },
      { dueDate: isoAt(2026, 5, 28, 10), status: "overdue" },
      { dueDate: isoAt(2026, 5, 28, 11), status: "upcoming" },
    ]);

    // Declaration order, not arrival order, so the dots do not reshuffle between renders.
    expect(markers["2026-05-28"].kinds).toEqual(["upcoming", "overdue", "completed"]);
  });

  it("buckets by the viewer's day and skips tasks with no due date", () => {
    const markers = buildDayMarkers([
      { dueDate: isoAt(2026, 5, 28, 23, 30), status: "upcoming" },
      { dueDate: isoAt(2026, 5, 29, 0, 30), status: "overdue" },
      { dueDate: null, status: "upcoming" },
    ]);

    // ISO keys sort chronologically under a plain string compare; the comparator is explicit for clarity.
    expect(Object.keys(markers).sort((a, b) => a.localeCompare(b))).toEqual(["2026-05-28", "2026-05-29"]);
  });

  it("honours a caller-supplied kind order and drops kinds outside it", () => {
    const order: SalesRepTaskStatusType[] = ["overdue", "upcoming"];
    const markers = buildDayMarkers(
      [
        { dueDate: isoAt(2026, 5, 28, 8), status: "completed" },
        { dueDate: isoAt(2026, 5, 28, 9), status: "upcoming" },
        { dueDate: isoAt(2026, 5, 28, 10), status: "overdue" },
      ],
      order,
    );

    expect(markers["2026-05-28"].kinds).toEqual(["overdue", "upcoming"]);
  });

  // The dots collapse a day to one mark per condition; the count is what survives that collapse, and the
  // only thing a screen reader can be told about how much is actually there.
  it("counts every task due on the day, however few dots it earns", () => {
    const markers = buildDayMarkers([
      { dueDate: isoAt(2026, 5, 28, 8), status: "overdue" },
      { dueDate: isoAt(2026, 5, 28, 12), status: "overdue" },
      { dueDate: isoAt(2026, 5, 28, 17), status: "upcoming" },
    ]);

    expect(markers["2026-05-28"]).toEqual({ kinds: ["upcoming", "overdue"], count: 3 });
  });

  // A canceled task earns no dot but is still due that day, and still shows up in the day's list.
  it("counts a task whose kind draws nothing", () => {
    const markers = buildDayMarkers([
      { dueDate: isoAt(2026, 5, 28, 8), status: "upcoming" },
      { dueDate: isoAt(2026, 5, 28, 9), status: "canceled" },
    ]);

    expect(markers["2026-05-28"]).toEqual({ kinds: ["upcoming"], count: 2 });
  });
});

describe("localDayWindow", () => {
  it("spans exactly the one local day, inclusive", () => {
    const { from, to } = localDayWindow("2026-05-28");

    expect(new Date(from).getTime()).toBe(new Date(2026, 4, 28, 0, 0, 0, 0).getTime());
    expect(new Date(to).getTime()).toBe(new Date(2026, 4, 28, 23, 59, 59, 999).getTime());
  });
});

describe("localCalendarWindow", () => {
  it("pads either side, because the grid renders adjacent-month days that also carry dots", () => {
    const { from, to } = localCalendarWindow("2026-05-01");

    expect(new Date(from).getTime()).toBe(new Date(2026, 3, 24).getTime());
    expect(new Date(to).getTime()).toBe(new Date(2026, 5, 14, 23, 59, 59, 999).getTime());
  });

  // The edges are not symmetric. February 2026 starts on a Sunday, so a 42-cell grid shows 28 own days and then
  // 14 of March - the worst case, and exactly what a 7-day trailing pad used to leave dotless.
  it("reaches the last cell of the worst-case grid", () => {
    const { from, to } = localCalendarWindow("2026-02-01");

    expect(new Date(from).getTime()).toBeLessThanOrEqual(new Date(2026, 1, 1).getTime());
    expect(new Date(to).getTime()).toBeGreaterThanOrEqual(new Date(2026, 2, 14, 23, 59, 59, 999).getTime());
  });

  it("crosses a year boundary correctly", () => {
    const { from } = localCalendarWindow("2026-01-01");

    expect(new Date(from).getFullYear()).toBe(2025);
    expect(new Date(from).getMonth()).toBe(11);
  });
});

describe("toMonthKey", () => {
  it("normalizes any day to the first of its month", () => {
    expect(toMonthKey("2026-05-28")).toBe("2026-05-01");
    expect(toMonthKey(new Date(2026, 11, 31))).toBe("2026-12-01");
  });
});

describe("localDayKeyToIso", () => {
  it("round-trips through localDayKey", () => {
    expect(localDayKey(localDayKeyToIso("2026-05-28"))).toBe("2026-05-28");
  });

  it("lands on local midnight, so the picked day is the day the calendar shows it on", () => {
    expect(new Date(localDayKeyToIso("2026-05-28")).getTime()).toBe(new Date(2026, 4, 28).getTime());
  });
});

describe("dueDateForDay", () => {
  // Built from a local Date, so the fixture sits on the same local day whatever timezone the suite runs in.
  const TIMED = new Date(2026, 4, 28, 7, 15, 30, 250).toISOString();

  // Creating: nothing earlier to preserve, so the task starts at the beginning of the chosen day.
  it("starts a new task at the beginning of the chosen day", () => {
    expect(dueDateForDay("2026-05-28")).toBe(localDayKeyToIso("2026-05-28"));
  });

  // The point of the whole function: update REPLACES the task, so an untouched date has to round-trip byte
  // for byte, or editing the title alone moves the deadline to midnight (VCST-5732 QA A-1).
  it("returns the original instant untouched when the day did not change", () => {
    expect(dueDateForDay("2026-05-28", TIMED)).toBe(TIMED);
  });

  it("carries the time of day over when the task moves to another day", () => {
    const moved = new Date(dueDateForDay("2026-05-29", TIMED));

    expect(localDayKey(moved)).toBe("2026-05-29");
    expect([moved.getHours(), moved.getMinutes(), moved.getSeconds(), moved.getMilliseconds()]).toEqual([
      7, 15, 30, 250,
    ]);
  });

  // A task the rep created here has no time to keep; it must not gain one on the way out either.
  it("keeps a midnight task at midnight when it moves", () => {
    const moved = dueDateForDay("2026-05-29", localDayKeyToIso("2026-05-28"));

    expect(moved).toBe(localDayKeyToIso("2026-05-29"));
  });
});

// VCST-6314: calls are due at a time of day, picked in steps on the rep's own clock.
describe("isCallTask", () => {
  // The type is a dictionary value, and an admin can retype it.
  it("reads the call type in any case", () => {
    expect(isCallTask("Call")).toBe(true);
    expect(isCallTask("call")).toBe(true);
  });

  it("reads nothing else as a call", () => {
    expect(isCallTask("Callback")).toBe(false);
    expect(isCallTask("")).toBe(false);
    expect(isCallTask(undefined)).toBe(false);
  });
});

describe("time of day", () => {
  it("keys a time on the user's clock", () => {
    expect(localTimeKey(new Date(2026, 9, 15, 9, 5))).toBe("09:05");
  });

  it("puts a picked day and time back together on the user's clock", () => {
    const due = new Date(dueDateForDayAndTime("2026-10-15", "14:45"));

    expect(localDayKey(due)).toBe("2026-10-15");
    expect([due.getHours(), due.getMinutes(), due.getSeconds()]).toEqual([14, 45, 0]);
  });

  // What the form writes is what it reads back when the call is opened again.
  it("round-trips through the instant the API stores", () => {
    const stored = dueDateForDayAndTime("2026-10-15", "09:00");

    expect([localDayKey(stored), localTimeKey(stored)]).toEqual(["2026-10-15", "09:00"]);
  });

  // updateSalesRepTask replaces the task, so an edit that moves neither must not rewrite the instant: seconds set
  // elsewhere would be dropped, as dueDateForDay already guards against for any other task.
  it("gives back the stored instant, byte for byte, when neither the day nor the time moved", () => {
    const original = new Date(2026, 9, 15, 9, 30, 15, 250).toISOString();

    expect(dueDateForDayAndTime("2026-10-15", "09:30", original)).toBe(original);
  });

  it("rebuilds the instant once the time or the day moves", () => {
    const original = new Date(2026, 9, 15, 9, 30, 15, 250).toISOString();

    const later = new Date(dueDateForDayAndTime("2026-10-15", "10:00", original));
    const nextDay = new Date(dueDateForDayAndTime("2026-10-16", "09:30", original));

    expect([localDayKey(later), later.getHours(), later.getMinutes(), later.getSeconds()]).toEqual([
      "2026-10-15",
      10,
      0,
      0,
    ]);
    expect([localDayKey(nextDay), nextDay.getHours(), nextDay.getMinutes(), nextDay.getSeconds()]).toEqual([
      "2026-10-16",
      9,
      30,
      0,
    ]);
  });

  // The locale decides 12- or 24-hour; the time select lists its times with this, and a call's line shows its time.
  it("writes a time of day the way the locale does", () => {
    const due = new Date(2026, 9, 15, 14, 30);

    expect(formatTimeOfDay(due, "en-US")).toMatch(/^2:30\sPM$/);
    expect(formatTimeOfDay(due, "fr-FR")).toBe("14:30");
    expect(formatTimeOfDay(due.toISOString(), "fr-FR")).toBe("14:30");
  });

  it("offers every quarter hour of the day, from midnight", () => {
    const keys = timeKeysOfDay(15);

    expect(keys).toHaveLength(96);
    expect(keys.slice(0, 3)).toEqual(["00:00", "00:15", "00:30"]);
    expect(keys.at(-1)).toBe("23:45");
  });

  it("gives a time key a date to format the time of day from", () => {
    const date = timeKeyToDate("09:30");

    expect([date.getHours(), date.getMinutes()]).toEqual([9, 30]);
  });
});

describe("taskSubline", () => {
  const t = ((key: string, named?: Record<string, unknown>) =>
    named ? `${key}(${String(named.date)})` : key) as unknown as ComposerTranslation;
  // The host's named date only: a call's time comes from formatTimeOfDay, never from `d`, whose `short` falls back
  // to English in every locale the host defines no formats for.
  const d = (_value: number | Date | string, options: DateTimeOptions) => {
    expect(options).toEqual({ key: "short" });
    return "Oct 15";
  };
  // 9:00 on the rep's own clock, whatever timezone the suite runs in.
  const CALL_DUE_DATE = new Date(2026, 9, 15, 9, 0).toISOString();

  function makeTask(overrides: Partial<SalesRepTaskType> = {}): SalesRepTaskType {
    return {
      id: "task-1",
      name: "Call",
      description: "",
      type: "",
      priority: "Normal",
      dueDate: localDayKeyToIso("2026-10-15"),
      isActive: true,
      status: "upcoming",
      ...overrides,
    };
  }

  it("gives a call its time of day and its customer", () => {
    const task = makeTask({ type: "Call", dueDate: CALL_DUE_DATE, customerName: "Lakeview Outfitters" });

    expect(taskSubline(task, t, d, "en-US")).toBe(
      `sales_rep.tasks.due_relative.due(Oct 15, ${formatTimeOfDay(CALL_DUE_DATE, "en-US")}) · Lakeview Outfitters · Call`,
    );
  });

  // The popup's time select shows 14:30 to a French rep, so the list must not say 2:30 PM.
  it("writes a call's time as the time select does, in the rep's locale", () => {
    const dueDate = new Date(2026, 9, 15, 14, 30).toISOString();
    const task = makeTask({ type: "Call", dueDate, customerName: "Lakeview Outfitters" });

    expect(taskSubline(task, t, d, "fr-FR")).toBe(
      "sales_rep.tasks.due_relative.due(Oct 15, 14:30) · Lakeview Outfitters · Call",
    );
  });

  // No customer picked, or one the rep no longer serves: the API names neither, and a call to nobody would mislead.
  it("says a call has no customer to name", () => {
    expect(taskSubline(makeTask({ type: "Call", dueDate: CALL_DUE_DATE }), t, d, "en-US")).toBe(
      `sales_rep.tasks.due_relative.due(Oct 15, ${formatTimeOfDay(CALL_DUE_DATE, "en-US")}) · sales_rep.tasks.customer_not_assigned · Call`,
    );
  });

  it("keeps any other task to its date", () => {
    expect(taskSubline(makeTask({ type: "Other" }), t, d, "fr-FR")).toBe(
      "sales_rep.tasks.due_relative.due(Oct 15) · Other",
    );
  });

  it("names the customer of any task", () => {
    expect(taskSubline(makeTask({ type: "Other", customerName: "Cabin Co." }), t, d, "en-US")).toBe(
      "sales_rep.tasks.due_relative.due(Oct 15) · Cabin Co. · Other",
    );
  });

  // The Tasks table gives the customer a column of its own, so its line leaves the customer out, a call's too.
  it("leaves the customer out when a column names it", () => {
    const options = { withCustomer: false };

    expect(taskSubline(makeTask({ type: "Other", customerName: "Cabin Co." }), t, d, "en-US", options)).toBe(
      "sales_rep.tasks.due_relative.due(Oct 15) · Other",
    );
    expect(taskSubline(makeTask({ type: "Call", dueDate: CALL_DUE_DATE }), t, d, "en-US", options)).toBe(
      `sales_rep.tasks.due_relative.due(Oct 15, ${formatTimeOfDay(CALL_DUE_DATE, "en-US")}) · Call`,
    );
  });
});

describe("taskFilterVariables", () => {
  it("passes every filter that is set", () => {
    expect(
      taskFilterVariables({ keyword: "renewal", status: "overdue", customerIds: ["org-1"], priorities: ["High"] }),
    ).toEqual({ keyword: "renewal", filter: "overdue", customerIds: ["org-1"], priorities: ["High"] });
  });

  // An argument sent empty would still change the request, and with it the Apollo cache entry, from the one a
  // surface without filters asks for.
  it("leaves out the filters that are not set", () => {
    expect(taskFilterVariables(emptyTaskFilters())).toEqual({
      keyword: undefined,
      filter: undefined,
      customerIds: undefined,
      priorities: undefined,
    });
    expect(taskFilterVariables()).toEqual(taskFilterVariables(emptyTaskFilters()));
  });
});

describe("taskPriorityLabelKey", () => {
  it("names a priority by its lower-cased name", () => {
    expect(taskPriorityLabelKey("Highest")).toBe("sales_rep.tasks.priority.highest");
  });
});
