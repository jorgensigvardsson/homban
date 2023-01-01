import { MakeDuration as mdur, isDaily, isHalfYearly, isMonthly, isQuarterly, isWeekly, isYearly } from './duration';


test("isYearly returns true for years = 1 and rest = 0", () => {
	expect(isYearly(mdur({ years: 1}))).toBe(true)
})

test("isYearly returns false for years = 1 and rest != 0", () => {
	expect(isYearly(mdur({ years: 1, halfYears: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, quarters: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, months: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, weeks: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, days: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, hours: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, minutes: 1}))).toBe(false);
	expect(isYearly(mdur({ years: 1, seconds: 1}))).toBe(false);
})

test("isHalfYearly returns true for halfYears = 1 and rest = 0", () => {
	expect(isHalfYearly(mdur({ halfYears: 1}))).toBe(true)
})

test("isHalfYearly returns false for years = 1 and rest != 0", () => {
	expect(isHalfYearly(mdur({ halfYears: 1, years: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, quarters: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, months: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, weeks: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, days: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, hours: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, minutes: 1}))).toBe(false);
	expect(isHalfYearly(mdur({ halfYears: 1, seconds: 1}))).toBe(false);
})

test("isQuarterly returns true for halfYears = 1 and rest = 0", () => {
	expect(isQuarterly(mdur({ quarters: 1}))).toBe(true)
})

test("isQuarterly returns false for years = 1 and rest != 0", () => {
	expect(isQuarterly(mdur({ quarters: 1, years: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, halfYears: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, months: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, weeks: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, days: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, hours: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, minutes: 1}))).toBe(false);
	expect(isQuarterly(mdur({ quarters: 1, seconds: 1}))).toBe(false);
})

test("isMonthly returns true for halfYears = 1 and rest = 0", () => {
	expect(isMonthly(mdur({ months: 1}))).toBe(true)
})

test("isMonthly returns false for years = 1 and rest != 0", () => {
	expect(isMonthly(mdur({ months: 1, years: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, halfYears: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, quarters: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, weeks: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, days: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, hours: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, minutes: 1}))).toBe(false);
	expect(isMonthly(mdur({ months: 1, seconds: 1}))).toBe(false);
})

test("isWeekly returns true for halfYears = 1 and rest = 0", () => {
	expect(isWeekly(mdur({ weeks: 1}))).toBe(true)
})

test("isWeekly returns false for years = 1 and rest != 0", () => {
	expect(isWeekly(mdur({ weeks: 1, years: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, quarters: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, months: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, halfYears: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, days: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, hours: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, minutes: 1}))).toBe(false);
	expect(isWeekly(mdur({ weeks: 1, seconds: 1}))).toBe(false);
})

test("isDaily returns true for halfYears = 1 and rest = 0", () => {
	expect(isDaily(mdur({ days: 1}))).toBe(true)
})

test("isDaily returns false for years = 1 and rest != 0", () => {
	expect(isDaily(mdur({ days: 1, years: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, quarters: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, months: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, weeks: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, halfYears: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, hours: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, minutes: 1}))).toBe(false);
	expect(isDaily(mdur({ days: 1, seconds: 1}))).toBe(false);
})