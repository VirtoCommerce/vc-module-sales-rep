import { truncate } from "lodash-es";
import { CALL_DURATION_MINUTES } from "./constants";
import type { SalesRepTaskFieldsFragment } from "./api/graphql/types";

/**
 * A new call as an event for the rep's own calendar (VCST-6314). The event is a copy: Google's link and the .ics file
 * only ever create one, and the storefront cannot reach it afterwards, so nothing here updates an event.
 */
export type CalendarEventType = {
  uid: string;
  title: string;
  start: Date;
  end: Date;
  description?: string;
};

// Outlook caps a subject at 255 characters and opens the same .ics; Google and Apple document no limit. The cap also
// keeps the Google link under the ~16,000 characters Google accepts, as one character can take nine once encoded.
const TITLE_MAX_LENGTH = 255;

export const ICS_FILE_TYPE = "text/calendar;charset=utf-8";
export const ICS_FILE_NAME = "call.ics";

/**
 * The call as the API saved it, as a timed event of CALL_DURATION_MINUTES. The default title "Call" says nothing in a
 * calendar, so the customer's name joins it.
 */
export function callEvent(
  task: Pick<SalesRepTaskFieldsFragment, "id" | "name" | "description" | "dueDate" | "customerName">,
): CalendarEventType {
  const start = new Date(task.dueDate);
  const title = [task.name, task.customerName].filter(Boolean).join(" — ");

  return {
    uid: task.id,
    title: truncate(title, { length: TITLE_MAX_LENGTH, omission: "…" }),
    start,
    end: new Date(start.getTime() + CALL_DURATION_MINUTES * 60_000),
    description: task.description || undefined,
  };
}

/** Google's new-event page, filled in. Nothing is added until the rep saves it there. */
export function googleCalendarUrl(event: CalendarEventType): string {
  const params = new URLSearchParams({
    action: "TEMPLATE",
    text: event.title,
    dates: `${utcStamp(event.start)}/${utcStamp(event.end)}`,
  });

  if (event.description) {
    params.set("details", event.description);
  }

  return `https://calendar.google.com/calendar/render?${params}`;
}

/** The event as an iCalendar file (RFC 5545), which Apple Calendar, Outlook and the rest import. */
export function icsFile(event: CalendarEventType, stamp: Date = new Date()): string {
  const lines = [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Virto Commerce//Sales Rep Hub//EN",
    "METHOD:PUBLISH",
    "BEGIN:VEVENT",
    `UID:${event.uid}`,
    `DTSTAMP:${utcStamp(stamp)}`,
    `DTSTART:${utcStamp(event.start)}`,
    `DTEND:${utcStamp(event.end)}`,
    `SUMMARY:${escapeText(event.title)}`,
    ...(event.description ? [`DESCRIPTION:${escapeText(event.description)}`] : []),
    "END:VEVENT",
    "END:VCALENDAR",
  ];

  return lines.map(foldLine).join("\r\n") + "\r\n";
}

/** "20261020T130000Z": UTC, which both Google's link and iCalendar take without naming a time zone. */
function utcStamp(date: Date): string {
  return date.toISOString().replace(/[-:]|\.\d{3}/g, "");
}

/** RFC 5545 TEXT: backslash, semicolon and comma escaped, and each line break written as "\n". */
function escapeText(text: string): string {
  return text.replace(/[\\;,]/g, "\\$&").replace(/\r\n|\r|\n/g, "\\n");
}

// RFC 5545 folds a line longer than 75 octets, each continuation starting with a space. The fold falls between
// characters, never inside a character's UTF-8 bytes.
const MAX_LINE_OCTETS = 75;
const utf8 = new TextEncoder();

function foldLine(line: string): string {
  let folded = "";
  let octets = 0;

  for (const char of line) {
    const size = utf8.encode(char).length;
    if (octets + size > MAX_LINE_OCTETS) {
      folded += "\r\n ";
      octets = 1;
    }

    folded += char;
    octets += size;
  }

  return folded;
}
