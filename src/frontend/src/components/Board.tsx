import { Board as BoardModel, State, Task as TaskModel } from '../models/board';
import './Board.css';
import { BoardLane } from './BoardLane';

interface Props {
	board: BoardModel,
	onTaskBeginDrag: (task: TaskModel, event: React.DragEvent<HTMLElement>) => void;
	onTaskDrop: (requestedState: State, event: React.DragEvent<HTMLElement>) => void;
}

export const Board = (props: Props) => {
	const [ready, inProgress, done] = makeLanes(props.board);

	return <div className="Board">
		<div className="LaneTitle">Ready</div>
		<div className="LaneTitle">In Progress</div>
		<div className="LaneTitle">Done</div>
		<BoardLane tasks={ready} stateForLane={State.Ready} onTaskBeginDrag={props.onTaskBeginDrag} onTaskDrop={props.onTaskDrop} />
		<BoardLane tasks={inProgress} stateForLane={State.InProgress} onTaskBeginDrag={props.onTaskBeginDrag} onTaskDrop={props.onTaskDrop} />
		<BoardLane tasks={done} stateForLane={State.Done} onTaskBeginDrag={props.onTaskBeginDrag} onTaskDrop={props.onTaskDrop} />
	</div>
}

function makeLanes(board: BoardModel): TaskModel[][] {
	const ready: TaskModel[] = [];
	const inProgress: TaskModel[] = [];
	const done: TaskModel[] = [];

	for (const task of board.tasks) {
		switch(task.state) {
			case State.Ready:
				ready.push(task);
				break;
			case State.InProgress:
				inProgress.push(task);
				break;
			case State.Done:
				done.push(task);
				break;
							
		}
	}

	return [ready, inProgress, done];
}