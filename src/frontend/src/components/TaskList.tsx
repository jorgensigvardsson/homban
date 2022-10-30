import { Task } from "../models/board";
import ListGroup from 'react-bootstrap/ListGroup';
import { TaskListItem } from "./TaskListItem";
import "./TaskList.css";

interface Props {
	tasks: Task[]
	selectedTask: Task | null;
	onSelected: (task: Task) => void;
}

export const TaskList = (props: Props) => {
	return <ListGroup id="task-list">
		{props.tasks.map(task => <TaskListItem isSelected={props.selectedTask?.id === task.id}
		                                       onClick={e => props.onSelected(task)}
											   task={task}
											   key={task.id}/>)}
	</ListGroup>
}