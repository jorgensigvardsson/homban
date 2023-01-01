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

export type PartialDuration = Partial<Duration>;

export function MakeDuration(v: PartialDuration): Duration {
	return {
		years: v.years ?? 0,
		halfYears: v.halfYears ?? 0,
		quarters: v.quarters ?? 0,
		months: v.months ?? 0,
		weeks: v.weeks ?? 0,
		days: v.days ?? 0,
		hours: v.hours ?? 0,
		minutes: v.minutes ?? 0,
		seconds: v.seconds ?? 0,
	} 
}

export function optimize(duration: Duration): Duration {
	let { years, halfYears, quarters, months, weeks, days, hours, minutes, seconds } = duration;

	minutes += Math.floor(seconds / 60);
	seconds = seconds % 60;
	hours += Math.floor(minutes / 60);
	minutes = minutes % 60;
	days += Math.floor(hours / 24);
	hours = hours % 24;
	weeks += Math.floor(days / 7);
	days = days % 7;
	quarters += Math.floor(months / 3);
	months = months % 3;
	halfYears += Math.floor(quarters / 2);
	quarters = quarters % 2;
	years += Math.floor(halfYears / 2);
	halfYears = halfYears % 2;

	return {
		years, halfYears, quarters, months, weeks, days, hours, minutes, seconds
	}
}

function eq(d: Duration, d2: PartialDuration) {
	return (d.years === (d2.years ?? 0)) &&
	       (d.halfYears === (d2.halfYears ?? 0)) &&
		   (d.quarters === (d2.quarters ?? 0)) &&
		   (d.months === (d2.months ?? 0)) &&
		   (d.weeks === (d2.weeks ?? 0)) &&
		   (d.days === (d2.days ?? 0)) &&
		   (d.hours === (d2.hours ?? 0)) &&
		   (d.minutes === (d2.minutes ?? 0)) &&
		   (d.seconds === (d2.seconds ?? 0));
}


export function isYearly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { years: 1});
}

export function isHalfYearly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { halfYears: 1});
}

export function isQuarterly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { quarters: 1});
}

export function isMonthly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { months: 1});
}

export function isBiMonthly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { months: 2});
}

export function isWeekly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { weeks: 1});
}

export function isBiWeekly(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { weeks: 2});
}

export function isDaily(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { days: 1});
}

export function isBiDaily(duration: Duration) {
	duration = optimize(duration);

	return eq(duration, { days: 2});
}