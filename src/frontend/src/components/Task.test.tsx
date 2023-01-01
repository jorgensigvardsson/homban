import { MakeDuration as mdur } from '../duration';
import { pillPeriodicText } from './Task';

test("pillPeriodicText for yearly works", () => {
	expect(pillPeriodicText(mdur({ years: 1}))).toBe("Årligen")
	expect(pillPeriodicText(mdur({ halfYears: 2}))).toBe("Årligen")
	expect(pillPeriodicText(mdur({ quarters: 4}))).toBe("Årligen")
	expect(pillPeriodicText(mdur({ months: 12}))).toBe("Årligen")
})

test("pillPeriodicText for halfyear works", () => {
	expect(pillPeriodicText(mdur({ halfYears: 1 }))).toBe("Varje halvår")
	expect(pillPeriodicText(mdur({ quarters: 2 }))).toBe("Varje halvår")
	expect(pillPeriodicText(mdur({ months: 6 }))).toBe("Varje halvår")
})

test("pillPeriodicText for quarter works", () => {
	expect(pillPeriodicText(mdur({ quarters: 1 }))).toBe("Varje kvartal")
	expect(pillPeriodicText(mdur({ months: 3 }))).toBe("Varje kvartal")
})

test("pillPeriodicText for month works", () => {
	expect(pillPeriodicText(mdur({ months: 1 }))).toBe("Varje månad")
})

test("pillPeriodicText for bimonth works", () => {
	expect(pillPeriodicText(mdur({ months: 2 }))).toBe("Varannan månad")
})

test("pillPeriodicText for week works", () => {
	expect(pillPeriodicText(mdur({ weeks: 1 }))).toBe("Varje vecka")
})

test("pillPeriodicText for biweek works", () => {
	expect(pillPeriodicText(mdur({ weeks: 2 }))).toBe("Varannan vecka")
})

test("pillPeriodicText for daily works", () => {
	expect(pillPeriodicText(mdur({ days: 1 }))).toBe("Dagligen")
})

test("pillPeriodicText for bidaily works", () => {
	expect(pillPeriodicText(mdur({ days: 2 }))).toBe("Varannan dag")
})