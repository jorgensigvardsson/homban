import React from 'react';
import { State, Task as TaskModel } from '../models/board';
import './BoardLane.css';
import { Task } from './Task';

interface Props {
	stateForLane: State,
	tasks: TaskModel[],
	onTaskBeginDrag: (task: TaskModel, event: React.DragEvent<HTMLElement>) => void;
	onTaskDrop: (requestedState: State, event: React.DragEvent<HTMLElement>) => void;
}

export const BoardLane = (props: Props) => {
	const onDrop = (e: React.DragEvent<HTMLElement>) => {
		props.onTaskDrop(props.stateForLane, e);
	}

	const onDragOver = (e: React.DragEvent<HTMLElement>) => {
		// TODO: Highlight the lane on which we are over
		e.preventDefault();
	}

	return <div className={`BoardLane`}
	            onDrop={e => onDrop(e)}
				onDragOver={e => onDragOver(e)}>
		{props.tasks.map((t, i) => <Task key={i} task={t} onTaskBeginDrag={props.onTaskBeginDrag}/>)}
	</div>
}