/** Derived, never stored. The backend exposes isActive/completed/dueDate and leaves the reading to us. */
export type SalesRepTaskStatusType = "upcoming" | "overdue" | "completed" | "canceled";

export type SalesRepTaskType = {
  id: string;
  name: string;
  description: string;
  type: string;
  priority: string;
  /** ISO instant. Always present on a task a rep created — the create/update inputs declare it non-null. */
  dueDate?: string;
  isActive: boolean;
  completed?: boolean;
  status: SalesRepTaskStatusType;
  /** The organization the task is about; absent when there is none, or the rep no longer serves it. */
  customerId?: string;
  customerName?: string;
};

/** What a calendar day carries: which conditions to dot, and how many tasks are due on it. */
export type SalesRepTaskDayType = {
  /** Presence of each condition, never a count — ten overdue tasks on a day carry one "overdue". */
  kinds: SalesRepTaskStatusType[];
  /** Announced, not drawn: there is no room for a dot per task, but plenty in the description. */
  count: number;
};

export type SalesRepTaskDayMarkersType = Record<string, SalesRepTaskDayType>;

/** What the Tasks page narrows its list and its calendar dots by, besides the day. Each one intersects with the rest. */
export type SalesRepTaskFiltersType = {
  /** Matched against the name and the notes; "" for none. */
  keyword: string;
  /** A rule name from salesRepTaskFilterRules; undefined for every status. */
  status?: string;
  /** Organization ids; empty for every customer. */
  customerIds: string[];
  /** TaskPriority names; empty for every priority. */
  priorities: string[];
};

export type SalesRepTaskInputType = {
  name: string;
  dueDate: string;
  description?: string;
  type?: string;
  priority?: string;
  customerId?: string;
};
