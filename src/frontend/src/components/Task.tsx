import { Lane,  Task as TaskModel } from '../models/board';
import './Task.css';
import { useState } from 'react';
import { Duration, isBiDaily, isBiMonthly, isBiWeekly, isDaily, isHalfYearly, isMonthly, isQuarterly, isWeekly, isYearly } from '../duration';

interface Props {
	task: TaskModel;
	lane: Lane;
	isDragging: boolean;
}

export function pillPeriodicText(period: Duration) {
	if (isYearly(period))
		return "Årligen";
	if (isHalfYearly(period))
		return "Varje halvår";
	if (isQuarterly(period))
		return "Varje kvartal";
	if (isBiMonthly(period))
		return "Varannan månad";
	if (isMonthly(period))
		return "Varje månad";
	if (isBiWeekly(period))
		return "Varannan vecka";
	if (isWeekly(period))
		return "Varje vecka";
	if (isBiDaily(period))
		return "Varannan dag";
	if (isDaily(period))
		return "Dagligen";
	return 'Annat';
}

export function pillText(task: TaskModel) {
	switch (task.schedule.type) {
		case 'one-time':
			return 'En gång';
		case 'periodic-activity':
		case 'periodic-calendar':
			return pillPeriodicText(task.schedule.period);
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
	const { task, lane, isDragging } = props;

	const shortDescriptionLength = 40;
	const isLongDescription = task.description.length > shortDescriptionLength;
	const descriptionFirstPart = isLongDescription ? `${task.description.substring(0, shortDescriptionLength)}...` : task.description;

	const [showFullDescription, setShowFullDescription] = useState(false);

	return (
		<div className={`task ${lane}${isDragging ? ' dragging' : ''}`} onDoubleClick={() => setShowFullDescription(!showFullDescription)} >
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