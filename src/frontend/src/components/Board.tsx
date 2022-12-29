import { Board as BoardModel, Lane, Task } from '../models/board';
import './Board.css';
import { BoardLane } from './BoardLane';
import { DragDropContext, Droppable, DropResult, ResponderProvided } from 'react-beautiful-dnd';

interface Props {
	board: BoardModel,
	onTaskDrop: (board: BoardModel, taskId: string, task: Task, lane: Lane, index: number) => void;
}

export const Board = (props: Props) => {
	const { board, onTaskDrop } = props;

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
				break;
			case 'in-progress':
				lane = Lane.InProgress;
				break;
			case 'done':
				lane = Lane.Done;
				break;
			default:
				return;
		}
				
		onTaskDrop(board, result.draggableId, task, lane, result.destination.index);
	}

	const readyTasks = board.readyLaneTasks.map(tid => ({id: tid, ...board.tasks[tid] }));
	const inProgressTasks = board.inProgressLaneTasks.map(tid => ({id: tid, ...board.tasks[tid] }));
	const doneTasks = board.doneLaneTasks.map(tid => ({id: tid, ...board.tasks[tid] }));

	return (
		<DragDropContext onDragEnd={(r, p) => onDragEnd(r, p)}>
			<div className="Board">
				<div className="LaneTitle">Ready</div>
				<div className="LaneTitle">In Progress</div>
				<div className="LaneTitle">Done</div>
				<Droppable droppableId="ready">
					{(provided, snapshot) => (
						<div ref={provided.innerRef}
						     style={{ backgroundColor: snapshot.isDraggingOver ? 'blue' : 'grey' }}
						     {...provided.droppableProps}>
							<BoardLane tasks={readyTasks} lane={Lane.Ready} />
							{provided.placeholder}
						</div>
					)}
				</Droppable>
				<Droppable droppableId="in-progress">
					{(provided, snapshot) => (
						<div ref={provided.innerRef}
						     style={{ backgroundColor: snapshot.isDraggingOver ? 'blue' : 'grey' }}
						     {...provided.droppableProps}>
							<BoardLane tasks={inProgressTasks} lane={Lane.InProgress} />
							{provided.placeholder}
						</div>
					)}
				</Droppable>
				<Droppable droppableId="done">
					{(provided, snapshot) => (
						<div ref={provided.innerRef}
						     style={{ backgroundColor: snapshot.isDraggingOver ? 'blue' : 'grey' }}
						     {...provided.droppableProps}>
							<BoardLane tasks={doneTasks} lane={Lane.Done} />
							{provided.placeholder}
						</div>
					)}
				</Droppable>
			</div>
		</DragDropContext>
	)
}
