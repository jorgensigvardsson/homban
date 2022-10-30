import { Duration } from 'moment';

export interface Board {
	readonly tasks: Task[]
}

export interface Task {
	readonly id: string;
	readonly title: string,
	readonly description: string,
	readonly state: State,
	readonly schedule: Schedule,
	readonly lastChange: Date
}

export interface TaskData {
	readonly title: string,
	readonly description: string,
	readonly state: State,
	readonly schedule: Schedule,
}

export interface BoardAndTask {
	readonly board: Board;
	readonly task: Task;
}

export enum State {
	Inactive = "inactive",
	Ready = "ready",
	InProgress = "in-progress",
	Done = "done"
}

export interface PeriodicScheduleFollowingCalendar {
	readonly type: "periodic-calendar",
	readonly start: Date,
	readonly period: Duration
}

export interface PeriodicScheduleFollowingActivity {
	readonly type: "periodic-activity",
	readonly start: Date,
	readonly period: Duration
}

export interface OneTimeSchedule {
	readonly type: "one-time",
	readonly when: Date
}

export type Schedule = PeriodicScheduleFollowingCalendar | PeriodicScheduleFollowingActivity | OneTimeSchedule;