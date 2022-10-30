import { Board, State, Task } from "../models/board";

export const addNewTask = (board: Board, task: Task) => {
	return {...board, tasks: [...board.tasks, task]};
}

export const setTaskState = (board: Board, task: Task, state: State) => {
	const index = board.tasks.findIndex(t => t.id === task.id);
	if (index < 0)
		throw new Error(`Task ${task.id} is not a member of the board`);

	// When changing state on a task, move it to the last position
	return {
		...board, 
		tasks: [...board.tasks.slice(0, index), ...board.tasks.slice(index + 1), {...task, state: state}]
	};
}

export const moveDoneTasksToInactiveAfterMidnight = (board: Board, now: Date) => {
	return {
		...board, 
		tasks: [
			board.tasks.map(t => ({
				...t,
				state: t.state === State.Done && isAfterMidnight(t.lastChange, now) ? State.Inactive : t.state
			}))
		]
	};
}

export const isAfterMidnight = (lastChange: Date, now: Date) => {
	return now.getFullYear() > lastChange.getFullYear() ||
	       now.getMonth() > lastChange.getMonth() ||
		   now.getDate() > lastChange.getDate();
}

export const getTask = (board: Board, taskId: string) => {
	const index = board.tasks.findIndex(t => t.id === taskId);
	if (index < 0)
		throw new Error(`Task ${taskId} is not a member of the board`);

	return board.tasks[index];
}