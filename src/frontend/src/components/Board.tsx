import { Board as BoardModel, Lane, Task } from '../models/board';
import './Board.css';
import { BoardLane } from './BoardLane';
import { DragDropContext, Droppable, DropResult, ResponderProvided } from 'react-beautiful-dnd';
import { useEffect } from 'react';
import { origin } from '../api';

interface Props {
	board: BoardModel,
	onTaskDrop: (board: BoardModel, taskId: string, task: Task, lane: Lane, index: number) => void;
}

const vibrate = (vibs: Array<number>) => {
	if (!Boolean(navigator.vibrate))
		return;
	
	navigator.vibrate(vibs);
}

const VIB_FREQ = 25;

export const Board = (props: Props) => {
	const { board, onTaskDrop } = props;

	const backdropStyle = {
		backgroundImage: `url('${origin}/api/resources/backdrop')`
	}

	const onDragStart = () => {
		vibrate([VIB_FREQ]);
	}

	const onDragEnd = (result: DropResult, provided: ResponderProvided) => {
		const task = board.tasks[result.draggableId];
		if (!task)
			return;

		if (!result.destination)
			return;

		let lane: Lane;

		switch (result.destination.droppableId) {
			case 'ready':
				lane = Lane.Ready;
				vibrate([VIB_FREQ]);
				break;
			case 'in-progress':
				lane = Lane.InProgress;
				vibrate([VIB_FREQ]);
				break;
			case 'done':
				lane = Lane.Done;
				vibrate([VIB_FREQ, VIB_FREQ, VIB_FREQ]);
				break;
			default:
				return;
		}
				
		onTaskDrop(board, result.draggableId, task, lane, result.destination.index);
	}

	const readyTasks = board.readyLaneTasks.map(tid => ({id: tid, ...board.tasks[tid] }));
	const inProgressTasks = board.inProgressLaneTasks.map(tid => ({id: tid, ...board.tasks[tid] }));
	const doneTasks = board.doneLaneTasks.map(tid => ({id: tid, ...board.tasks[tid] }));

	const droppableStyle = (isDraggingOver: boolean) => {
		return {
			backgroundColor: isDraggingOver ? '#e0e0e040' : 'transparent'
		}
	}

	return (
		<DragDropContext onDragEnd={(r, p) => onDragEnd(r, p)}
		                 onDragStart={(s, p) => onDragStart()}>
			<div className="BoardContainer" style={{...backdropStyle}}>
				<div className="BoardTitle">
					<div className="LaneTitle">Ready</div>
					<div className="LaneTitle">In Progress</div>
					<div className="LaneTitle">Done</div>
				</div>
				<div className="Board">
					<Droppable droppableId="ready">
						{(provided, snapshot) => (
							<div ref={provided.innerRef}
								style={droppableStyle(snapshot.isDraggingOver)}
								{...provided.droppableProps}>
								<BoardLane tasks={readyTasks} lane={Lane.Ready} />
								{provided.placeholder}
							</div>
						)}
					</Droppable>
					<Droppable droppableId="in-progress">
						{(provided, snapshot) => (
							<div ref={provided.innerRef}
								style={droppableStyle(snapshot.isDraggingOver)}
								{...provided.droppableProps}>
								<BoardLane tasks={inProgressTasks} lane={Lane.InProgress} />
								{provided.placeholder}
							</div>
						)}
					</Droppable>
					<Droppable droppableId="done">
						{(provided, snapshot) => (
							<div ref={provided.innerRef}
								style={droppableStyle(snapshot.isDraggingOver)}
								{...provided.droppableProps}>
								<BoardLane tasks={doneTasks} lane={Lane.Done} />
								{provided.placeholder}
							</div>
						)}
					</Droppable>
				</div>
			</div>
		</DragDropContext>
	)
}
