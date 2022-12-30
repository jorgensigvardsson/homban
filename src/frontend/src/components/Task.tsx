import { Lane, Task as TaskModel } from '../models/board';
import './Task.css';
import { useState } from 'react';

interface Props {
	task: TaskModel;
	lane: Lane;
}

function pillText(task: TaskModel) {
	switch (task.schedule.type) {
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
			return 'Årligen';
	}
}

function generateMarkup(text: string) {
	const markup = [];
	const lines = text.split('\n');
	for (let i = 0; i < lines.length; ++i) {
		markup.push(<span key={`line${i}`}>{lines[i]}</span>);
		if (i + 1 < lines.length)
			markup.push(<br key={`newline${i}`}/>);
	}
	return markup;
}

export const Task = (props: Props) => {
	const { task, lane } = props;

	const shortDescriptionLength = 40;
	const isLongDescription = task.description.length > shortDescriptionLength;
	const descriptionFirstPart = isLongDescription ? `${task.description.substring(0, shortDescriptionLength)}...` : task.description;

	const [showFullDescription, setShowFullDescription] = useState(false);

	return (
		<div className={`task ${lane}`} onDoubleClick={() => setShowFullDescription(!showFullDescription)} >
			<div className="content">
				<div>
					<div className="title">{task.title}</div>
					<div>
						{!isLongDescription && task.description}
						{isLongDescription && !showFullDescription && descriptionFirstPart}
						{isLongDescription && showFullDescription && generateMarkup(task.description)}
					</div>
				</div>
				<div className="pill-row">
					<span className="pill period">{pillText(task)}</span>
				</div>
			</div>
			{lane === Lane.Done && <div className='done'>
				DONE
			</div>}
		</div>
	)
}