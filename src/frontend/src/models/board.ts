import { Duration } from "../duration";

export type TaskDictionary = { [key: string]: Task }

export interface Board {
	readonly tasks: TaskDictionary,
	readonly readyLaneTasks: string[],
	readonly inProgressLaneTasks: string[],
	readonly doneLaneTasks: string[],
	readonly inactiveLaneTasks: string[],
}

export enum Lane {
	Inactive = "inactive",
	Ready = "ready",
	InProgress = "in-progress",
	Done = "done"
}

export interface IdentifiedTask extends Task {
	readonly id: string;
}

export interface Task {
	readonly title: string,
	readonly description: string,
	readonly schedule: Schedule,
	readonly lastChange: Date
}

export interface TaskData {
	readonly title: string,
	readonly description: string,
	readonly schedule: Schedule,
}

export interface BoardAndTask {
	readonly board: Board;
	readonly task: IdentifiedTask;
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
