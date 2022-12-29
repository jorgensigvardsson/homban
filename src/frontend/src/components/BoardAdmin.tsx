import { Board, OneTimeSchedule, State, Task } from "../models/board"
import { TaskEditor } from "./TaskEditor"
import './BoardAdmin.css'
import { TaskList } from "./TaskList"
import { useState } from "react"
import moment from "moment"
import { Api, withApi } from "../api"
import Button from 'react-bootstrap/Button';
import Container from "react-bootstrap/Container"
import Row from "react-bootstrap/Row"
import Col from "react-bootstrap/Col"

interface Props {
	board: Board;
	onBoardUpdated: (board: Board) => void;
	api: Api;
}

export interface TaskBeingEdited {
	readonly title: string,
	readonly description: string,
	readonly scheduleType: string;
	readonly period?: string;
	readonly start?: string;
	readonly when?: string;
}

export type TaskErrors = {
	readonly title?: string,
	readonly description?: string,
	readonly scheduleType?: string;
	readonly period?: string;
	readonly start?: string;
	readonly when?: string;
}

export const BoardAdmin = (props: Props) => {
	const { board, api, onBoardUpdated } = props;

	const [editedTask, setEditedTask] = useState<TaskBeingEdited | null>(null);
	const [selectedTask, setSelectedTask] = useState<Task | null>(null);
	const [isDirty, setIsDirty] = useState<boolean>(false);
	const [errors, setErrors] = useState<TaskErrors>({});

	const selectTask = async (task: Task) => {
		if (selectedTask || editedTask) {
			if (selectedTask && selectedTask.id === task.id)
				return;

			if (!selectedTask || isDirty) {
				const confirmed = await new Promise<boolean>(resolve => {
					resolve(window.confirm('There are unsaved changes. Continue?'));
				})

				if (!confirmed)
					return;
			}
		}
		setSelectedTask(task);
		setEditedTask(makeEditable(task));
		setIsDirty(false);
		setErrors({});
	}

	const deleteSelectedTask = async () => {
		if (!selectedTask)
			return;
			
		try {
			const board = await api.deleteTask(selectedTask.id);
			onBoardUpdated(board);
			setSelectedTask(null);
			setEditedTask(null);
		} catch (error: any) {
			alert(`An error occurred: ${error.message}`)
		}
	}

	const revertSelectedTask = () => {
		if (!selectedTask)
			return;

		setSelectedTask(selectedTask);
		setEditedTask(makeEditable(selectedTask));
		setIsDirty(false);
		setErrors({});
	}

	const evaluateEditedTask = (editedTask: TaskBeingEdited) => {
		const errors = findErrors(editedTask);
		const isValid = errors.description === undefined && 
		                errors.period === undefined &&
		                errors.scheduleType === undefined &&
		                errors.start === undefined &&
		                errors.title === undefined &&
		                errors.when === undefined;
		const equal = selectedTask ? areTasksEqual(selectedTask, editedTask) : false;
		setIsDirty(isValid && !equal);
		setErrors(errors);
		setEditedTask(editedTask);
	}

	const save = async () => {
		if (!editedTask)
			return;

		try {
			const boardAndTask = selectedTask 
				? await api.updateTask(selectedTask.id, makeTaskData(editedTask, selectedTask.state))
				: await api.createTask(makeTaskData(editedTask, State.Ready));

			onBoardUpdated(boardAndTask.board);
			setSelectedTask(boardAndTask.task)
			setEditedTask(makeEditable(boardAndTask.task));
			setIsDirty(false);
			setErrors({});
		} catch (error: any) {
			alert(`An error occurred: ${error.message}`)
		}
	}

	const newTask = () => {
		setSelectedTask(null);
		const newTaskData = {
			title: "",
			description: "",
			scheduleType: "periodic-activity"
		};
		setEditedTask(newTaskData);
		evaluateEditedTask(newTaskData);
	}

	const copyAndEditSelectedTask = () => {
		if (!selectedTask)
			return;

		setSelectedTask(null);
		const newTaskData = makeEditable(selectedTask);
		setEditedTask(newTaskData);
		evaluateEditedTask(newTaskData);
	}

	const cancelNewTask = () => {
		setEditedTask(null);
	}

	const sortedTasks = [...board.tasks].sort((a, b) => a.title.localeCompare(b.title))

	return (
		<Container className="BoardAdmin">
			<Row>
				<Col xs={12} sm={editedTask ? 4 : 12}>
					<Container>
						<Row>
							<Col>
								<TaskList tasks={sortedTasks} selectedTask={selectedTask} onSelected={task => selectTask(task)} />
							</Col>
						</Row>
						<Row style={{marginTop: "0.5em"}}>
							<Col>
								<Button variant="primary" disabled={!!(selectedTask && isDirty)} style={{marginLeft: "0.5em"}} onClick={() => newTask()}>New</Button>
								<Button variant="secondary" disabled={!selectedTask || isDirty} style={{marginLeft: "0.5em"}} onClick={() => copyAndEditSelectedTask()}>Copy</Button>
							</Col>
						</Row>
					</Container>
				</Col>				
				<Col xs={editedTask ? 12 : 0} sm={editedTask ? 8 : 0}>
					{editedTask && <TaskEditor task={editedTask}
											onTaskChanged={updatedTask => evaluateEditedTask(updatedTask)}
											isDirty={isDirty}
											errors={errors}
											onSave={() => save()}
											onDelete={() => deleteSelectedTask()}
											onRevert={() => revertSelectedTask()}
											onCancel={() => cancelNewTask()}
											isNew={!selectedTask}/>}
				</Col>
			</Row>
		</Container>
	)
}

export default withApi(BoardAdmin);

const isValidScheduleType = (type: string | undefined) => {
	if (!type)
		return false;

	switch(type) {
		case "one-time":
		case "periodic-calendar":
		case "periodic-activity":
			return true;
		default:
			return false;
	}
}

const findErrors = (current: TaskBeingEdited) => {
	return {
		title: current.title?.trim().length === 0 ? "Please enter a title" : undefined,
		description: current.description?.trim().length === 0 ? "Please enter a description" : undefined,
		scheduleType: !isValidScheduleType(current.scheduleType) ? "Select a type of schedule" : undefined,
		period: current.scheduleType !== "one-time" && !isValidDuration(current.period) ? "Please enter a valid duration" : undefined,
		start: current.scheduleType !== "one-time" && !isValidDate(current.start) ? "Please enter a date" : undefined,
		when: current.scheduleType === "one-time" && !isValidDate(current.when) ? "Please enter a date" : undefined			
	};
}

const isValidDate = (date: string | undefined) => {
	return date !== undefined && parseDate(date) !== null;
}

const isValidDuration = (duration: string | undefined) => {
	return duration !== undefined && parseDuration(duration) !== null;
}

const makeTaskData = (edited: TaskBeingEdited, state: State) => {
	return {
		title: edited.title,
		description: edited.description,
		state: state,
		schedule: makeSchedule(edited)
	}
}

const makeSchedule = (edited: TaskBeingEdited) => {
	switch (edited.scheduleType) {
		case "one-time":
			return {
				type: edited.scheduleType,
				when: parseDateStrict(edited.when!)
			} as OneTimeSchedule
		case "periodic-calendar":
		case "periodic-activity":
			return {
				type: edited.scheduleType,
				start: parseDateStrict(edited.start!),
				period: parseDurationStrict(edited.period!)
			}
		default:
			throw new Error(`Unknown schedule type ${edited.scheduleType}`)
	}
}

const makeEditable = (original: Task) => {
	return {
		title: original.title,
		description: original.description,
		scheduleType: original.schedule.type as string,
		period: original.schedule.type !== "one-time" ? formatDuration(original.schedule.period) : undefined,
		start: original.schedule.type !== "one-time" ? moment(original.schedule.start).format("yyyy-MM-DD") : undefined,
		when: original.schedule.type === "one-time" ? moment(original.schedule.when).format("yyyy-MM-DD") : undefined
	}
}

const areTasksEqual = (original: Task, current: TaskBeingEdited) => {
	if (original.title !== current.title || original.description !== current.description)
		return false;

	if (current.scheduleType !== original.schedule.type)
		return false;

	switch (original.schedule.type) {
		case "one-time":
			return current.when && original.schedule.when.toDateString() === parseDate(current.when)?.toDateString();
		case "periodic-activity":
		case "periodic-calendar":
			return current.start && original.schedule.start.toDateString() === parseDate(current.start)?.toDateString() &&
			       current.period && original.schedule.period.toISOString() === parseDuration(current.period)?.toISOString();
		default:
			return false;
	}
}

const dateRegex = /^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$/
const parseDate = (text: string) => {
	const match = text.match(dateRegex);
	if (!match)
		return null;
	
	return new Date(parseInt(match.groups!.year), parseInt(match.groups!.month) - 1, parseInt(match.groups!.day));
}

const durationRegex = /^((?<years>\d+)\s*y(ear(s)?)?)?\s*((?<quarters>\d+)\s*q(uarter(s)?)?)?\s*((?<months>\d+)\s*mo(nth(s)?)?)?\s*((?<weeks>\d+)\s*w(eek(s)?)?)?\s*((?<days>\d+)\s*d(ay(s)?)?)?\s*((?<hours>\d+)\s*h(our(s)?)?)?\s*((?<minutes>\d+)\s*m(inute(s)?)?)?\s*((?<seconds>\d+)\s*s(econd(s)?)?)?\s*$/
const parseDuration = (text: string) => {
	const match = text.match(durationRegex);
	if (!match)
		return null;

	return moment.duration({
		years: match.groups?.years ? parseInt(match.groups.years) : 0,
		months: match.groups?.months ? parseInt(match.groups.months) : 0,
		quarters: match.groups?.quarters ? parseInt(match.groups.quarters) : 0,
		weeks: match.groups?.weeks ? parseInt(match.groups.weeks) : 0,
		days: match.groups?.days ? parseInt(match.groups.days) : 0,
		hours: match.groups?.hours ? parseInt(match.groups.hours) : 0,
		minutes: match.groups?.minutes ? parseInt(match.groups.minutes) : 0,
		seconds: match.groups?.seconds ? parseInt(match.groups.seconds) : 0
	})
}

const parseDurationStrict = (text: string) => {
	const duration = parseDuration(text);
	if (!duration)
		throw new Error(`Invalid duration ${text}`)
	return duration;
}

const parseDateStrict = (text: string) => {
	const date = parseDate(text);
	if (!date)
		throw new Error(`Invalid date ${text}`)
	return date;
}

const formatDuration = (duration: moment.Duration) => {
	let durationString = "";

	const append = (amount: number, unit: string) => {
		if (amount > 0) {
			if (durationString)
				durationString += " ";
			durationString += `${amount} ${unit}${amount > 1 ? "s" : ""}`;
		}
	}

	console.log("days", duration.days());

	const quarters = Math.floor(duration.months() / 3);
	const months = duration.months() % 3;
	const weeks = Math.floor(duration.days() / 7);
	const days = duration.days() % 7; 

	append(duration.years(), "year");
	append(quarters, "quarter");
	append(months, "month");
	append(weeks, "week");
	append(days, "day");
	append(duration.hours(), "hour");
	append(duration.minutes(), "minute");
	append(duration.seconds(), "second");

	return durationString;
}
