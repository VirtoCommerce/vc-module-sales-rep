import { describe, expect, it } from "vitest";
import { callEvent, googleCalendarUrl, icsFile } from "./calendar";

// UTC in, UTC out: an event leaves the storefront as an instant, so literal ISO strings are safe here, unlike in
// tasks.test.ts, where a day is a day on the viewer's calendar.
const CALL = {
  id: "0b9e8c0e-1d2f-4a6b-9c3d-5e7f8a9b0c1d",
  name: "Call",
  description: "Ask about the Q4 order",
  dueDate: "2026-10-20T13:00:00.000Z",
  customerName: "Lakeview Outfitters",
};

const STAMP = new Date("2026-10-15T08:00:00.000Z");

/** The lines a calendar reads: each fold (CRLF and one space) undone, RFC 5545 section 3.1. */
function unfold(ics: string): string[] {
  return ics.replace(/\r\n /g, "").split("\r\n");
}

function property(ics: string, name: string): string | undefined {
  return unfold(ics)
    .find((line) => line.startsWith(`${name}:`))
    ?.slice(name.length + 1);
}

describe("callEvent", () => {
  it("names the customer after the title, and lasts half an hour from the due time", () => {
    expect(callEvent(CALL)).toEqual({
      uid: CALL.id,
      title: "Call — Lakeview Outfitters",
      start: new Date("2026-10-20T13:00:00.000Z"),
      end: new Date("2026-10-20T13:30:00.000Z"),
      description: "Ask about the Q4 order",
    });
  });

  it("keeps the title alone when the call has no customer to name", () => {
    expect(callEvent({ ...CALL, customerName: undefined }).title).toBe("Call");
  });

  it("leaves out empty notes", () => {
    expect(callEvent({ ...CALL, description: "" }).description).toBeUndefined();
  });

  // Outlook's subject limit, the lowest known; Google and Apple document none.
  it("cuts a title past 255 characters, and marks the cut", () => {
    const { title } = callEvent({ ...CALL, name: "x".repeat(250) });

    expect(title).toHaveLength(255);
    expect(title.endsWith("…")).toBe(true);
  });
});

describe("googleCalendarUrl", () => {
  it("opens Google's new-event page with the title, the times in UTC and the notes", () => {
    const url = new URL(googleCalendarUrl(callEvent(CALL)));

    expect(`${url.origin}${url.pathname}`).toBe("https://calendar.google.com/calendar/render");
    expect(Object.fromEntries(url.searchParams)).toEqual({
      action: "TEMPLATE",
      text: "Call — Lakeview Outfitters",
      dates: "20261020T130000Z/20261020T133000Z",
      details: "Ask about the Q4 order",
    });
  });

  it("sends no details for a call without notes", () => {
    const url = new URL(googleCalendarUrl(callEvent({ ...CALL, description: "" })));

    expect(url.searchParams.has("details")).toBe(false);
  });

  // Google answers 400 to a link past ~16,000 characters, and a character outside ASCII takes up to nine once
  // encoded. The longest call the form can save: a 256-character title, a 512-character customer, 1000 of notes.
  it("stays within the link length Google accepts, for the longest call the form can save", () => {
    const longest = { ...CALL, name: "通".repeat(256), customerName: "客".repeat(512), description: "备".repeat(1000) };

    expect(googleCalendarUrl(callEvent(longest)).length).toBeLessThan(16_000);
  });
});

describe("icsFile", () => {
  it("writes one timed event in UTC, identified by the task's id", () => {
    expect(unfold(icsFile(callEvent(CALL), STAMP))).toEqual([
      "BEGIN:VCALENDAR",
      "VERSION:2.0",
      "PRODID:-//Virto Commerce//Sales Rep Hub//EN",
      "METHOD:PUBLISH",
      "BEGIN:VEVENT",
      `UID:${CALL.id}`,
      "DTSTAMP:20261015T080000Z",
      "DTSTART:20261020T130000Z",
      "DTEND:20261020T133000Z",
      "SUMMARY:Call — Lakeview Outfitters",
      "DESCRIPTION:Ask about the Q4 order",
      "END:VEVENT",
      "END:VCALENDAR",
      "",
    ]);
  });

  it("ends every line with CRLF, the last one included", () => {
    const ics = icsFile(callEvent(CALL), STAMP);

    expect(ics.endsWith("\r\n")).toBe(true);
    expect(ics.replace(/\r\n/g, "")).not.toMatch(/[\r\n]/);
  });

  it("escapes what iCalendar text reserves, and writes each line break as \\n", () => {
    const ics = icsFile(
      callEvent({ ...CALL, name: "Call; then mail, maybe", description: "Line 1\nLine 2\r\nC:\\quotes" }),
      STAMP,
    );

    expect(property(ics, "SUMMARY")).toBe("Call\\; then mail\\, maybe — Lakeview Outfitters");
    expect(property(ics, "DESCRIPTION")).toBe("Line 1\\nLine 2\\nC:\\\\quotes");
  });

  it("writes no DESCRIPTION for a call without notes", () => {
    expect(property(icsFile(callEvent({ ...CALL, description: "" }), STAMP), "DESCRIPTION")).toBeUndefined();
  });

  it("folds long lines at 75 octets, never inside a character", () => {
    const notes = "Звонок по заказу 📞 ".repeat(20);
    const lines = icsFile(callEvent({ ...CALL, description: notes }), STAMP).split("\r\n");

    expect(lines.filter((line) => line.startsWith(" ")).length).toBeGreaterThan(1);
    for (const line of lines) {
      const octets = new TextEncoder().encode(line);
      expect(octets.length).toBeLessThanOrEqual(75);
      // A character split across lines would leave half a surrogate pair behind, which UTF-8 cannot carry.
      expect(new TextDecoder().decode(octets)).toBe(line);
    }

    expect(property(lines.join("\r\n"), "DESCRIPTION")).toBe(notes);
  });
});
