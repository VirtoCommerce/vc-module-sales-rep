import { eod, iso, local } from "./utils";

/** Periods the Reporting page offers; `year` is the current calendar year, `q1`–`q4` its quarters. */
export const REPORTING_PERIODS = ["6m", "12m", "year", "q1", "q2", "q3", "q4"] as const;
export type ReportingPeriodType = (typeof REPORTING_PERIODS)[number];
export const REPORTING_DEFAULT_PERIOD: ReportingPeriodType = "year";

/** The query carries this many month slots; no period is longer. */
export const REPORTING_MONTH_SLOTS = 12;

const MONTHS_IN_QUARTER = 3;
const ROLLING_MONTHS: Partial<Record<ReportingPeriodType, number>> = { "6m": 6, "12m": 12 };
const QUARTERS: Partial<Record<ReportingPeriodType, number>> = { q1: 0, q2: 1, q3: 2, q4: 3 };

export type ReportingMonthStateType = "closed" | "current" | "upcoming";

export type ReportingMonthSlotType = {
  year: number;
  /** 0-based, as `Date#getMonth`. */
  month: number;
  state: ReportingMonthStateType;
  from: string;
  /** End of today for the current month (see buildStatisticsWindows on why), end of the month otherwise. */
  to: string;
  lyFrom: string;
  /** Always the whole month last year: the current month compares its projection, which is a whole month too. */
  lyTo: string;
  daysPassed: number;
  daysInMonth: number;
};

export type ReportingPeriodTotalsType = { amount: number; count: number };

/** One month slot's raw figures, and the Cancelled share to take out of them should the statistics include it. */
export type ReportingRawMonthType = {
  all?: ReportingPeriodTotalsType;
  cancelled?: ReportingPeriodTotalsType;
  lastYear?: ReportingPeriodTotalsType;
  lastYearCancelled?: ReportingPeriodTotalsType;
};

export type ReportingMonthType = ReportingMonthSlotType & {
  /** Net of cancelled orders; null for an upcoming month. */
  revenue: number | null;
  orders: number | null;
  /** null when there are no orders (shown as "—", never as 0 or NaN). */
  aov: number | null;
  /** Month-end projection; set for the current month only, once at least one day has passed. */
  projectedRevenue: number | null;
  lastYearRevenue: number | null;
  lastYearOrders: number | null;
  lastYearAov: number | null;
  /** Percent vs the same month last year; null without a last-year baseline. */
  revenueChange: number | null;
  aovChange: number | null;
  /** Percent vs the month before (the current month compares its projection); null without a baseline. */
  revenueMonthChange: number | null;
  aovMonthChange: number | null;
};

export type ReportingCurrentMonthType = {
  year: number;
  month: number;
  revenue: number;
  projectedRevenue: number | null;
  daysPassed: number;
  daysInMonth: number;
};

export type ReportingSummaryType = {
  averageRevenuePerClosedMonth: number | null;
  /** Revenue / orders over every month that has data, not an average of monthly AOVs. */
  periodAov: number | null;
  /** Period totals over the months with data. */
  revenue: number;
  orders: number;
  /**
   * Percent vs the same months last year. The current month's last-year figure is prorated to the same day, so a
   * partial month is compared with a partial month.
   */
  revenueChange: number | null;
  ordersChange: number | null;
  aovChange: number | null;
  currentMonth: ReportingCurrentMonthType | null;
};

function daysIn(year: number, month: number): number {
  return new Date(year, month + 1, 0).getDate();
}

function firstMonthOf(period: ReportingPeriodType, now: Date): { year: number; month: number; length: number } {
  const year = now.getFullYear();
  const rolling = ROLLING_MONTHS[period];
  if (rolling) {
    return { year, month: now.getMonth() - rolling + 1, length: rolling };
  }
  const quarter = QUARTERS[period];
  if (quarter !== undefined) {
    return { year, month: quarter * MONTHS_IN_QUARTER, length: MONTHS_IN_QUARTER };
  }
  return { year, month: 0, length: REPORTING_MONTH_SLOTS };
}

/** The months a period covers, oldest first, each with its own and last year's date window. */
export function buildReportingMonths(period: ReportingPeriodType, now: Date = new Date()): ReportingMonthSlotType[] {
  const start = firstMonthOf(period, now);
  const todayYear = now.getFullYear();
  const todayMonth = now.getMonth();

  return Array.from({ length: start.length }, (_, i) => {
    // Date normalizes a negative or overflowing month into the right year.
    const first = new Date(start.year, start.month + i, 1);
    const year = first.getFullYear();
    const month = first.getMonth();
    const daysInMonth = daysIn(year, month);
    const isCurrent = year === todayYear && month === todayMonth;
    const isUpcoming = year > todayYear || (year === todayYear && month > todayMonth);

    let state: ReportingMonthStateType = "closed";
    if (isCurrent) {
      state = "current";
    } else if (isUpcoming) {
      state = "upcoming";
    }

    return {
      year,
      month,
      state,
      from: iso(local(year, month, 1)),
      to: iso(isCurrent ? eod(year, month, now.getDate()) : eod(year, month, daysInMonth)),
      lyFrom: iso(local(year - 1, month, 1)),
      lyTo: iso(eod(year - 1, month, daysIn(year - 1, month))),
      daysPassed: isCurrent ? now.getDate() : daysInMonth,
      daysInMonth,
    };
  });
}

function net(all?: ReportingPeriodTotalsType, cancelled?: ReportingPeriodTotalsType): ReportingPeriodTotalsType | null {
  if (!all) {
    return null;
  }
  // Clamped: the two slices are separate aggregations, so a status change between them must not go negative.
  return {
    amount: Math.max(0, all.amount - (cancelled?.amount ?? 0)),
    count: Math.max(0, all.count - (cancelled?.count ?? 0)),
  };
}

function average(totals: ReportingPeriodTotalsType | null): number | null {
  return totals && totals.count > 0 ? totals.amount / totals.count : null;
}

/** Percent change, or null when there is no positive baseline to compare with. */
export function percentChange(current: number | null, previous: number | null): number | null {
  if (current === null || previous === null || previous <= 0) {
    return null;
  }
  return ((current - previous) / previous) * 100;
}

export function toReportingMonth(slot: ReportingMonthSlotType, raw: ReportingRawMonthType): ReportingMonthType {
  const isUpcoming = slot.state === "upcoming";
  const current = isUpcoming ? null : net(raw.all, raw.cancelled);
  const lastYear = net(raw.lastYear, raw.lastYearCancelled);

  const canProject = slot.state === "current" && slot.daysPassed > 0 && current !== null;
  const factor = slot.daysInMonth / slot.daysPassed;
  const projectedRevenue = canProject ? current.amount * factor : null;

  const revenue = current?.amount ?? null;
  const aov = average(current);
  const lastYearRevenue = lastYear?.amount ?? null;
  const lastYearAov = average(lastYear);

  return {
    ...slot,
    revenue,
    orders: current?.count ?? null,
    aov,
    projectedRevenue,
    lastYearRevenue,
    lastYearOrders: lastYear?.count ?? null,
    lastYearAov,
    revenueChange: percentChange(projectedRevenue ?? revenue, lastYearRevenue),
    aovChange: percentChange(aov, lastYearAov),
    revenueMonthChange: null,
    aovMonthChange: null,
  };
}

/** The month just before a period starts: the baseline for the first month's "vs previous month". */
export function previousMonthWindow(period: ReportingPeriodType, now: Date = new Date()): { from: string; to: string } {
  const start = firstMonthOf(period, now);
  const first = new Date(start.year, start.month - 1, 1);
  const year = first.getFullYear();
  const month = first.getMonth();
  return { from: iso(local(year, month, 1)), to: iso(eod(year, month, daysIn(year, month))) };
}

/** Fills the "vs previous month" figures; `before` is the month preceding the first one. */
export function withMonthChanges(
  months: ReportingMonthType[],
  before: ReportingRawMonthType = {},
): ReportingMonthType[] {
  const baseline = net(before.all, before.cancelled);
  let previousRevenue = baseline?.amount ?? null;
  let previousAov = average(baseline);

  return months.map((month) => {
    const result = {
      ...month,
      revenueMonthChange: percentChange(month.projectedRevenue ?? month.revenue, previousRevenue),
      aovMonthChange: percentChange(month.aov, previousAov),
    };
    previousRevenue = month.revenue;
    previousAov = month.aov;
    return result;
  });
}

export function summarizeReporting(months: ReportingMonthType[]): ReportingSummaryType {
  const closed = months.filter((month) => month.state === "closed");
  const withData = months.filter((month) => month.state !== "upcoming");

  const sum = (items: ReportingMonthType[], pick: (month: ReportingMonthType) => number | null): number =>
    items.reduce((total, month) => total + (pick(month) ?? 0), 0);
  // Last year's figure for the same span: the current month only up to the same day.
  const share = (month: ReportingMonthType): number =>
    month.state === "current" ? month.daysPassed / month.daysInMonth : 1;
  const hasLastYear = withData.some((month) => month.lastYearRevenue !== null);

  const revenue = sum(withData, (month) => month.revenue);
  const orders = sum(withData, (month) => month.orders);
  const lastYearRevenue = hasLastYear ? sum(withData, (month) => (month.lastYearRevenue ?? 0) * share(month)) : null;
  const lastYearOrders = hasLastYear ? sum(withData, (month) => (month.lastYearOrders ?? 0) * share(month)) : null;
  const periodAov = orders > 0 ? revenue / orders : null;
  const lastYearAov = lastYearRevenue !== null && lastYearOrders ? lastYearRevenue / lastYearOrders : null;

  const current = months.find((month) => month.state === "current");

  return {
    averageRevenuePerClosedMonth: closed.length ? sum(closed, (month) => month.revenue) / closed.length : null,
    periodAov,
    revenue,
    orders,
    revenueChange: percentChange(revenue, lastYearRevenue),
    ordersChange: percentChange(orders, lastYearOrders),
    aovChange: percentChange(periodAov, lastYearAov),
    currentMonth: current
      ? {
          year: current.year,
          month: current.month,
          revenue: current.revenue ?? 0,
          projectedRevenue: current.projectedRevenue,
          daysPassed: current.daysPassed,
          daysInMonth: current.daysInMonth,
        }
      : null,
  };
}

const NICE_STEPS = [1, 2, 2.5, 5, 10];

/** Axis ticks from 0 to a round maximum that covers `max`, `count` intervals. */
export function niceTicks(max: number, count = 4): number[] {
  if (!Number.isFinite(max) || max <= 0) {
    return Array.from({ length: count + 1 }, (_, i) => i);
  }
  const rawStep = max / count;
  const magnitude = 10 ** Math.floor(Math.log10(rawStep));
  const step = (NICE_STEPS.find((nice) => nice * magnitude >= rawStep) ?? 10) * magnitude;
  return Array.from({ length: count + 1 }, (_, i) => i * step);
}

export type ChartPointType = { x: number; y: number };

const SVG_PRECISION = 100;

function round(value: number): number {
  return Math.round(value * SVG_PRECISION) / SVG_PRECISION;
}

/**
 * SVG path through every point as a smooth curve (monotone cubic, Fritsch–Carlson): peaks and dips are rounded with a
 * flat tangent, and the curve never overshoots a point, so it can't suggest a value the data doesn't have.
 */
export function smoothLinePath(points: ChartPointType[]): string {
  if (points.length === 0) {
    return "";
  }
  const [first] = points;
  if (points.length === 1) {
    return `M${round(first.x)},${round(first.y)}`;
  }

  const slopes = points.slice(0, -1).map((point, i) => {
    const next = points[i + 1];
    return (next.y - point.y) / (next.x - point.x);
  });

  const tangents = points.map((_, i) => {
    if (i === 0) {
      return slopes[0];
    }
    if (i === points.length - 1) {
      return slopes[i - 1];
    }
    const before = slopes[i - 1];
    const after = slopes[i];
    // A peak or a dip (the slope changes sign): a flat tangent rounds it off.
    return before * after <= 0 ? 0 : (before + after) / 2;
  });

  slopes.forEach((slope, i) => {
    if (slope === 0) {
      tangents[i] = 0;
      tangents[i + 1] = 0;
      return;
    }
    const a = tangents[i] / slope;
    const b = tangents[i + 1] / slope;
    const length = a * a + b * b;
    // Keeps each segment monotone, so the curve stays between its two points.
    if (length > 9) {
      const scale = 3 / Math.sqrt(length);
      tangents[i] = scale * a * slope;
      tangents[i + 1] = scale * b * slope;
    }
  });

  const segments = points.slice(0, -1).map((point, i) => {
    const next = points[i + 1];
    const third = (next.x - point.x) / 3;
    const c1 = { x: point.x + third, y: point.y + tangents[i] * third };
    const c2 = { x: next.x - third, y: next.y - tangents[i + 1] * third };
    return `C${round(c1.x)},${round(c1.y)} ${round(c2.x)},${round(c2.y)} ${round(next.x)},${round(next.y)}`;
  });

  return `M${round(first.x)},${round(first.y)} ${segments.join(" ")}`;
}
