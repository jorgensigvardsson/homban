import Form from 'react-bootstrap/Form';
import { TaskBeingEdited, TaskErrors } from "./BoardAdmin";

interface Props {
	task: TaskBeingEdited;
	errors: TaskErrors;
	onScheduleChanged: (task: TaskBeingEdited) => void;
}

export const ScheduleEditor = (props: Props) => {
	const { task, errors, onScheduleChanged } = props;

	const onTypeChanged = (type: string) => {
		onScheduleChanged({ ...task, scheduleType: type })
	}

	return (
		<>
			<Form.Group style={{marginTop: "1em"}}>
				<Form.Label>Type of Schedule</Form.Label>
				<Form.Select value={task.scheduleType}
				             onChange={e => onTypeChanged(e.target.value)}
							 isInvalid={!!errors.scheduleType}>
					<option value="one-time">One Time Schedule</option>
					<option value="periodic-calendar">Periodic, resets on Calendar</option>
					<option value="periodic-activity">Periodic, resets after activity done</option>
				</Form.Select>
				<Form.Control.Feedback type="invalid">
					{errors.scheduleType}
				</Form.Control.Feedback>
			</Form.Group>
			{task.scheduleType === "one-time" && <OneTimeScheduleEditor task={task} errors={errors} onScheduleChanged={onScheduleChanged}/>}
			{task.scheduleType === "periodic-calendar" && <PeriodicScheduleFollowingCalendarScheduleEditor task={task} errors={errors} onScheduleChanged={onScheduleChanged}/>}
			{task.scheduleType === "periodic-activity" && <PeriodicScheduleFollowingActivityEditor task={task} errors={errors} onScheduleChanged={onScheduleChanged}/>}
		</>
	)
}

interface OneTimeProps {
	task: TaskBeingEdited;
	errors: TaskErrors;
	onScheduleChanged: (task: TaskBeingEdited) => void;
}

const OneTimeScheduleEditor = (props: OneTimeProps) => {
	const { task, errors, onScheduleChanged } = props;

	return (
		<Form.Group style={{marginTop: "1em"}}>
			<Form.Label>When it is available</Form.Label>
			<Form.Control 
				type="date"
				value={task.when ?? ""}
				onChange={e => onScheduleChanged({...task, when: e.target.value})}
				isInvalid={!!errors.when}
			/>
			<Form.Control.Feedback type="invalid">
				{errors.when}
			</Form.Control.Feedback>
		</Form.Group>
	)
}

const PeriodHint = () => <>Units: y(ear), q(uarter), mo(nth), w(eek), d(ay). Examples: <i>3 weeks</i>, <i>3 w</i>, <i>3 week</i></>

interface CalendarProps {
	task: TaskBeingEdited;
	errors: TaskErrors;
	onScheduleChanged: (task: TaskBeingEdited) => void;
}

const PeriodicScheduleFollowingCalendarScheduleEditor = (props: CalendarProps) => {
	const { task, errors, onScheduleChanged } = props;

	return (
		<>
			<Form.Group style={{marginTop: "1em"}}>
				<Form.Label>When it is available</Form.Label>
				<Form.Control 
					type="date"
					value={task.start ?? ""}
					onChange={e => onScheduleChanged({...task, start: e.target.value})}
					isInvalid={!!errors.start}
				/>
				<Form.Control.Feedback type="invalid">
					{errors.start}
				</Form.Control.Feedback>
			</Form.Group>
			<Form.Group style={{marginTop: "1em"}}>
				<Form.Label>Period after it is available again (from start)</Form.Label>
				<Form.Control type="text"
				              value={task.period ?? ""}
							  placeholder="E.g. 7 days"
							  onChange={e => onScheduleChanged({...task, period: e.target.value})}
							  isInvalid={!!errors.period}/>
				{!errors.period && <Form.Text className="text-muted">
					{PeriodHint()}
				</Form.Text>}
				<Form.Control.Feedback type="invalid">
					{errors.period}<br/>{PeriodHint()}
				</Form.Control.Feedback>
			</Form.Group>
		</>
	)
}

interface ActivityProps {
	task: TaskBeingEdited;
	errors: TaskErrors;
	onScheduleChanged: (task: TaskBeingEdited) => void;
}

const PeriodicScheduleFollowingActivityEditor = (props: ActivityProps) => {
	const { task, errors, onScheduleChanged } = props;

	return (
		<>
			<Form.Group style={{marginTop: "1em"}}>
				<Form.Label>When it is available</Form.Label>
				<Form.Control 
					type="date"
					value={task.start ?? ""}
					onChange={e => onScheduleChanged({...task, start: e.target.value})}
					isInvalid={!!errors.start}
					/>
				<Form.Control.Feedback type="invalid">
					{errors.start}
				</Form.Control.Feedback>
			</Form.Group>
			<Form.Group style={{marginTop: "1em"}}>
				<Form.Label>Period after it is available again (from done)</Form.Label>
				<Form.Control type="text"
				              value={task.period ?? ""}
							  placeholder="E.g. 7 days"
							  onChange={e => onScheduleChanged({...task, period: e.target.value})}
							  isInvalid={!!errors.period}/>
				{!errors.period && <Form.Text className="text-muted">
					{PeriodHint()}
				</Form.Text>}
				<Form.Control.Feedback type="invalid">
					{errors.period}<br/>{PeriodHint()}
				</Form.Control.Feedback>
			</Form.Group>
		</>
	)
}
