import { Task as TaskModel } from '../models/board';
import './Task.css';
import Card from 'react-bootstrap/Card';

interface Props {
	task: TaskModel;
}

function cardBackground(task: TaskModel) {
	switch(task.schedule.type) {
		case 'one-time':
			return 'light';
		case 'periodic-activity':
		case 'periodic-calendar':
			if (task.schedule.period.asDays() < 7)
				return 'primary';
			if (task.schedule.period.asDays() < 14)
				return 'secondary';
			if (task.schedule.period.asMonths() < 1)
				return 'success';
			if (task.schedule.period.asMonths() < 3)
				return 'warning';
			if (task.schedule.period.asMonths() < 6)
				return 'warning';
			if (task.schedule.period.asYears() < 1)
				return 'danger';
			return 'dark';
	}
}

function cardForeground(task: TaskModel) {
	switch(task.schedule.type) {
		case 'one-time':
			return 'light';
		case 'periodic-activity':
		case 'periodic-calendar':
			if (task.schedule.period.asDays() < 7)
				return 'light';
			if (task.schedule.period.asDays() < 14)
				return 'light';
			if (task.schedule.period.asMonths() < 1)
				return 'light';
			if (task.schedule.period.asMonths() < 3)
				return 'light';
			if (task.schedule.period.asMonths() < 6)
				return 'light';
			if (task.schedule.period.asYears() < 1)
				return 'light';
			return 'dark';
	}
}

function cardTitle(task: TaskModel) {
	switch(task.schedule.type) {
		case 'one-time':
			return 'En gång';
		case 'periodic-activity':
		case 'periodic-calendar':
			if (task.schedule.period.asDays() < 7)
				return 'Dagligen';
			if (task.schedule.period.asDays() < 14)
				return 'Varje vecka';
			if (task.schedule.period.asMonths() < 1)
				return 'Varannan vecka';
			if (task.schedule.period.asMonths() < 3)
				return 'Varje månad';
			if (task.schedule.period.asMonths() < 6)
				return 'Varje kvartal';
			if (task.schedule.period.asYears() < 1)
				return 'Varje halvår';
			return 'dark';
	}
}

export const Task = (props: Props) => {
	return <Card bg={cardBackground(props.task)}
	             text={cardForeground(props.task)}
				 style={{ margin: "0.3em" }}>
		<Card.Header>{cardTitle(props.task)}</Card.Header>
		<Card.Body>
			<Card.Title>{props.task.title}</Card.Title>
			<Card.Text>{props.task.description}</Card.Text>
		</Card.Body>
	</Card>
}