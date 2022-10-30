import ListGroupItem from 'react-bootstrap/ListGroupItem';
import { Task } from "../models/board";

interface Props {
	task: Task;
	isSelected: boolean;
	onClick: (task: Task) => void;
}

export const TaskListItem = (props: Props) => {
	const className = props.isSelected ? "active" : "";

	return <ListGroupItem className={className}
	                      style={{ cursor: 'pointer'}}
						  onClick={e => props.onClick(props.task)}>{props.task.title}</ListGroupItem>
}