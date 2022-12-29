import { Draggable } from 'react-beautiful-dnd';
import { IdentifiedTask, Lane } from '../models/board';
import './BoardLane.css';
import { Task } from './Task';

interface Props {
	lane: Lane,
	tasks: IdentifiedTask[]
}

export const BoardLane = (props: Props) => {
	return <div className={`BoardLane`}>
		{props.tasks.map((t, i) => (
			<Draggable key={t.id} draggableId={t.id} index={i}>
				{(provided, snapshot) => (
					<div ref={provided.innerRef}
					     {...provided.draggableProps}
					     {...provided.dragHandleProps}>
						<Task task={t}/>
					</div>
				)}
			</Draggable>
		))}
	</div>
}