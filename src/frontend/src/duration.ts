const durationRegex = /^((?<years>\d+)\s*y(ear(s)?)?)?\s*((?<halfYears>\d+)\s*ha(lfyear(s)?)?)?\s*((?<quarters>\d+)\s*q(uarter(s)?)?)?\s*((?<months>\d+)\s*mo(nth(s)?)?)?\s*((?<weeks>\d+)\s*w(eek(s)?)?)?\s*((?<days>\d+)\s*d(ay(s)?)?)?\s*((?<hours>\d+)\s*h(our(s)?)?)?\s*((?<minutes>\d+)\s*m(inute(s)?)?)?\s*((?<seconds>\d+)\s*s(econd(s)?)?)?\s*$/;

export function parseDuration(text: string): Duration | null {
	const match = text.match(durationRegex);
	if (!match)
		return null;

	return {
		years: match.groups?.years ? parseInt(match.groups.years) : 0,
		halfYears: match.groups?.halfYears ? parseInt(match.groups.halfYears) : 0,
		quarters: (match.groups?.quarters ? parseInt(match.groups.quarters) : 0),
		months: match.groups?.months ? parseInt(match.groups.months) : 0,
		weeks: match.groups?.weeks ? parseInt(match.groups.weeks) : 0,
		days: match.groups?.days ? parseInt(match.groups.days) : 0,
		hours: match.groups?.hours ? parseInt(match.groups.hours) : 0,
		minutes: match.groups?.minutes ? parseInt(match.groups.minutes) : 0,
		seconds: match.groups?.seconds ? parseInt(match.groups.seconds) : 0
	}
}

export function parseDurationStrict(text: string): Duration {
	const duration = parseDuration(text);
	if (!duration)
		throw new Error(`Invalid duration ${text}`)
	return duration;
}

export function formatDuration(duration: Duration): string {
	let durationString = "";

	const append = (amount: number, unit: string) => {
		if (amount > 0) {
			if (durationString)
				durationString += " ";
			durationString += `${amount} ${unit}${amount > 1 ? "s" : ""}`;
		}
	}

	append(duration.years, "year");
	append(duration.halfYears, "halfyear");
	append(duration.quarters, "quarter");
	append(duration.months, "month");
	append(duration.weeks, "week");
	append(duration.days, "day");
	append(duration.hours, "hour");
	append(duration.minutes, "minute");
	append(duration.seconds, "second");

	return durationString;
}

export interface Duration {
	readonly years: number;
	readonly halfYears: number;
	readonly quarters: number;
	readonly months: number;
	readonly weeks: number;
	readonly days: number;
	readonly hours: number;
	readonly minutes: number;
	readonly seconds: number;
}

export function optimize(duration: Duration): Duration {
	let { years, halfYears, quarters, months, weeks, days, hours, minutes, seconds } = duration;

	minutes += seconds / 60;
	seconds = seconds % 60;
	hours += minutes / 60;
	minutes = minutes % 60;
	days += hours / 24;
	hours = hours % 24;
	weeks += days / 7;
	days = days % 7;
	quarters += months / 3;
	months = months % 3;
	halfYears += quarters / 2;
	quarters = quarters % 2;
	years += halfYears / 2;
	halfYears = halfYears % 2;

	return {
		years, halfYears, quarters, months, weeks, days, hours, minutes, seconds
	}
}


export function isYearly(duration: Duration) {
	duration = optimize(duration);

	return duration.years === 1 && duration.halfYears === 0 && duration.quarters === 0 && duration.months === 0 && duration.weeks === 0 && duration.days === 0 && duration.hours === 0 && duration.minutes === 0 && duration.seconds === 0;
}

export function isHalfYearly(duration: Duration) {
	duration = optimize(duration);

	return duration.years === 0 && duration.halfYears === 1 && duration.quarters === 0 && duration.months === 0 && duration.weeks === 0 && duration.days === 0 && duration.hours === 0 && duration.minutes === 0 && duration.seconds === 0;
}

export function isQuarterly(duration: Duration) {
	duration = optimize(duration);

	return duration.years === 0 && duration.halfYears === 0 && duration.quarters === 1 && duration.months === 0 && duration.weeks === 0 && duration.days === 0 && duration.hours === 0 && duration.minutes === 0 && duration.seconds === 0;
}

export function isMonthly(duration: Duration) {
	duration = optimize(duration);

	return duration.years === 0 && duration.halfYears === 0 && duration.quarters === 0 && duration.months === 1 && duration.weeks === 0 && duration.days === 0 && duration.hours === 0 && duration.minutes === 0 && duration.seconds === 0;
}

export function isWeekly(duration: Duration) {
	duration = optimize(duration);

	return duration.years === 0 && duration.halfYears === 0 && duration.quarters === 0 && duration.months === 0 && duration.weeks === 1 && duration.days === 0 && duration.hours === 0 && duration.minutes === 0 && duration.seconds === 0;
}

export function isDaily(duration: Duration) {
	duration = optimize(duration);

	return duration.years === 0 && duration.halfYears === 0 && duration.quarters === 0 && duration.months === 0 && duration.weeks === 0 && duration.days === 1 && duration.hours === 0 && duration.minutes === 0 && duration.seconds === 0;
}