import { IdentifiedTask } from "../models/board";
import ListGroup from 'react-bootstrap/ListGroup';
import { TaskListItem } from "./TaskListItem";
import "./TaskList.css";

interface Props {
	tasks: IdentifiedTask[]
	selectedTask: IdentifiedTask | null;
	onSelected: (task: IdentifiedTask) => void;
}

export const TaskList = (props: Props) => {
	return <ListGroup id="task-list">
		{props.tasks.map(task => <TaskListItem isSelected={props.selectedTask?.id === task.id}
		                                       onClick={e => props.onSelected(task)}
											   task={task}
											   key={task.id}/>)}
	</ListGroup>
}