import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import { Lane } from '../models/board';
import { TaskBeingEdited, TaskErrors } from './BoardAdmin';
import { ScheduleEditor } from './ScheduleEditor';

interface Props {
	task: TaskBeingEdited;
	onTaskChanged: (task: TaskBeingEdited) => void;
	isDirty: boolean;
	errors: TaskErrors;
	onSave: () => void;
	onDelete: () => void;
	onRevert: () => void;
	isNew: boolean;
	onCancel: () => void;
	onActivate: () => void;
	onDeactivate: () => void;
}

export const TaskEditor = (props: Props) => {
	const { task, onTaskChanged, isDirty, onSave, errors, onDelete, onRevert, isNew, onCancel, onActivate, onDeactivate } = props;

	return (
		<Form>
			<Form.Group>
				<Form.Label>Title</Form.Label>
				<Form.Control type="text" placeholder="Enter a short descriptive title" value={task.title}
				              onChange={e => onTaskChanged({...task, title: e.target.value})}
							  isInvalid={!!errors.title}/>
				<Form.Control.Feedback type="invalid">
					{errors.title}
				</Form.Control.Feedback>
			</Form.Group>
			<Form.Group style={{marginTop: "1em"}}>
				<Form.Label>Description</Form.Label>
				<Form.Control as="textarea" placeholder="Enter a longer description of the task" value={task.description} rows={3}
				              onChange={e => onTaskChanged({...task, description: e.target.value})}
							  isInvalid={!!errors.description}/>
				<Form.Control.Feedback type="invalid">
					{errors.description}
				</Form.Control.Feedback>
			</Form.Group>
			<ScheduleEditor task={task} errors={errors} onScheduleChanged={task => onTaskChanged(task)}/>
			<Form.Group style={{marginTop: "1em"}}>
				<Button variant="primary" type="button" disabled={!isDirty} onClick={() => onSave()}>Save</Button>
				{isNew || <Button variant="danger" type="button" disabled={isDirty} onClick={() => onDelete()} style={{marginLeft: "0.5em"}}>Delete</Button>}
				{isNew || <Button variant="warning" type="button" disabled={!isDirty} onClick={() => onRevert()} style={{marginLeft: "0.5em"}}>Revert</Button>}
				{isNew && <Button variant="danger" type="button" onClick={() => onCancel()} style={{marginLeft: "0.5em"}}>Cancel</Button>}
				{isNew || (!isDirty && task.lane !== undefined && task.lane === Lane.Inactive && <Button variant="outline-danger" type="button" onClick={() => onActivate()} style={{marginLeft: "0.5em"}}>Activate</Button>)}
				{isNew || (!isDirty && task.lane !== undefined && task.lane !== Lane.Inactive && <Button variant="outline-danger" type="button" onClick={() => onDeactivate()} style={{marginLeft: "0.5em"}}>Deactivate</Button>)}
			</Form.Group>
		</Form>
	)
}